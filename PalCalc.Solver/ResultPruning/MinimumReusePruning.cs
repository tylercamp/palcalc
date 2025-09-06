using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    // if the same pal is used for multiple steps it may become a bottleneck
    public class MinimumReusePruning : IResultPruning
    {
        public MinimumReusePruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, r =>
            {
                var observed = cachedData.InnerReferences[r];
                return -(observed.Count - observed.Distinct().Count());
            });
    }
}
