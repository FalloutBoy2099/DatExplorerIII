namespace DatExplorerIII.Services.Media
{
	using System;
	using System.IO;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Models.Graphics;

	/// <summary>
	/// Reads the usual picture files (PNG, BMP, JPEG, GIF) with the decoders of Windows, into pictures made of pixels.
	/// </summary>
	public static class PictureDecoder
	{
		/// <summary>The pictures larger than this (in pixels) are not decoded: they would take too much memory.</summary>
		private const Int64 MaxPixels = 100L * 1024 * 1024;

		/// <summary>
		/// Decodes a picture (the first image of an animated GIF). Any thread can do it: the result is plain data.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not a picture Windows can read.</exception>
		public static PixelImage Decode(Byte[] data)
		{
			try
			{
				using MemoryStream stream = new(data);
				BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
				BitmapFrame frame = decoder.Frames[0];

				if ((Int64)frame.PixelWidth * frame.PixelHeight > MaxPixels)
					throw new InvalidDataException("The picture is too large.");

				BitmapSource source = frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
				Byte[] pixels = new Byte[frame.PixelWidth * frame.PixelHeight * 4];
				source.CopyPixels(pixels, frame.PixelWidth * 4, 0);
				return new PixelImage(frame.PixelWidth, frame.PixelHeight, pixels);
			}
			catch (Exception exception) when (exception is NotSupportedException or FileFormatException or ArgumentException or InvalidOperationException or IOException)
			{
				throw new InvalidDataException("This is not a picture that can be read.", exception);
			}
		}
	}
}