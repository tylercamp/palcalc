using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public readonly record struct IV_Value(bool IsRelevant, int Min, int Max)
    {
        public bool Satisfies(int minValue) => Min >= minValue;

        public static readonly IV_Value Random = new(false, 0, 0);

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
