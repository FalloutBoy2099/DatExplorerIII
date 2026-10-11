namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A palette of 256 colors, 8 bits per channel. Fallout images store the index of a color, not the color.
	/// </summary>
	public sealed class Palette
	{
		public const Int32 Size = 256;

		private readonly Byte[] _rgb;

		/// <param name="rgb">Red, green and blue of the 256 colors: 768 bytes.</param>
		public Palette(Byte[] rgb)
		{
			if (rgb.Length != Size * 3)
			{
				throw new ArgumentException("A palette has 256 colors.", nameof(rgb));
			}

			_rgb = rgb;
		}

		public Byte GetRed(Int32 index) => _rgb[index * 3];

		public Byte GetGreen(Int32 index) => _rgb[(index * 3) + 1];

		public Byte GetBlue(Int32 index) => _rgb[(index * 3) + 2];

		/// <summary>A palette of 256 grays, for images shown without their palette.</summary>
		public static Palette Grayscale()
		{
			Byte[] rgb = new Byte[Size * 3];
			for (Int32 i = 0; i < Size; i++)
			{
				rgb[i * 3] = rgb[(i * 3) + 1] = rgb[(i * 3) + 2] = (Byte)i;
			}

			return new Palette(rgb);
		}
	}
}