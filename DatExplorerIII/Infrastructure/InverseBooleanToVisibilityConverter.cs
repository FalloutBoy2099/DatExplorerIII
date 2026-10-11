namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// Shows an element when the value is false and hides it when it is true.
	/// </summary>
	public sealed class InverseBooleanToVisibilityConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Boolean flag && flag ? Visibility.Collapsed : Visibility.Visible;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}