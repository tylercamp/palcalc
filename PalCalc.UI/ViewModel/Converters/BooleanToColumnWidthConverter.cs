using System;
using System.Globalization;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Converters
{
    internal class BooleanToColumnWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value is bool b && b;
            if (!visible) return 0.0;

            if (parameter is double d) return d;
            if (double.TryParse(parameter as string, out double parsed)) return parsed;

            return double.NaN;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
