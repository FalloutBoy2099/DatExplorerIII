namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using System.Text;
	using Models.Graphics;

	/// <summary>
	/// Reads Fallout full screen pictures (.rix): the signature "RIX3", the width and the height (16 bits, little endian),
	/// two descriptive bytes, a palette of 256 colors of three values from 0 to 63, then one byte per pixel.
	/// </summary>
	public static class RixReader
	{
		private const Int32 HeaderSize = 10;
		private const Int32 PaletteSize = 768;
		private const Int32 MaxSide = 8192;

		/// <summary>True when the data starts like a RIX file.</summary>
		public static Boolean IsRix(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize + PaletteSize && Encoding.ASCII.GetString(data[..4]) == "RIX3";
		}

		/// <exception cref="InvalidDataException">The data is not a valid RIX file.</exception>
		public static RixImage Read(Byte[] data)
		{
			if (!IsRix(data))
				throw new InvalidDataException("This is not a RIX picture.");

			Int32 width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4));
			Int32 height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6));
			if (width == 0 || height == 0 || width > MaxSide || height > MaxSide)
				throw new InvalidDataException("The size of the picture is impossible.");

			Int32 pixelCount = width * height;
			Int32 pixelStart = HeaderSize + PaletteSize;
			if (data.Length < pixelStart + pixelCount)
				throw new InvalidDataException("The picture is incomplete.");

			Palette palette = PaletteReader.Read(data.AsSpan(HeaderSize, PaletteSize));
			Byte[] pixels = new Byte[pixelCount];
			Array.Copy(data, pixelStart, pixels, 0, pixelCount);
			return new RixImage(width, height, palette, pixels);
		}
	}
}