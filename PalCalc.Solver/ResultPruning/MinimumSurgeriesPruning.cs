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

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r => r.NumTotalSurgerySteps);
    }
}
