namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A picture as plain data: 32 bit pixels in the order blue, green, red, alpha, line after line. The view models hold pictures
	/// this way; the views turn them into what the screen draws.
	/// </summary>
	public sealed class PixelImage
	{
		/// <param name="width">Width in pixels.</param>
		/// <param name="height">Height in pixels.</param>
		/// <param name="pixels">The pixels (4 bytes each). The picture uses this array, it does not copy it.</param>
		public PixelImage(Int32 width, Int32 height, Byte[] pixels)
		{
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
			ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
			if (pixels.Length != (Int64)width * height * 4)
			{
				throw new ArgumentException("The pixels do not fit the size of the picture.", nameof(pixels));
			}

			Width = width;
			Height = height;
			Pixels = pixels;
		}

		/// <summary>Raised after the pixels were changed (the pictures of a movie are drawn in the same array one after the other).</summary>
		public event EventHandler? Changed;

		public Int32 Width { get; }

		public Int32 Height { get; }

		public Byte[] Pixels { get; }

		/// <summary>Bytes of a line.</summary>
		public Int32 Stride => Width * 4;

		/// <summary>Tells the views that the pixels changed.</summary>
		public void Invalidate()
		{
			Changed?.Invoke(this, EventArgs.Empty);
		}
	}
}