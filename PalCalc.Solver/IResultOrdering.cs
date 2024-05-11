using Newtonsoft.Json.Bson;
using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public abstract class IResultOrdering
    {
        public abstract IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results);

        protected IEnumerable<IPalReference> CollectAll(IPalReference startRef)
        {
            yield return startRef;

            switch (startRef)
            {
                case BredPalReference bpr:
                    foreach (var r in CollectAll(bpr.Parent1)) yield return r;
                    foreach (var r in CollectAll(bpr.Parent2)) yield return r;
                    break;
            }
        }
    }

    /// <summary>
    /// if the same pal is used for multiple steps it may become a bottleneck
    /// </summary>
    public class MinimumReuseOrdering : IResultOrdering
    {
        public override IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results) => results.ThenBy(currentResult =>
        {
            var observed = CollectAll(currentResult).ToList();
            return observed.Count - observed.Distinct().Count();
        });
    }

    public class PreferredLocationOrdering : IResultOrdering
    {
        public override IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results) => results.ThenBy(currentResult =>
        {
            var countsByLocationType = new Dictionary<LocationType, int>
            {
                { LocationType.Palbox, 0 },
                { LocationType.Base, 0 },
                { LocationType.PlayerParty, 0 },
            };

            foreach (var pref in CollectAll(currentResult))
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

            // TODO - need to keep this ordering synced with the owned pal filtering done in BreedingSolver prep
            return (
                countsByLocationType[LocationType.Palbox] * 0 +
                countsByLocationType[LocationType.Base] * 100 +
                countsByLocationType[LocationType.PlayerParty] * 10000
            );
        });
    }

    // cases where multiple pals for the same input are available
    //public class ManyAlternativesOrdering : IResultOrdering { }

    // (acts like a "minimum breeding steps" ordering)
    public class MinimumInputsOrdering : IResultOrdering
    {
        public override IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results) => results.ThenBy(r => CollectAll(r).Distinct().Count());
    }

    public class MinimumWildPalsOrdering : IResultOrdering
    {
        public override IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results) => results.ThenBy(r => CollectAll(r).Count(p => p is BredPalReference));
    }

    public class MinimumReferencedPlayersOrdering : IResultOrdering
    {
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

        public override IOrderedEnumerable<IPalReference> Apply(IOrderedEnumerable<IPalReference> results) => results.ThenBy(r => CollectAll(r).SelectMany(PlayerIdsOf).Distinct().Count());
    }
}
