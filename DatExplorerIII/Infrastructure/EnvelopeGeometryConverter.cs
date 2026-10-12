namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;
	using System.Windows.Media;

	/// <summary>
	/// Draws the shape of a sound from its loudness (the bytes, 0 to 255, of <c>AudioClip.Envelope</c>): the outline, mirrored
	/// around the middle line of a box of 1000 by 100 units.
	/// </summary>
	public sealed class EnvelopeGeometryConverter : IValueConverter
	{
		public Object? Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is Byte[] envelope ? CreateShape(envelope) : null;
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture) => throw new NotSupportedException();

		public static Geometry? CreateShape(Byte[] envelope)
		{
			if (envelope.Length == 0)
			{
				return null;
			}

			StreamGeometry geometry = new();
			using (StreamGeometryContext context = geometry.Open())
			{
				Double step = 1000.0 / envelope.Length;
				context.BeginFigure(new Point(0, 50), true, true);

				for (Int32 i = 0; i < envelope.Length; i++)
				{
					context.LineTo(new Point(i * step, 50 - (envelope[i] / 255.0 * 50)), true, false);
				}

				for (Int32 i = envelope.Length - 1; i >= 0; i--)
				{
					context.LineTo(new Point(i * step, 50 + (envelope[i] / 255.0 * 50)), true, false);
				}
			}

			geometry.Freeze();
			return geometry;
		}
	}
}