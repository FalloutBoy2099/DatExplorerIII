namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// Turns a width into a margin on the right of that width: what the scroll bar covers (it is drawn over the content) is left
	/// free, whatever width the user chose for the scroll bars.
	/// </summary>
	public sealed class RightMarginConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is Double width && !Double.IsNaN(width) ? new Thickness(0, 0, Math.Max(0, width), 0) : new Thickness(0);
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}