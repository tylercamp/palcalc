using PalCalc.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public interface IVValueViewModel : IComparable
    {
        /* Properties */
        string Label { get; }

        /* Utils */
        public static IVValueViewModel FromIV(IV_IValue value)
        {
            switch (value)
            {
                case IV_Random: return IVAnyValueViewModel.Instance;
                case IV_Range range:
                    if (range.Min == range.Max) return new IVDirectValueViewModel(range.Min);
                    else return new IVRangeValueViewModel(range.Min, range.Max);

                default:
                    throw new NotImplementedException();
            }
        }

        int IComparable.CompareTo(object obj)
        {
            int ValueOf(object iv) =>
                iv switch
                {
                    IVDirectValueViewModel d => d.Value,
                    IVRangeValueViewModel r => (r.Min + r.Max) / 2,
                    IVAnyValueViewModel => 0,
                    _ => 0
                };

            return ValueOf(obj) - ValueOf(this);
        }
    }

    public class IVDirectValueViewModel(int value) : IVValueViewModel
    {
        public int Value => value;
        public string Label { get; } = value.ToString();
    }

    public class IVRangeValueViewModel(int min, int max) : IVValueViewModel
    {
        public int Min => min;
        public int Max => max;

        public string Label { get; } = $"{min}-{max}";
    }

    public class IVAnyValueViewModel : IVValueViewModel
    {
        private IVAnyValueViewModel() { }

        public static IVAnyValueViewModel Instance { get; } = new();

        public string Label { get; } = "-";
    }
}
