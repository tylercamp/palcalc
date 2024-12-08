using PalCalc.Solver.PalReference;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.ResultPruning
{
    /// <summary>
    /// TODO
    /// </summary>
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

        static int ReduceIVs(IPalReference pref, int fallback, Func<IV_Range, int> map, Func<IEnumerable<int>, int> reduce) =>
            reduce((int[])[
                ValueOf(pref.IV_HP, fallback, map),
                ValueOf(pref.IV_Attack, fallback, map),
                ValueOf(pref.IV_Defense, fallback, map),
            ]);

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results)
        {
            // TODO - would min+max average be better?
            int SelectValue(IV_IValue value) =>
                ValueOf(value, 0, r => r.Max);

            int TotalIVs(IPalReference p) =>
                SelectValue(p.IV_HP) + SelectValue(p.IV_Attack) + SelectValue(p.IV_Defense);

            if (token.IsCancellationRequested) return results;

            var bestValue = results.Max(TotalIVs);
            var threshold = bestValue - maxIvDifference * 3;

            return results.Where(p => TotalIVs(p) >= threshold).ToList();
        }
    }
}
