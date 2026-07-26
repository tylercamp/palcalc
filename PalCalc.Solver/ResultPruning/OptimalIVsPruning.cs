using PalCalc.Solver.PalReference;
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
    public class OptimalIVsPruning(CancellationToken token, int maxIvDifference) : ResultPruningRule(token)
    {
        static int ValueOf(IV_Value value, int fallback, Func<IV_Value, int> map) =>
            value == IV_Value.Random ? fallback : map(value);

        static int SelectValue(IV_Value value) =>
            ValueOf(value, 0, r => r.Max);

        static int TotalIVs(IV_Set ivs) =>
            SelectValue(ivs.HP) + SelectValue(ivs.Attack) + SelectValue(ivs.Defense);

        static int TotalIVs(IPalReference r) => TotalIVs(r.IVs);

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results, CachedResultData cachedData)
        {
            // Candidates in this group are expected to:
            //
            // - all have the same `IsRelevant` for each type of IV
            //   e.g. all HP will be relevant or all HP will be irrelevant
            //
            //   (enforced by `EffectivePropertiesKey`)
            //
            // - have relevant IV ranges whose minimum and maximum both satisfy
            //   the target threshold.
            //
            // `InitialPalBuilder` establishes the second invariant for owned
            // Pals. Wild IVs are irrelevant, breeding preserves the invariant
            // when it merges ranges, and surgery leaves IVs unchanged.
            //

            // current impl just compares the maximum part of the IV range. filtering by min/avg
            // IVs doesn't affect whether we get a relevant result (see above), so we'll instead
            // try to maximize the highest possible value.

            if (token.IsCancellationRequested) return results;

            // could multiply maxIvDifference by the number of relevant IV types, but this
            // just further prunes results, and I'd prefer to give later pruning steps an
            // opportunity to apply their pruning
            var bestValue = results.Max(TotalIVs);
            var threshold = bestValue - maxIvDifference * 3;

            return results.Where(p => TotalIVs(p.IVs) >= threshold);
        }
    }
}
