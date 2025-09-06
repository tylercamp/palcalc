using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    // main default pruning
    public class MinimumEffortPruning : IResultPruning
    {
        public MinimumEffortPruning(CancellationToken token) : base(token)
        {
        }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            MinGroupOf(results, r => r.BreedingEffort);
    }
}
