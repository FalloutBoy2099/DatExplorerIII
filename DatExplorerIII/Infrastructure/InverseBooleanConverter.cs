namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows.Data;

	/// <summary>
	/// Turns true into false and false into true.
	/// </summary>
	public sealed class InverseBooleanConverter : IValueConverter
	{
		public Object Convert(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Boolean flag ? !flag : value;
		}

		public Object ConvertBack(Object value, Type targetType, Object parameter, CultureInfo culture)
		{
			return value is Boolean flag ? !flag : value;
		}
	}
}