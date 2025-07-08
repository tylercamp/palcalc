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

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(results, r => r.NumTotalGenderReversers);
    }
}
