using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumWildPalsPruning : IResultPruning
    {
        public MinimumWildPalsPruning(CancellationToken token) : base(token)
        {
        }

        private static int CountWildPals(IPalReference r, CachedResultData cachedData)
        {
            int count = 0;
            foreach (var p in cachedData.InnerReferences[r])
            {
                if (p is WildPalReference)
                    count++;
            }
            return count;
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, r => CountWildPals(r, cachedData));
    }
}
