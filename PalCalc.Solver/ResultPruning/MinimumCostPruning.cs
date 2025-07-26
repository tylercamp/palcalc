using PalCalc.Solver.PalReference;
using System.Collections.Generic;

namespace PalCalc.Solver.ResultPruning
{
    public class MinimumCostPruning : IResultPruning.ForceDeterministic
    {
        public MinimumCostPruning(CancellationToken token) : base(token) { }

        private static int TotalCost(IPalReference r) => r.TotalCost;

        protected override IEnumerable<IPalReference> ApplyNonDeterministic(IEnumerable<IPalReference> results) =>
            MinGroupOf(results, TotalCost);
    }
}