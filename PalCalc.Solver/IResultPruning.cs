using Newtonsoft.Json.Bson;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public abstract class IResultPruning
    {
        protected CancellationToken token;
        public IResultPruning(CancellationToken token)
        {
            this.token = token;
        }

        public abstract IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results);

        protected static readonly IEnumerable<IPalReference> Empty = Enumerable.Empty<IPalReference>();
        protected IEnumerable<IPalReference> FirstGroupOf<T>(IEnumerable<IPalReference> input, Func<IPalReference, T> grouping)
        {
            try
            {
                if (token.IsCancellationRequested)
                    return Empty;

                var resultGroup = input.TakeWhile(_ => !token.IsCancellationRequested).GroupBy(grouping).OrderBy(g => g.Key).FirstOrDefault();
                if (token.IsCancellationRequested)
                    return Empty;

                return resultGroup.ToList();
            }
            catch (Exception)
            {
                if (token.IsCancellationRequested)
                    return input;
                else
                    throw;
            }
        }
    }

    // main default pruning
    public class MinimumEffortPruning : IResultPruning
    {
        public MinimumEffortPruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) => FirstGroupOf(results, r => r.BreedingEffort);
    }

    // if the same pal is used for multiple steps it may become a bottleneck
    public class MinimumReusePruning : IResultPruning
    {
        public MinimumReusePruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r =>
            {
                var observed = r.AllReferences().ToList();
                return -(observed.Count - observed.Distinct().Count());
            });
    }

    // choose results where the inputs are in preferred locations
    public class PreferredLocationPruning : IResultPruning
    {
        public PreferredLocationPruning(CancellationToken token) : base(token)
        {
        }

        // prefer pals in palbox, then in base, etc
        public static int LocationOrderingOf(LocationType type) => type switch
        {
            LocationType.Palbox => 0,
            LocationType.Base => 100,
            LocationType.PlayerParty => 10000,
            _ => throw new NotImplementedException()
        };

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r =>
            {
                var countsByLocationType = new Dictionary<LocationType, int>
                {
                    { LocationType.Palbox, 0 },
                    { LocationType.Base, 0 },
                    { LocationType.PlayerParty, 0 },
                };

                foreach (var pref in r.AllReferences())
                {
                    switch (pref.Location)
                    {
                        case OwnedRefLocation orl:
                            countsByLocationType[orl.Location.Type] += 1; break;

                        case CompositeRefLocation crl:
                            var maleLoc = crl.MaleLoc as OwnedRefLocation;
                            var femaleLoc = crl.FemaleLoc as OwnedRefLocation;

                            countsByLocationType[maleLoc.Location.Type] += 1;
                            if (maleLoc.Location.Type != femaleLoc.Location.Type)
                                countsByLocationType[femaleLoc.Location.Type] += 1;

                            break;
                    }
                }

                return (
                    countsByLocationType[LocationType.Palbox] * LocationOrderingOf(LocationType.Palbox) +
                    countsByLocationType[LocationType.Base] * LocationOrderingOf(LocationType.Base) +
                    countsByLocationType[LocationType.PlayerParty] * LocationOrderingOf(LocationType.PlayerParty)
                );
            });
    }

    // cases where multiple pals for the same input are available
    //public class ManyAlternativesOrdering : IResultOrdering { }

    public class MinimumBreedingStepsPruning : IResultPruning
    {
        public MinimumBreedingStepsPruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r =>
                r.AllReferences()
                    .Where(r => r is BredPalReference)
                    .Distinct()
                    .Count()
            );
    }

    public class MinimumWildPalsPruning : IResultPruning
    {
        public MinimumWildPalsPruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r => r.AllReferences().Count(p => p is WildPalReference));
    }

    // prefer options where we don't need to borrow pals from multiple players
    public class MinimumReferencedPlayersPruning : IResultPruning
    {
        public MinimumReferencedPlayersPruning(CancellationToken token) : base(token)
        {
        }

        private static IEnumerable<string> PlayerIdsOf(IPalReference pref)
        {
            switch (pref)
            {
                case OwnedPalReference opr:
                    yield return opr.UnderlyingInstance.OwnerPlayerId;
                    break;

                case CompositeOwnedPalReference copr:
                    // TODO - this will end up avoiding results which use composite refs; construction of composites
                    //        has no way to know which player to "prefer" for selection
                    yield return copr.Male.UnderlyingInstance.OwnerPlayerId;
                    yield return copr.Female.UnderlyingInstance.OwnerPlayerId;
                    break;
            }
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r => r.AllReferences().SelectMany(PlayerIdsOf).Distinct().Count());
    }

    // avoid generating lots of very similar results
    public class VariedResultsPruning : IResultPruning
    {
        float maxSimilarityPercent; // 0 - 1
        public VariedResultsPruning(CancellationToken token, float maxSimilarityPercent) : base(token)
        {
            this.maxSimilarityPercent = maxSimilarityPercent;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results)
        {
            var palOccurrences = results.ToDictionary(r => r, r =>
            {
                return r.AllReferences().GroupBy(ir => ir.Pal).ToDictionary(g => g.Key, g => g.Count());
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
                var nextCommonResults = commonResults.GroupBy(r => palOccurrences[r].GetValueOrElse(pal, 0)).OrderByDescending(g => g.Key).SelectMany(g => g).ToList();
                if (nextCommonResults.Count > 0)
                    commonResults = nextCommonResults;

                if (commonResults.Count == 1) break;
            }

            if (token.IsCancellationRequested) return Empty;

            var prunedResults = new List<IPalReference>() { commonResults.First() };
            foreach (var currentResult in results.TakeWhile(_ => !token.IsCancellationRequested))
            {
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

    public class ResultLimitPruning : IResultPruning
    {
        int maxResults;
        public ResultLimitPruning(CancellationToken token, int maxResults) : base(token)
        {
            this.maxResults = maxResults;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) => results.TakeWhile(_ => !token.IsCancellationRequested).Take(maxResults);
    }
}
