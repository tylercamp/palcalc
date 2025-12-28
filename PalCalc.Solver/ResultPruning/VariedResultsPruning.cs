using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    // avoid generating lots of very similar results
    public class VariedResultsPruning : IResultPruning.ForceDeterministic
    {
        float maxSimilarityPercent; // 0 - 1
        public VariedResultsPruning(CancellationToken token, float maxSimilarityPercent) : base(token)
        {
            this.maxSimilarityPercent = maxSimilarityPercent;
        }

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData)
        {
            var palOccurrences = results.ToDictionary(r => r, r =>
            {
                return cachedData.InnerReferences[r].GroupBy(ir => ir.Pal).ToDictionary(g => g.Key, g => g.Count());
            });

            var totalPalOccurrences = new Dictionary<Pal, int>();
            foreach (var (r, occ) in palOccurrences)
            {
                foreach (var kvp in occ)
                {
                    if (!totalPalOccurrences.ContainsKey(kvp.Key)) totalPalOccurrences[kvp.Key] = 0;
                    totalPalOccurrences[kvp.Key] += kvp.Value;
                }
            }

            // find the result which has the most common set of shared pals
            var commonResults = results.ToList();
            // (for each observed pal in the results, ordered by which pal is used the most, find the results which have the most references to that pal)
            foreach (var pal in totalPalOccurrences.OrderByDescending(kvp => kvp.Value).Select(kvp => kvp.Key))
            {
                if (token.IsCancellationRequested) return [];

                var nextCommonResults = commonResults.GroupBy(r => palOccurrences[r].GetValueOrElse(pal, 0)).OrderByDescending(g => g.Key).SelectMany(g => g).ToList();
                if (nextCommonResults.Count > 0)
                    commonResults = nextCommonResults;

                if (commonResults.Count == 1) break;
            }

            if (commonResults.Count == 0)
                return [];

            var prunedResults = new List<IPalReference>() { commonResults.First() };
            foreach (var currentResult in results)
            {
                if (token.IsCancellationRequested) return [];

                if (prunedResults.Contains(currentResult)) continue;

                var resultOccurrences = palOccurrences[currentResult];
                var resultTotalPals = resultOccurrences.Sum(kvp => kvp.Value);

                // TODO - this checks for similarity of `currentResult` contents vs `prunedResult` contents, but maybe it's better
                //        to do the opposite?
                var lowestDifferenceScore = prunedResults.Min(prunedResult =>
                {
                    var differenceCount = 0;
                    foreach (var p in resultOccurrences.Keys)
                    {
                        differenceCount += Math.Abs(resultOccurrences[p] - palOccurrences[prunedResult].GetValueOrElse(p, 0));
                    }
                    return differenceCount / (float)resultTotalPals;
                });

                var highestSimilarityScore = 1 - lowestDifferenceScore;
                if (highestSimilarityScore < maxSimilarityPercent)
                    prunedResults.Add(currentResult);
            }

            return prunedResults;
        }
    }
}
