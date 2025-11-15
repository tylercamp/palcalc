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

        bool IsRelevant { get; }

        int Min { get; }
        int Max { get; }

        // (any equality between IV_IValues will always be reference
        // comparisons, despite IV_Range being a record struct, due to boxing)
        public static bool AreEqual(IV_IValue a, IV_IValue b)
        {
            var nullA = a is null;
            var nullB = b is null;

            if (nullA != nullB) return false;
            if (nullA && nullB) return true;

            var randA = ReferenceEquals(a, IV_Random.Instance);
            var randB = ReferenceEquals(b, IV_Random.Instance);

            if (randA != randB) return false;
            if (randA && randB) return true;

            var rangeA = (IV_Range)a;
            var rangeB = (IV_Range)b;

            return rangeA == rangeB;
        }
    }

    public record class IV_Random : IV_IValue
    {
        private IV_Random() { }

        public int Min => 0;
        public int Max => 0;

        public bool IsRelevant => false;

        public bool Satisfies(int minValue) => false;

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

        public static IV_Range Merge(IV_Range a, IV_Range b)
        {
            if (a == b) return a;
            else return new IV_Range(a.IsRelevant, Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max));
        }

        public override string ToString() => Min == Max ? Min.ToString() : $"{Min}-{Max}";
    }
}
