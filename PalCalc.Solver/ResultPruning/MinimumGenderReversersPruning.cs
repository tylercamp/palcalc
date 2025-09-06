using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumGenderReversersPruning : IResultPruning
    {
        public MinimumGenderReversersPruning(CancellationToken token) : base(token) { }

        private static int NumGenderReversers(IPalReference r) => r.NumTotalGenderReversers;

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, NumGenderReversers);
    }
}
