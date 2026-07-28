using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.PalReference.Properties
{
    public readonly record struct IV_Value(bool IsRelevant, int Min, int Max)
    {
        public bool Satisfies(int minValue) => Min >= minValue;

        // Min/Max of -1 keeps this distinct from a known IV of 0, which is a real
        // value that must still be merged as a possible inherited result.
        public static readonly IV_Value Random = new(false, -1, -1);

        public static IV_Value Merge(IV_Value a, IV_Value b)
        {
            if (a == b) return a;
            else return new IV_Value(a.IsRelevant, Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max));
        }

        public override string ToString()
        {
            if (this == Random) return "(Random IV)";

            return Min == Max ? Min.ToString() : $"{Min}-{Max}";
        }
    }
}
