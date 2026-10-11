namespace DatExplorerIII.Services
{
	using System;
	using System.ComponentModel;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>
	/// The size of the scroll bars: their width, and the smallest length of their slider (which the theme lets get tiny in a long
	/// list). The scroll bars of the program read the width from the resources of the application and the smallest slider from
	/// <see cref="Current"/>, so a change shows at once.
	/// </summary>
	public sealed class ScrollBarMetrics : INotifyPropertyChanged
	{
		public const Int32 DefaultWidth = 16;
		public const Int32 DefaultMinimumThumb = 40;
		public const Int32 MinimumWidth = 10;
		public const Int32 MaximumWidth = 32;
		public const Int32 MinimumThumbLength = 20;
		public const Int32 MaximumThumbLength = 160;

		private Double _minimumThumb = DefaultMinimumThumb;

		/// <summary>The sizes the scroll bars use.</summary>
		public static ScrollBarMetrics Current { get; } = new();

		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>The smallest length of the slider, in device independent pixels.</summary>
		public Double MinimumThumb
		{
			get => _minimumThumb;
			private set
			{
				if (_minimumThumb != value)
				{
					_minimumThumb = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinimumThumb)));
				}
			}
		}

		/// <summary>Keeps a number between the limits.</summary>
		public static Int32 Clamp(Int32 value, Int32 minimum, Int32 maximum) => Math.Clamp(value, minimum, maximum);

		/// <summary>Gives the scroll bars of the program this width and this smallest slider (in device independent pixels).</summary>
		public static void Apply(Int32 width, Int32 minimumThumb)
		{
			Current.MinimumThumb = Clamp(minimumThumb, MinimumThumbLength, MaximumThumbLength);
			if (Application.Current is { } application)
			{
				application.Resources["ScrollBarSize"] = (Double)Clamp(width, MinimumWidth, MaximumWidth);
			}
		}
	}

	/// <summary>
	/// Gives a scroll bar a slider that is never shorter than the smallest length. The track works the slider out from the size of
	/// what is seen and the size of what there is; a longer "seen" part makes the slider longer, and the scroll bar reports its
	/// positions through the track, so the dragging stays right.
	/// </summary>
	public sealed class ScrollBarViewportConverter : IMultiValueConverter
	{
		/// <param name="values">The size of what is seen, the smallest and the largest value, the length of the track, the smallest slider.</param>
		public Object Convert(Object[] values, Type targetType, Object parameter, CultureInfo culture)
		{
			if (values.Length < 5 || values[0] is not Double viewport || values[1] is not Double minimum || values[2] is not Double maximum || values[3] is not Double trackLength || values[4] is not Double minimumThumb)
			{
				return values.Length > 0 && values[0] is Double value ? value : 0.0;
			}

			Double range = maximum - minimum;
			if (Double.IsNaN(viewport) || Double.IsInfinity(viewport) || range <= 0 || trackLength <= minimumThumb || trackLength <= 0)
			{
				return viewport;
			}

			Double thumb = trackLength * viewport / (range + viewport);
			return thumb >= minimumThumb ? viewport : minimumThumb * range / (trackLength - minimumThumb);
		}

		public Object[] ConvertBack(Object value, Type[] targetTypes, Object parameter, CultureInfo culture) => throw new NotSupportedException();
	}
}