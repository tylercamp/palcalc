using GraphSharp.Converters;
using System;
using System.Globalization;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Converters;

/// <summary>Connects edges to the main node body, excluding the level badge below it.</summary>
public sealed class BreedingEdgeRouteToPathConverter : IMultiValueConverter
{
    private readonly EdgeRouteToPathConverter converter = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var bodyValues = new object[9];
        Array.Copy(values, bodyValues, 9);
        AdjustBody(1, 3, 9);
        AdjustBody(5, 7, 10);
        return converter.Convert(bodyValues, targetType, parameter, culture);

        void AdjustBody(int yIndex, int heightIndex, int bodyIndex)
        {
            if (values[bodyIndex] is double bodyHeight && bodyHeight > 0 &&
                values[heightIndex] is double fullHeight && values[yIndex] is double centerY)
            {
                bodyValues[yIndex] = centerY - (fullHeight - bodyHeight) / 2;
                bodyValues[heightIndex] = bodyHeight;
            }
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
