﻿using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    internal class WorkingSet
    {
        private static ILogger logger = Log.ForContext<WorkingSet>();

        public static PalProperty.GroupIdFn DefaultGroupFn = PalProperty.Combine(
            PalProperty.Pal,
            PalProperty.Gender,
            PalProperty.EffectivePassives,
            PalProperty.IvRelevance,
            PalProperty.GenderReversers
            //PalProperty.GoldCost
        );

        private SolverStateController controller;
        private PalPropertyGrouping content;
        private ILazyCartesianProduct<IPalReference> remainingParentPairs;

        int maxThreads;
        PalSpecifier target;
        private List<IPalReference> discoveredResults = new List<IPalReference>();
        public IEnumerable<IPalReference> Result => discoveredResults.Distinct().GroupBy(r => r.BreedingEffort).SelectMany(PruningFunc);
        
        public IEnumerable<IPalReference> CurrentContent => content.All;

        Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> PruningFunc;

        public WorkingSet(PalSpecifier target, PruningRulesBuilder pruningRulesBuilder, IEnumerable<IPalReference> initialContent, int maxThreads, SolverStateController controller)
        {
            this.target = target;
            this.controller = controller;

            PruningFunc = pruningRulesBuilder.BuildAggregate(controller.CancellationToken).Apply;

            content = new PalPropertyGrouping(DefaultGroupFn);
            content.AddRange(initialContent);

            discoveredResults.AddRange(content.All.Where(target.IsSatisfiedBy));

            var initialList = initialContent.ToList();
            remainingParentPairs = new LazyCartesianProduct<IPalReference>(initialList, initialList);

            if (maxThreads <= 0) maxThreads = Environment.ProcessorCount;

            this.maxThreads = maxThreads;
        }

        public bool IsOptimal(IPalReference p)
        {
            var match = content[p]?.FirstOrDefault();

            if (match == null) return true;
            if (p.BreedingEffort > match.BreedingEffort) return false;
            if (p.BreedingEffort < match.BreedingEffort) return true;

            if (p.NumTotalGenderReversers > match.NumTotalGenderReversers) return false;
            if (p.NumTotalGenderReversers < match.NumTotalGenderReversers) return true;

            if (p.TotalCost > match.TotalCost) return false;
            if (p.TotalCost <  match.TotalCost) return true;

            // all important measures are equal, this is as good as (but not more optimal than) what we've already stored
            return false;
        }

        /// <summary>
        /// Uses the provided `doWork` function to produce results for all remaining pair-wise work to be done. The results
        /// returned by `doWork` are merged with the current working set of results and the next set of work
        /// is updated.
        /// </summary>
        /// <param name="doWork"></param>
        /// <returns>Whether or not any changes were made by merging the current working set with the results of `doWork`.</returns>
        public bool UpdateByPairs(Func<ILazyCartesianProduct<IPalReference>, IEnumerable<IPalReference>> doWork)
        {
            if (remainingParentPairs.Count == 0) return false;

            logger.Debug("beginning pairs processing");

            var newResults = doWork(remainingParentPairs).ToList();

            var existingContent = content.All.OrderBy(p => p.Pal.Id).ToList();
            var changeset = MergeWithResults(newResults);
            existingContent.RemoveAll(changeset.Removed.Contains);
            
            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                (content.All.OrderBy(p => p.Pal.Id).ToList(), changeset.Added),
                (changeset.Added, changeset.Added)
            ]);

            foreach (var ta in changeset.Added.TakeUntilCancelled(controller.CancellationToken))
            {
                controller.PauseIfRequested();
                content.Add(ta);
            }

            return changeset.Changed;
        }

        /// <summary>
        /// Uses the provided `doWork` function to produce results for each individual `IPalReference`. The results
        /// returned by `doWork` are merged with the current working set of results and the next set of work
        /// is updated.
        /// </summary>
        /// <returns>Whether any changes were made</returns>
        public bool UpdateBySingle(Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> doWork)
        {
            logger.Debug("beginning single-item processing");

            var newItems = doWork(content.All).ToList();
            if (!newItems.Any()) return false;

            var existingContent = content.All.OrderBy(p => p.Pal.Id).ToList();
            var changeset = MergeWithResults(newItems);
            existingContent.RemoveAll(changeset.Removed.Contains);

            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                remainingParentPairs.Where(parent => !changeset.Removed.Contains(parent), controller.CancellationToken),
                new LazyCartesianProduct<IPalReference>(changeset.Added, existingContent),
                new LazyCartesianProduct<IPalReference>(changeset.Added, changeset.Added)
            ]);

            foreach (var ta in changeset.Added.TakeUntilCancelled(controller.CancellationToken))
            {
                controller.PauseIfRequested();
                content.Add(ta);
            }

            return changeset.Changed;
        }

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, passives, etc.)
        private IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
                .TakeUntilCancelled(controller.CancellationToken)
                .Tap(_ => controller.PauseIfRequested())
                .GroupBy(pref => DefaultGroupFn(pref))
                .SelectMany(g => PruningFunc(g.Distinct()));

        private record class MergeChangeset(bool Changed, List<IPalReference> Added, HashSet<IPalReference> Removed);

        private MergeChangeset MergeWithResults(List<IPalReference> newResults)
        {
            var changed = false;
            var allAdded = new List<IPalReference>();
            var allRemoved = new List<IPalReference>();

            // since we know the breeding effort of each potential instance, we can ignore new instances
            // with higher effort than existing known instances
            //
            // (this is the main optimization that lets the solver complete in less than a week)

            // `PruneCollection` is fairly heavy and single-threaded, perform pruning of multiple batches of the
            // main set of references before pruning the final combined collection

            discoveredResults.AddRange(
                newResults
                    .TakeUntilCancelled(controller.CancellationToken)
                    .Tap(_ => controller.PauseIfRequested())
                    .Where(target.IsSatisfiedBy)
            );
            if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(false, [], []);

            logger.Debug("performing pre-prune on {count} items", newResults.Count);
            var pruned = PruneCollection(
                newResults
                    .BatchedAsParallel()
                    .WithCancellation(controller.CancellationToken)
                    .WithDegreeOfParallelism(maxThreads)
                    .SelectMany(batch => PruneCollection(batch).ToList())
                    .ToList()
            );
            if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(false, [], []);

            logger.Debug("merging");

            foreach (var newInstances in pruned.GroupBy(i => DefaultGroupFn(i)).Select(g => g.ToList()).ToList())
            {
                if (controller.CancellationToken.IsCancellationRequested) return new MergeChangeset(changed, [], []);
                controller.PauseIfRequested();

                var refNewInst = newInstances.First();

                // these are results to be used as output, don't bother adding them to working set / continue breeding those
                if ((refNewInst is BredPalReference || refNewInst is SurgeryTablePalReference) && target.IsSatisfiedBy(refNewInst))
                {
                    // (though if we're not at the passive limit and there are some optional passives
                    //  we'd like, then we'll keep this in the pool)
                    if (
                        // at max passives
                        refNewInst.EffectivePassives.Count(t => t is not RandomPassiveSkill) == GameConstants.MaxTotalPassives ||
                        // there's nothing else we'd be interested in
                        !target.OptionalPassives.Except(refNewInst.EffectivePassives).Any()
                    ) continue;
                }

                var existingInstances = content[refNewInst];
                var refInst = existingInstances?.FirstOrDefault();

                if (refInst != null)
                {
                    var newSelection = PruningFunc(existingInstances.Concat(newInstances.Except(existingInstances)));

                    var added = newInstances.Intersect(newSelection).Except(existingInstances);
                    var removed = existingInstances.Except(newSelection);

                    if (added.Any())
                    {
                        allAdded.AddRange(added);
                        changed = true;
                    }

                    if (removed.Any())
                    {
                        foreach (var r in removed.ToList())
                        {
                            content.Remove(r);
                            allRemoved.Add(r);
                        }
                        changed = true;
                    }
                }
                else
                {
                    allAdded.AddRange(newInstances);
                    changed = true;
                }
            }

            // (minor memory bandwidth improvement by minimizing how often we switch types of pals, hopefully
            // keeps some pal-specific data like child pal + gender probabilities in CPU cache.)
            allAdded = allAdded.OrderBy(p => p.Pal.Id).ToList();

            return new MergeChangeset(changed, allAdded, new HashSet<IPalReference>(allRemoved));
        }
    }
}
