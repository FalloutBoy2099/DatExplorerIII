namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;

	/// <summary>
	/// The top margin that makes the vertical scroll bar of a list start under the headers of its columns: the headers
	/// of a <see cref="GridView"/> are part of what the scroll viewer draws, so its scroll bar would run along them.
	/// The value is the view of the list; a list without columns has nothing to leave free. The parameter is the height of the
	/// headers when it is not the one of the theme.
	/// </summary>
	public sealed class GridViewHeaderOffsetConverter : IValueConverter
	{
		/// <summary>The height of the headers of the theme, in device independent pixels.</summary>
		public const Double HeaderHeight = 32;

		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			Double height = parameter is String text && Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out Double given) ? given : HeaderHeight;
			return value is GridView ? new Thickness(0, height, 0, 0) : new Thickness(0);
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}