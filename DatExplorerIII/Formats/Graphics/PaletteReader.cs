namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads palettes: the Fallout ones (.pal, 256 colors of three values from 0 to 63) and the plain RGB ones (.act, three
	/// bytes per color, made by Photoshop and the Fallout tools). The palette file of the game (color.pal) also holds a
	/// table to find the closest color, which is not needed to show a picture.
	/// </summary>
	public static class PaletteReader
	{
		/// <summary>Reads a Fallout palette: values of 6 bits, shifted to 8 bits as the game does.</summary>
		/// <exception cref="InvalidDataException">The data is shorter than a palette.</exception>
		public static Palette Read(ReadOnlySpan<Byte> data)
		{
			Byte[] rgb = Take(data);

			// The game shifts the values and keeps the low byte: the colors that are cycled (229 to 254) may hold 255, which
			// gives 252, and a color of the palette of the game is never as bright as a plain scaling to 255 would make it.
			for (Int32 i = 0; i < rgb.Length; i++)
			{
				rgb[i] = (Byte)(rgb[i] << 2);
			}

			return new Palette(rgb);
		}

		/// <summary>Reads a palette of 8 bit values (the first 768 bytes; an .act file may end with a few more bytes).</summary>
		/// <exception cref="InvalidDataException">The data is shorter than a palette.</exception>
		public static Palette ReadRgb(ReadOnlySpan<Byte> data)
		{
			return new Palette(Take(data));
		}

		private static Byte[] Take(ReadOnlySpan<Byte> data)
		{
			if (data.Length < Palette.Size * 3)
				throw new InvalidDataException("A palette needs 768 bytes.");

			return data[..(Palette.Size * 3)].ToArray();
		}
	}
}