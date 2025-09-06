using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    // main default pruning
    public class MinimumEffortPruning : IResultPruning.ForceDeterministic
    {
        public MinimumEffortPruning(CancellationToken token) : base(token)
        {
        }

        private static TimeSpan BreedingEffort(IPalReference r) => r.BreedingEffort;

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, BreedingEffort);
    }
}
