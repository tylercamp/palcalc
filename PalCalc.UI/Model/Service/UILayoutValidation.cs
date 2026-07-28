using System;
using System.Collections.Generic;
using System.Linq;

namespace PalCalc.UI.Model.Service
{
    internal readonly record struct GridDefinitionConstraints(
        double Minimum,
        double Maximum,
        double AutoSize
    );

    internal static class UILayoutValidation
    {
        private const long MaximumSaneWindowDimension = 100_000;
        private const double MaximumSaneGridValue = 100_000;

        public static bool IsValidWindowPlacement(WindowPlacementSettings placement, int expectedVersion)
        {
            if (placement == null || placement.Version != expectedVersion)
                return false;

            var width = (long)placement.Right - placement.Left;
            var height = (long)placement.Bottom - placement.Top;

            return width > 0 &&
                   height > 0 &&
                   width <= MaximumSaneWindowDimension &&
                   height <= MaximumSaneWindowDimension;
        }

        public static bool TryNormalizeGridLengths(
            IReadOnlyList<GridLengthSettings> saved,
            IReadOnlyList<GridDefinitionConstraints> constraints,
            double availableSize,
            int primaryIndex,
            double primaryMinimum,
            out IReadOnlyList<GridLengthSettings> normalized
        )
        {
            normalized = null;

            if (saved == null ||
                constraints == null ||
                saved.Count != constraints.Count ||
                !IsFiniteNonNegative(availableSize) ||
                !IsFiniteNonNegative(primaryMinimum) ||
                primaryIndex < -1 ||
                primaryIndex >= saved.Count)
            {
                return false;
            }

            if (saved.Count == 0)
            {
                normalized = [];
                return true;
            }

            var result = new List<GridLengthSettings>(saved.Count);
            var minimums = new double[saved.Count];
            var totalRequired = 0d;

            for (var i = 0; i < saved.Count; i++)
            {
                var item = saved[i];
                var constraint = constraints[i];
                var minimum = Math.Max(
                    constraint.Minimum,
                    i == primaryIndex ? primaryMinimum : 0
                );

                if (!IsFiniteNonNegative(minimum) ||
                    (!double.IsPositiveInfinity(constraint.Maximum) &&
                     (!IsFiniteNonNegative(constraint.Maximum) ||
                      constraint.Maximum < minimum)) ||
                    !IsFiniteNonNegative(constraint.AutoSize) ||
                    item == null ||
                    !Enum.IsDefined(item.Unit))
                {
                    return false;
                }

                minimums[i] = minimum;

                switch (item.Unit)
                {
                    case LayoutGridUnit.Auto:
                        result.Add(new GridLengthSettings
                        {
                            Unit = LayoutGridUnit.Auto,
                            Value = 1,
                        });
                        totalRequired += constraint.AutoSize;
                        break;

                    case LayoutGridUnit.Pixel:
                        if (!IsFiniteNonNegative(item.Value) ||
                            item.Value > MaximumSaneGridValue)
                            return false;

                        var pixelValue = Math.Clamp(item.Value, minimum, constraint.Maximum);
                        result.Add(new GridLengthSettings
                        {
                            Unit = LayoutGridUnit.Pixel,
                            Value = pixelValue,
                        });
                        totalRequired += pixelValue;
                        break;

                    case LayoutGridUnit.Star:
                        if (!double.IsFinite(item.Value) ||
                            item.Value <= 0 ||
                            item.Value > MaximumSaneGridValue)
                            return false;

                        result.Add(new GridLengthSettings
                        {
                            Unit = LayoutGridUnit.Star,
                            Value = item.Value,
                        });
                        totalRequired += minimum;
                        break;

                    default:
                        return false;
                }
            }

            var excess = totalRequired - availableSize;
            if (excess > 0)
            {
                var reduciblePixels = result
                    .Select((item, index) => (item, index))
                    .Where(entry => entry.item.Unit == LayoutGridUnit.Pixel)
                    .Sum(entry => entry.item.Value - minimums[entry.index]);

                if (reduciblePixels + 0.01 < excess)
                    return false;

                var reductionRatio = Math.Min(1, excess / reduciblePixels);
                for (var i = 0; i < result.Count; i++)
                {
                    if (result[i].Unit != LayoutGridUnit.Pixel)
                        continue;

                    var reducible = result[i].Value - minimums[i];
                    result[i].Value -= reducible * reductionRatio;
                }
            }

            normalized = result;
            return true;
        }

        private static bool IsFiniteNonNegative(double value) =>
            double.IsFinite(value) && value >= 0;
    }
}
