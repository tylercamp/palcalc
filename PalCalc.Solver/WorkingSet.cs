using Newtonsoft.Json.Linq;
using PalCalc.Model;
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


        private CancellationToken token;
        private Dictionary<PalId, List<IPalReference>> content;
        private List<(IPalReference, IPalReference)> remainingWork;

        PalSpecifier target;
        private List<IPalReference> discoveredResults = new List<IPalReference>();
        public IEnumerable<IPalReference> Result => discoveredResults.Distinct().GroupBy(r => r.BreedingEffort).SelectMany(PruningFunc);

        Func<IEnumerable<IPalReference>, IEnumerable<IPalReference>> PruningFunc;

        public WorkingSet(PalSpecifier target, IEnumerable<IPalReference> initialContent, CancellationToken token)
        {
            this.target = target;

            // TODO - allow external configuration
            var resultPrunings = new List<IResultPruning>()
            {
                new MinimumEffortPruning(token),
                new MinimumInputsPruning(token),
                new PreferredLocationPruning(token),
                new MinimumReusePruning(token),
                new MinimumWildPalsPruning(token),
                new MinimumReferencedPlayersPruning(token),
                new VariedResultsPruning(token, maxSimilarityPercent: 0.1f),
                new ResultLimitPruning(token, maxResults: 3),
            };

            PruningFunc = (results) =>
            {
                var pruned = results;
                foreach (var order in resultPrunings)
                    pruned = order.Apply(pruned);
                return pruned;
            };

            content = PruneCollection(initialContent).GroupBy(p => p.Pal.Id).ToDictionary(g => g.Key, g => g.ToList());

            discoveredResults.AddRange(content.SelectMany(kvp => kvp.Value).Where(target.IsSatisfiedBy));

            remainingWork = initialContent.SelectMany(p1 => initialContent.Select(p2 => (p1, p2))).ToList();
            this.token = token;
        }

        public bool IsOptimal(IPalReference p)
        {
            if (!content.ContainsKey(p.Pal.Id)) return true;

            var items = content[p.Pal.Id];
            var match = items.FirstOrDefault(i => i.Gender == p.Gender && i.EffectiveTraitsHash == p.EffectiveTraitsHash);

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
                newResults.ToList().BatchedForParallel()
                    .AsParallel()
                    .SelectMany(batch => PruneCollection(batch).ToList())
                    .ToList()
            );

            logger.Debug("merging");
            var changed = false;
            var toAdd = new List<IPalReference>();

            foreach (var newInstances in pruned.GroupBy(i => (i.Pal, i.Gender, i.EffectiveTraitsHash)).Select(g => g.ToList()).ToList())
            {
                if (token.IsCancellationRequested) return changed;

                var refNewInst = newInstances.First();

                if (target.IsSatisfiedBy(refNewInst))
                {
                    // these are results to be used as output, don't bother adding them to working set / continue breeding those
                    continue;
                }

                var existingInstances = content.GetValueOrElse(refNewInst.Pal.Id, new List<IPalReference>())
                    .TakeWhile(_ => !token.IsCancellationRequested)
                    .Where(pi =>
                        pi.Gender == refNewInst.Gender &&
                        pi.EffectiveTraitsHash == refNewInst.EffectiveTraitsHash
                    )
                .ToList();

                var refInst = existingInstances.FirstOrDefault();

                if (refInst != null)
                {
                    var newSelection = PruningFunc(existingInstances.Concat(newInstances));

                    var added = newInstances.Intersect(newSelection);
                    var removed = existingInstances.Intersect(newSelection);

                    if (added.Any())
                    {
                        toAdd.AddRange(added);
                        changed = true;
                    }

                    if (removed.Any())
                    {
                        content[refNewInst.Pal.Id].RemoveAll(removed.Contains);
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
            remainingWork.EnsureCapacity(toAdd.Count * toAdd.Count + 2 * toAdd.Count * content.Count);

            remainingWork.AddRange(content.Values.SelectMany(l => l)
                // need to check results between new and old content
                .SelectMany(p1 => toAdd.Select(p2 => (p1, p2)))
                // and check results within the new content
                .Concat(toAdd.SelectMany(p1 => toAdd.Select(p2 => (p1, p2))))
            );

            foreach (var ta in toAdd)
            {
                if (!content.ContainsKey(ta.Pal.Id)) content.Add(ta.Pal.Id, new List<IPalReference>());

                content[ta.Pal.Id].Add(ta);
            }

            return changed;
        }

        // gives a new, reduced collection which only includes the "most optimal" / lowest-effort
        // reference for each instance spec (gender, traits, etc.)
        private IEnumerable<IPalReference> PruneCollection(IEnumerable<IPalReference> refs) =>
            refs
                .TakeWhile(_ => !token.IsCancellationRequested)
                .GroupBy(pref => (
                    pref.Pal,
                    pref.Gender,
                    pref.EffectiveTraitsHash
                ))
                .SelectMany(g => PruningFunc(g.Distinct()));
    }
}
