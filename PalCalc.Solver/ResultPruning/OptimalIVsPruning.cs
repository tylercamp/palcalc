﻿using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    /// <param name="maxIvDifference">
    /// Given a pal with the highest IVs, other pals will only be kept if their IVs differ by at most this much.
    /// </param>
    public class OptimalIVsPruning(CancellationToken token, int maxIvDifference) : IResultPruning(token)
    {

        static int ValueOf(IV_IValue value, int fallback, Func<IV_Range, int> map) =>
            value switch
            {
                IV_Random => fallback,
                IV_Range range => map(range),
                _ => throw new NotImplementedException()
            };

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results)
        {
            // note: all pals within a group being pruned should:
            //
            // - all have the same `IsRelevant` for each type of IV
            //   e.g. all HP will be relevant or all HP will be irrelevant
            //
            //   (would be enforced by grouping with `WorkingSet.DefaultGroupFn`)
            //
            // - if an IV range is relevant, all its min/max values will also be relevant
            //
            //   (would be enforced by applying min-IV filter on input pals (done in
            //   `BreedingSolver`), so the only IVs included will be relevant IVs)
            //

            // current impl just compares the maximum part of the IV range. filtering by min/avg
            // IVs doesn't affect whether we get a relevant result (see above), so we'll instead
            // try to maximize the highest possible value

            int SelectValue(IV_IValue value) =>
                ValueOf(value, 0, r => r.Max);

            int TotalIVs(IV_Set ivs) =>
                SelectValue(ivs.HP) + SelectValue(ivs.Attack) + SelectValue(ivs.Defense);

            if (token.IsCancellationRequested) return results;

            // could multiply maxIvDifference by the number of relevant IV types, but this
            // just further prunes results, and I'd prefer to give later pruning steps an
            // opportunity to apply their pruning
            var bestValue = results.Max(r => TotalIVs(r.IVs));
            var threshold = bestValue - maxIvDifference * 3;

            return results.Where(p => TotalIVs(p.IVs) >= threshold);
        }
    }
}
