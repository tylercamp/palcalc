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

        private static int NumReferencedPlayers(IPalReference r, CachedResultData cachedData)
        {
            List<string> playerIds = [];

            foreach (var p in cachedData.InnerReferences[r])
            {
                switch (p)
                {
                    case OwnedPalReference opr:
                        var pid = opr.UnderlyingInstance.OwnerPlayerId;
                        if (!playerIds.Contains(pid))
                            playerIds.Add(pid);
                        break;

                    case CompositeOwnedPalReference copr:
                        // TODO - this will end up avoiding results which use composite refs; construction of composites
                        //        has no way to know which player to "prefer" for selection
                        var mpid = copr.Male.UnderlyingInstance.OwnerPlayerId;
                        if (!playerIds.Contains(mpid))
                            playerIds.Add(mpid);

                        var fpid = copr.Female.UnderlyingInstance.OwnerPlayerId;
                        if (!playerIds.Contains(fpid))
                            playerIds.Add(fpid);

                        break;
                }
            }

            return playerIds.Count;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, r => NumReferencedPlayers(r, cachedData));
    }
}
