namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// Places a mark at a fraction (0 to 1) of the width of the space it moves in: the values are the fraction, the width of the
	/// space and the width of the mark; the result is the left margin of the mark.
	/// </summary>
	public sealed class FractionOfWidthConverter : IMultiValueConverter
	{
		public Object Convert(Object[] values, Type targetType, Object parameter, CultureInfo culture)
		{
			if (values.Length < 3 || values[0] is not Double fraction || values[1] is not Double width || values[2] is not Double markWidth || Double.IsNaN(width) || Double.IsNaN(markWidth))
			{
				return new Thickness(0);
			}

			return new Thickness(Math.Max(0, (width - markWidth) * Math.Clamp(fraction, 0, 1)), 0, 0, 0);
		}

		public Object[] ConvertBack(Object value, Type[] targetTypes, Object parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}