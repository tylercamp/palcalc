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

        public override IEnumerable<IPalReference> Apply(IEnumerable<IPalReference> results)
        {
            // TODO - would min+max average be better?
            int SelectValue(IV_IValue value) =>
                ValueOf(value, 0, r => r.Max);

            int TotalIVs(IV_Set ivs) =>
                SelectValue(ivs.HP) + SelectValue(ivs.Attack) + SelectValue(ivs.Defense);

            if (token.IsCancellationRequested) return results;

            var bestValue = results.Max(r => TotalIVs(r.IVs));
            var threshold = bestValue - maxIvDifference * 3;

            return results.Where(p => TotalIVs(p.IVs) >= threshold).ToList();
        }
    }
}
