using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows;

namespace GraphSharp.Converters
{
	public class PointArrayToString : IValueConverter
	{
		#region IValueConverter Members

	    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var points = value as Point[];

			if ( points == null )
				return string.Empty;
			var sb = new StringBuilder();
			foreach ( var point in points )
				sb.AppendLine( point.ToString() );
			return sb;
		}

	    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

		#endregion
	}
}