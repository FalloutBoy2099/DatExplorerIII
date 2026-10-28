namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the area of a town map of Arcanum (.tma): the signature "TMA2", the width and the height of the map in tiles, four more
	/// 32 bit numbers (64, 32, 4 and 8 in the files known), the size of the bits, then a bit for each tile, a line after the other, with
	/// no padding between the lines, the lowest bit of a byte first. A bit is set for the tiles that are part of the map.
	/// </summary>
	public static class TmaReader
	{
		private const Int32 HeaderSize = 32;
		private const Int32 MaxSide = 16384;

		/// <summary>True when the data starts like a TMA file.</summary>
		public static Boolean IsTma(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize && data.StartsWith("TMA2"u8);
		}

		/// <exception cref="InvalidDataException">The data is not a valid TMA file.</exception>
		public static MaskImage Read(Byte[] data)
		{
			if (!IsTma(data))
				throw new InvalidDataException("This is not a TMA area.");

			UInt32 width = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4));
			UInt32 height = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8));
			if (width == 0 || height == 0 || width > MaxSide || height > MaxSide)
				throw new InvalidDataException("The size of the area is impossible.");

			Int32 count = (Int32)(width * height);
			if (data.Length - HeaderSize < (count + 7) / 8)
				throw new InvalidDataException("The area is incomplete.");

			Boolean[] pixels = new Boolean[count];
			for (Int32 i = 0; i < count; i++)
			{
				pixels[i] = (data[HeaderSize + (i >> 3)] & (1 << (i & 7))) != 0;
			}

			return new MaskImage((Int32)width, (Int32)height, pixels);
		}
	}
}
