using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PalCalc.UI.ViewModel.Converters
{
    public class CommaSeparatedListConverter : IValueConverter
    {
        public string Prefix { get; set; } = "";
        public string Suffix { get; set; } = "";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Join(
                 ", ",
                 (value as IEnumerable)
                    .Cast<string>()
                    .Select(v => Prefix.Replace("\\", "") + v + Suffix.Replace("\\", ""))
            );
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
