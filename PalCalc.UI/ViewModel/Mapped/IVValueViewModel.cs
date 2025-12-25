using PalCalc.Solver;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.ViewModel.Mapped
{
    public record class IVSetViewModel(IVValueViewModel HP, IVValueViewModel Attack, IVValueViewModel Defense)
    {
        public static IVSetViewModel FromIVs(IV_Set ivs) =>
            new IVSetViewModel(
                HP: IVValueViewModel.FromIV(ivs.HP),
                Attack: IVValueViewModel.FromIV(ivs.Attack),
                Defense: IVValueViewModel.FromIV(ivs.Defense)
            );
    }

    public interface IVValueViewModel : IComparable
    {
        /* Properties */
        string Label { get; }

        bool IsRelevant { get; }

        /* Utils */
        public static IVValueViewModel FromIV(IV_Value value)
        {
            if (value == IV_Value.Random) return IVAnyValueViewModel.Instance;

            if (value.Min == value.Max) return new IVDirectValueViewModel(value.IsRelevant, value.Min);
            else return new IVRangeValueViewModel(value.IsRelevant, value.Min, value.Max);
        }

        int IComparable.CompareTo(object obj)
        {
            int ValueOf(object iv) =>
                iv switch
                {
                    IVDirectValueViewModel d => d.Value,
                    IVRangeValueViewModel r => r.Max,
                    IVAnyValueViewModel => 0,
                    _ => 0
                };

            return ValueOf(obj) - ValueOf(this);
        }
    }

    public class IVDirectValueViewModel(bool isRelevant, int value) : IVValueViewModel
    {
        public int Value => value;
        public bool IsRelevant => isRelevant;
        public string Label { get; } = value.ToString();
    }

    public class IVRangeValueViewModel(bool isRelevant, int min, int max) : IVValueViewModel
    {
        public bool IsRelevant => isRelevant;
        public int Min => min;
        public int Max => max;

        public string Label { get; } = $"{min}-{max}";

        int IComparable.CompareTo(object obj)
        {
            return obj switch
            {
                IVAnyValueViewModel => -1,
                IVDirectValueViewModel d => d.Value - max,
                IVRangeValueViewModel r => r.Max == Max ? r.Min - min : r.Max - max,
                _ => 0
            };
        }
    }

    public class IVAnyValueViewModel : IVValueViewModel
    {
        private IVAnyValueViewModel() { }

        public static IVAnyValueViewModel Instance { get; } = new();

        public bool IsRelevant { get; } = false;

        public string Label { get; } = "-";
    }
}
