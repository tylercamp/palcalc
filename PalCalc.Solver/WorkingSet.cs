using Newtonsoft.Json.Linq;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using Serilog;
using System;
using System.Collections.Generic;
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
            PalProperty.IvValidity
        );

        private CancellationToken token;
        private PalPropertyGrouping content;
        private List<(IPalReference, IPalReference)> remainingWork;

        int maxThreads;
        PalSpecifier target;
        private List<IPalReference> discoveredResults = new List<IPalReference>();
        public IEnumerable<IPalReference> Result => discoveredResults.Distinct().GroupBy(r => r.BreedingEffort).SelectMany(PruningFunc);

        Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> PruningFunc;

        public WorkingSet(PalSpecifier target, PruningRulesBuilder pruningRulesBuilder, IEnumerable<IPalReference> initialContent, int maxThreads, CancellationToken token)
        {
            this.target = target;

            PruningFunc = pruningRulesBuilder.BuildAggregate(token).Apply;

            content = new PalPropertyGrouping(DefaultGroupFn);
            content.AddRange(initialContent);

            discoveredResults.AddRange(content.All.Where(target.IsSatisfiedBy));

            remainingWork = initialContent.SelectMany(p1 => initialContent.Select(p2 => (p1, p2))).ToList();
            this.token = token;

            if (maxThreads <= 0) maxThreads = Environment.ProcessorCount;

            this.maxThreads = maxThreads;
        }

        public bool IsOptimal(IPalReference p)
        {
            var match = content[p]?.FirstOrDefault();
            if (match == null) return true;

            return match == null || p.BreedingEffort < match.BreedingEffort;
        }

        /// <summary>
        /// Uses the provided `doWork` function to produce results for all remaining work to be done. The results
        /// returned by `doWork` are merged with the current working set of results and the next set of work
        /// is updated.
        /// </summary>
        /// <param name="doWork"></param>
        /// <returns>Whether or not any changes were made by merging the current working set with the results of `doWork`.</returns>
        public bool Process(Func<List<(IPalReference, IPalReference)>, IEnumerable<IPalReference>> doWork)
        {
            if (remainingWork.Count == 0) return false;

            logger.Debug("beginning work processing");
            var newResults = doWork(remainingWork).ToList();

            // since we know the breeding effort of each potential instance, we can ignore new instances
            // with higher effort than existing known instances
            //
            // (this is the main optimization that lets the solver complete in less than a week)

            // `PruneCollection` is fairly heavy and single-threaded, perform pruning of multiple batches of the
            // main set of references before pruning the final combined collection

            discoveredResults.AddRange(newResults.TakeWhile(_ => !token.IsCancellationRequested).Where(target.IsSatisfiedBy));

            logger.Debug("performing pre-prune");
            var pruned = PruneCollection(
                newResults.Batched(newResults.Count / maxThreads + 1)
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
                if (token.IsCancellationRequested) return changed;

                var refNewInst = newInstances.First();

                // these are results to be used as output, don't bother adding them to working set / continue breeding those
                if (refNewInst is BredPalReference && target.IsSatisfiedBy(refNewInst))
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

            remainingWork.Clear();
            remainingWork.EnsureCapacity(toAdd.Count * toAdd.Count + 2 * toAdd.Count * content.TotalCount);

            remainingWork.AddRange(content.All
                // need to check results between new and old content
                .SelectMany(p1 => toAdd.Select(p2 => (p1, p2)))
                // and check results within the new content
                .Concat(toAdd.SelectMany(p1 => toAdd.Select(p2 => (p1, p2))))
            );

            foreach (var ta in toAdd)
                content.Add(ta);

            return changed;
        }

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, passives, etc.)
        private IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
                .TakeWhile(_ => !token.IsCancellationRequested)
                .GroupBy(pref => DefaultGroupFn(pref))
                .SelectMany(g => PruningFunc(g.Distinct()));
    }
}
