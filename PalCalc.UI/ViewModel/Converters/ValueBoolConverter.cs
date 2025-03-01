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
    internal class ValueBoolConverter : IValueConverter
    {
        public bool Negate { get; set; } = false;

        private bool Evaluate(object value)
        {
            if (value == null) return false;
            else if (value is bool v) return v;
            else if (value is int i) return i != 0;
            else return true;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var res = Evaluate(value);
            if (Negate) res = !res;
            return res;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
