using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface IV_IValue
    {
        bool Satisfies(int minValue);
        bool BetterThan(IV_IValue other);

        bool IsRelevant { get; }
    }

    public record class IV_Random : IV_IValue
    {
        private IV_Random() { }

        public bool IsRelevant => false;

        public bool Satisfies(int minValue) => false;
        public bool BetterThan(IV_IValue other) => false;

        private static int _hash = nameof(IV_Random).GetHashCode();
        public override int GetHashCode() => _hash;

        public static readonly IV_Random Instance = new();

        public override string ToString() => "(Random IV)";
    }

    public readonly record struct IV_Range(bool IsRelevant, int Min, int Max) : IV_IValue
    {
        public IV_Range(bool isRelevant, int value) : this(isRelevant, value, value)
        {
        }

        public bool Satisfies(int minValue) => Min >= minValue;

        public bool BetterThan(IV_IValue other)
        {
            switch (other)
            {
                case IV_Random: return true;
                case IV_Range ivr:
                    if (Max > ivr.Max) return true;
                    else if (Max < ivr.Max) return false;
                    else return Min > ivr.Min;

                default: throw new NotImplementedException();
            }
        }

        public static IV_Range Merge(params IV_Range[] ranges)
        {
            var first = ranges[0];

            int xmin = first.Min;
            int xmax = first.Max;
            bool isRelevant = first.IsRelevant;

            for (int i = 1; i < ranges.Length; i++)
            {
                var range = ranges[i];
                xmin = Math.Min(range.Min, xmin);
                xmax = Math.Max(range.Max, xmax);
            }

            return new IV_Range(isRelevant, xmin, xmax);
        }

        public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
    }
}
