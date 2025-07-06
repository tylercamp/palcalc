using PalCalc.Solver.PalReference;
using System.Collections.Generic;

namespace PalCalc.Solver.ResultPruning
{
    /// <summary>
    /// Prunes results keeping only the lowest-cost pal(s) in each equivalence group,
    /// where cost is measured by <see cref="IPalReference.TotalCost"/>.
    /// </summary>
    public class MinimumCostPruning : IResultPruning
    {
        public MinimumCostPruning(CancellationToken token) : base(token) { }

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results) =>
            FirstGroupOf(
                input: FirstGroupOf(results, r => r.TotalCost),
                grouping: r => r.NumTotalSurgerySteps
            );
    }
}