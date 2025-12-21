using PalCalc.Solver.PalReference;
using System.Collections.Generic;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumCostPruning : IResultPruning.ForceDeterministic
    {
        public MinimumCostPruning(CancellationToken token) : base(token) { }

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results, CachedResultData cachedData) =>
            MinGroupOf(results, r => r.TotalCost);
    }
}