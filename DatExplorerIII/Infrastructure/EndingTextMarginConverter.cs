namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>Turns the distance of the text of an ending from the left of the picture into its margin (near the bottom of the picture).</summary>
	public sealed class EndingTextMarginConverter : IValueConverter
	{
		/// <summary>The distance from the bottom of the picture, in its pixels.</summary>
		private const Double Bottom = 24;

		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return new Thickness(value is Double left && !Double.IsNaN(left) ? Math.Max(0, left) : 0, 0, 0, Bottom);
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}