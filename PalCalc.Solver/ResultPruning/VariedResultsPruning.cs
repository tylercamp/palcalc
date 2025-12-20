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

        // some static helpers for selecting data; this code is all in a hot-path, using static methods avoids allocating
        // temporary, scoped arrow-functions
        private static T Identity<T>(T value) => value;
        private static K GroupKeyOf<K, V>(IGrouping<K, V> g) => g.Key;
        private static K KvpKey<K, V>(KeyValuePair<K, V> kvp) => kvp.Key;
        private static V KvpValue<K, V>(KeyValuePair<K, V> kvp) => kvp.Value;

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData)
        {
            var palOccurrences = results.ToDictionary(Identity, r =>
            {
                return cachedData.InnerReferences[r].GroupBy(ir => ir.Pal).ToDictionary(GroupKeyOf, g => g.Count());
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
            foreach (var pal in totalPalOccurrences.OrderByDescending(KvpValue).Select(KvpKey))
            {
                if (token.IsCancellationRequested) return Empty;

                var nextCommonResults = commonResults.GroupBy(r => palOccurrences[r].GetValueOrElse(pal, 0)).OrderByDescending(GroupKeyOf).SelectMany(Identity).ToList();
                if (nextCommonResults.Count > 0)
                    commonResults = nextCommonResults;

                if (commonResults.Count == 1) break;
            }

            if (commonResults.Count == 0)
                return [];

            var prunedResults = new List<IPalReference>() { commonResults.First() };
            foreach (var currentResult in results.TakeUntilCancelled(token))
            {
                if (token.IsCancellationRequested) return Empty;

                if (prunedResults.Contains(currentResult)) continue;

                var resultOccurrences = palOccurrences[currentResult];
                var resultTotalPals = resultOccurrences.Sum(KvpValue);

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
