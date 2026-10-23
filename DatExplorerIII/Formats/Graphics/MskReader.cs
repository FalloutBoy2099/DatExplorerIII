namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the masks of the world map of Fallout 2 (.msk, <c>data\wrldmp00.msk</c>...): one file for each tile of the map, 350 by
	/// 300 pixels of one bit each, from the left (the high bit of a byte) to the right, a line after the other, every line taking a
	/// whole number of 32 bit words (44 bytes). There is no header: the size of the file is the only thing that says it is a mask.
	/// </summary>
	public static class MskReader
	{
		public const Int32 Width = 350;
		public const Int32 Height = 300;

		/// <summary>Bytes of a line: the 350 bits, rounded up to a whole number of 32 bit words.</summary>
		private const Int32 Stride = ((Width + 31) / 32) * 4;

		/// <summary>The size of a mask of the world map.</summary>
		public const Int32 FileSize = Stride * Height;

		/// <exception cref="InvalidDataException">The data is not a mask of the world map.</exception>
		public static MaskImage Read(Byte[] data)
		{
			if (data.Length != FileSize)
				throw new InvalidDataException("This is not a mask of the world map.");

			Boolean[] pixels = new Boolean[Width * Height];
			for (Int32 y = 0; y < Height; y++)
			{
				for (Int32 x = 0; x < Width; x++)
				{
					pixels[(y * Width) + x] = (data[(y * Stride) + (x >> 3)] & (0x80 >> (x & 7))) != 0;
				}
			}

			return new MaskImage(Width, Height, pixels);
		}
	}
}