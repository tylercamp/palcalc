using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
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
            MinGroupOf(results, r => r.AllReferences().SelectMany(PlayerIdsOf).Distinct().Count());
    }
}
