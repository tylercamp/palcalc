using PalCalc.UI.ViewModel.Mapped;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Converters
{
    internal class IVRelevanceToOpacityConverter : IValueConverter
    {
        private static double IrrelevantOpacity = 0.7;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return IrrelevantOpacity;
            if (value is IVValueViewModel v && !v.IsRelevant) return IrrelevantOpacity;
            else return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
