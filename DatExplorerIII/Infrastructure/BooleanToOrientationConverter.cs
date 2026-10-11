namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows.Controls;
	using System.Windows.Data;

	/// <summary>True (one above the other) is a vertical arrangement, false (side by side) a horizontal one.</summary>
	public sealed class BooleanToOrientationConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Boolean stacked && stacked ? Orientation.Vertical : Orientation.Horizontal;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}