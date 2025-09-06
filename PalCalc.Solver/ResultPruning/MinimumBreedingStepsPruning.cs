using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumBreedingStepsPruning : IResultPruning
    {
        public MinimumBreedingStepsPruning(CancellationToken token) : base(token)
        {
        }

        private static int NumBreedingSteps(IPalReference r) => r.NumTotalBreedingSteps;

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, NumBreedingSteps);
    }
}
