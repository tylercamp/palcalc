using Newtonsoft.Json.Linq;
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
            PalProperty.IvRelevance
            //PalProperty.GoldCost
        );

        private SolverStateController controller;
        private PalPropertyGrouping content;
        private ILazyCartesianProduct<IPalReference> remainingParentPairs;

        int maxThreads;
        PalSpecifier target;
        private List<IPalReference> discoveredResults = new List<IPalReference>();
        public IEnumerable<IPalReference> Result => discoveredResults.Distinct().GroupBy(r => r.BreedingEffort).SelectMany(PruningFunc);
        
        /// <summary>
        /// Access to current working set content for surgery generation
        /// </summary>
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

            logger.Debug("beginning work processing");

            var newResults = doWork(remainingParentPairs).ToList();

            // since we know the breeding effort of each potential instance, we can ignore new instances
            // with higher effort than existing known instances
            //
            // (this is the main optimization that lets the solver complete in less than a week)

            // `PruneCollection` is fairly heavy and single-threaded, perform pruning of multiple batches of the
            // main set of references before pruning the final combined collection

            discoveredResults.AddRange(
                newResults
                    .TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested)
                    .Tap(_ => controller.PauseIfRequested())
                    .Where(target.IsSatisfiedBy)
            );

            logger.Debug("performing pre-prune");
            var pruned = PruneCollection(
                newResults.BatchedForParallel()
                    .AsParallel()
                    .WithDegreeOfParallelism(maxThreads)
                    .SelectMany(batch => PruneCollection(batch).ToList())
                    .ToList()
            );

            logger.Debug("merging");
            var changed = false;
            var toAdd = new List<IPalReference>();

            foreach (var newInstances in pruned.GroupBy(i => DefaultGroupFn(i)).Select(g => g.ToList()).ToList())
            {
                if (controller.CancellationToken.IsCancellationRequested) return changed;
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
                    var newSelection = PruningFunc(existingInstances.Concat(newInstances));

                    var added = newInstances.Intersect(newSelection);
                    var removed = existingInstances.Except(newSelection);

                    if (added.Any())
                    {
                        toAdd.AddRange(added);
                        changed = true;
                    }

                    if (removed.Any())
                    {
                        foreach (var r in removed.ToList())
                            content.Remove(r);
                        changed = true;
                    }
                }
                else
                {
                    toAdd.AddRange(newInstances);
                    changed = true;
                }
            }

            // (minor memory bandwidth improvement by minimizing how often we switch types of pals, hopefully
            // keeps some pal-specific data like child pal + gender probabilities in CPU cache.)
            toAdd = toAdd.OrderBy(p => p.Pal.Id).ToList();
            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                (content.All.OrderBy(p => p.Pal.Id).ToList(), toAdd),
                (toAdd, toAdd)
            ]);

            foreach (var ta in toAdd.TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested))
            {
                controller.PauseIfRequested();
                content.Add(ta);
            }

            return changed;
        }

        /// <summary>
        /// Merges new single-parent results (e.g., from surgery) with the current working set.
        /// Updates remainingWork to include pairs between existing content and new items, plus new-item pairs.
        /// </summary>
        /// <param name="newItems">New IPalReference items to merge</param>
        /// <returns>Whether any changes were made</returns>
        public bool UpdateBySingle(Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> doWork)
        {
            var newItems = doWork(content.All).ToList();
            if (!newItems.Any()) return false;

            logger.Debug("beginning single-item processing with {count} items", newItems.Count);

            // Reuse the same pruning/merging logic from UpdateByPairs
            discoveredResults.AddRange(
                newItems
                    .TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested)
                    .Tap(_ => controller.PauseIfRequested())
                    .Where(target.IsSatisfiedBy)
            );

            logger.Debug("performing pre-prune");
            var pruned = PruneCollection(
                newItems.BatchedForParallel()
                    .AsParallel()
                    .WithDegreeOfParallelism(maxThreads)
                    .SelectMany(batch => PruneCollection(batch).ToList())
                    .ToList()
            );

            logger.Debug("merging");
            var changed = false;
            var toAdd = new List<IPalReference>();

            foreach (var newInstances in pruned.GroupBy(i => DefaultGroupFn(i)).Select(g => g.ToList()).ToList())
            {
                if (controller.CancellationToken.IsCancellationRequested) return changed;
                controller.PauseIfRequested();

                var refNewInst = newInstances.First();

                // Same logic as UpdateByPairs for skipping completed results
                if ((refNewInst is BredPalReference || refNewInst is SurgeryTablePalReference) && target.IsSatisfiedBy(refNewInst))
                {
                    if (
                        refNewInst.EffectivePassives.Count(t => t is not RandomPassiveSkill) == GameConstants.MaxTotalPassives ||
                        !target.OptionalPassives.Except(refNewInst.EffectivePassives).Any()
                    ) continue;
                }

                var existingInstances = content[refNewInst];
                var refInst = existingInstances?.FirstOrDefault();

                if (refInst != null)
                {
                    var newSelection = PruningFunc(existingInstances.Concat(newInstances));

                    var added = newInstances.Intersect(newSelection);
                    var removed = existingInstances.Except(newSelection);

                    if (added.Any())
                    {
                        toAdd.AddRange(added);
                        changed = true;
                    }

                    if (removed.Any())
                    {
                        foreach (var r in removed.ToList())
                            content.Remove(r);
                        changed = true;
                    }
                }
                else
                {
                    toAdd.AddRange(newInstances);
                    changed = true;
                }
            }

            // Update remainingWork with both (existing, new) and (new, new) pairs
            toAdd = toAdd.OrderBy(p => p.Pal.Id).ToList();
            var existingContent = content.All.OrderBy(p => p.Pal.Id).ToList();

            remainingParentPairs = new ConcatenatedLazyCartesianProduct<IPalReference>([
                remainingParentPairs,
                new LazyCartesianProduct<IPalReference>(toAdd, existingContent),
                new LazyCartesianProduct<IPalReference>(toAdd, toAdd)
            ]);

            foreach (var ta in toAdd.TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested))
            {
                controller.PauseIfRequested();
                content.Add(ta);
            }

            return changed;
        }

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, passives, etc.)
        private IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
                .TakeWhile(_ => !controller.CancellationToken.IsCancellationRequested)
                .Tap(_ => controller.PauseIfRequested())
                .GroupBy(pref => DefaultGroupFn(pref))
                .SelectMany(g => PruningFunc(g.Distinct()));
    }
}
