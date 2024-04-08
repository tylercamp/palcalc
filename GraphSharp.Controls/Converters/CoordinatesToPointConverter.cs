using System;
using System.Globalization;
using System.Windows.Data;
using System.Diagnostics;
using System.Windows;

namespace GraphSharp.Converters
{
	public class CoordinatesToPointConverter : IMultiValueConverter
	{
	    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			Debug.Assert( values != null && values.Length == 2, "CoordinatesToPointConverter.Convert should get 2 values as input: X and Y coordinates" );

			var x = (double)values[0];
			var y = (double)values[1];

			return new Point( x, y );
		}

	    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			Debug.Assert( !( value is Point ), "CoordinatesToPointConverter.ConvertBack should get a Point object as input." );

			var point = (Point)value;

			return new object[] { point.X, point.Y };
		}
	}
}