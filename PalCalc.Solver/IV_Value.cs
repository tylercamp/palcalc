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
