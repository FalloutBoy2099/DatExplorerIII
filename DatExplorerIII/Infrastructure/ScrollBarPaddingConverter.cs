namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// Turns the width of the scroll bars into the padding of a text box. The theme draws the scroll bars over the text (the content
	/// of its scroll viewer fills the whole box): the padding keeps the text clear of them, whatever width the user chose. The
	/// parameter is the padding without the bars, "left,top,right,bottom", then after a bar "r" (add the width to the right, for the
	/// vertical bar) and/or "b" (add it at the bottom, for the horizontal bar): "10,5,6,6|rb".
	/// </summary>
	public sealed class ScrollBarPaddingConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			String[] parts = (parameter as String ?? "0,0,0,0").Split('|');
			String[] numbers = parts[0].Split(',');
			Double width = value is Double bar && !Double.IsNaN(bar) ? Math.Max(0, bar) : 0;
			String bars = parts.Length > 1 ? parts[1] : String.Empty;
			Double Number(Int32 index) => index < numbers.Length && Double.TryParse(numbers[index], NumberStyles.Float, CultureInfo.InvariantCulture, out Double number) ? number : 0;
			return new Thickness(Number(0), Number(1), Number(2) + (bars.Contains('r', StringComparison.Ordinal) ? width : 0), Number(3) + (bars.Contains('b', StringComparison.Ordinal) ? width : 0));
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}