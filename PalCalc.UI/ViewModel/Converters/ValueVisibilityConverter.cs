using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Converters
{
    internal class ValueVisibilityConverter : IValueConverter
    {
        public bool Negate { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool shouldShow;

            if (value == null) shouldShow = false;
            else if (value is bool v) shouldShow = v;
            else if (value is int i) shouldShow = i != 0;
            else shouldShow = true;

            if (Negate) shouldShow = !shouldShow;

            return shouldShow ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
