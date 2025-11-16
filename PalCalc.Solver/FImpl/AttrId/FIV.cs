using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.FImpl.AttrId
{
    // (note: use of `short` only works so long as IVs can't exceed 100)
    // first 2 bits are Type, next 7 are Range-Min, last 7 are Range-Max/Exact Value
    public readonly record struct FIV(ushort Store)
    {
        private const ushort IS_RELEVANT = 0x8000;
        public static readonly FIV Random = new(0);

        public FIV(IV_IValue v) : this(v.IsRelevant, v.Min, v.Max)
        {
        }

        public FIV(bool isRelevant, int value) : this(isRelevant, value, value)
        {
        }

        public FIV(bool isRelevant, int minValue, int maxValue)
            : this((ushort)(
                (isRelevant ? 0x8000 : 0)
                  | ((minValue & 0x7F) << 7)
                  | (maxValue & 0x7F)
            ))
        {
        }

        // Creates a new IV which includes the relevant values from a and b
        public static FIV Merge(FIV a, FIV b)
        {
            if ((a.Store & IS_RELEVANT) == (b.Store & IS_RELEVANT))
            {
                // they're both relevant or both irrelevant, give a new IV range
                // which captures both
                return new(a.IsRelevant, Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max));
            }
            else if (a.IsRelevant)
            {
                return a;
            }
            else
            {
                return b;
            }
        }

        public bool IsRandom => Store == 0;
        public bool IsRelevant => (Store & 0x8000) != 0;
        public int Max => Store & 0x7F;
        public int Min => (Store >> 7) & 0x7F;

        public bool Satisfies(int minValue) =>
            !IsRandom && Min >= minValue;

        public IV_IValue ModelObject
        {
            get
            {
                if (this == Random) return IV_Random.Instance;

                return new IV_Range(IsRelevant, Min, Max);
            }
        }
    }
}
