using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumSurgeriesPruning : IResultPruning.ForceDeterministic
    {
        public MinimumSurgeriesPruning(CancellationToken token) : base(token) { }

        int NumSurgeries(IPalReference r) => r.NumTotalSurgerySteps;

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, NumSurgeries);
    }
}
