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

        bool IsValid { get; }
    }

    public class IV_Random : IV_IValue
    {
        private IV_Random() { }

        public bool IsValid => false;

        public bool Satisfies(int minValue) => false;
        public bool BetterThan(IV_IValue other) => false;

        private static int _hash = typeof(IV_Random).GetHashCode();
        public override bool Equals(object obj) => obj is IV_Random;
        public override int GetHashCode() => _hash;

        public static readonly IV_Random Instance = new();

        public override string ToString() => "(Random IV)";
    }

    public class IV_Range : IV_IValue
    {
        public IV_Range(int a, int b)
        {
            Min = Math.Min(a, b);
            Max = Math.Max(a, b);
        }

        public IV_Range(int value)
        {
            Min = value;
            Max = value;
        }

        public IV_Range(IV_Range other)
        {
            Min = other.Min;
            Max = other.Max;
        }

        public bool IsValid => true;

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

        public int Min { get; private set; }
        public int Max { get; private set; }

        public static IV_Range Merge(params IV_Range[] ranges)
        {
            var res = new IV_Range(ranges.First());
            foreach (var range in ranges.Skip(1))
            {
                res.Min = Math.Min(range.Min, res.Min);
                res.Max = Math.Max(range.Max, res.Max);
            }
            return res;
        }

        private static int _baseHash = typeof(IV_Range).GetHashCode();
        public override int GetHashCode() => HashCode.Combine(Min, Max, _baseHash);

        public override bool Equals(object obj) => obj is IV_Random && obj.GetHashCode() == GetHashCode();

        public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
    }
}
