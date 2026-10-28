namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the pictures of Fallout Tactics (.zar): the signature "&lt;zar&gt;" and a zero, a type (0x33 or 0x34), a zero, the width and
	/// the height (32 bits), a byte that says if a palette follows (a count of colors, then blue, green, red and alpha of each, and the
	/// default color for the type 0x34), the size of the data, then the runs: a byte holds a mode (2 low bits) and a count (6 high bits),
	/// and the modes are transparent pixels, palette indexes, indexes with an alpha, and alphas of the default color.
	/// </summary>
	public static class ZarReader
	{
		private const Int32 HeaderSize = 17;
		private const Int32 MaxSide = 16384;
		private const Int32 PaletteColors = 256;
		private const Byte TypeWithDefaultColor = 0x34;

		/// <summary>The signature of a picture.</summary>
		public static ReadOnlySpan<Byte> Signature => "<zar>\0"u8;

		/// <summary>True when the data starts like a ZAR file.</summary>
		public static Boolean IsZar(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize + 4 && data[..Signature.Length].SequenceEqual(Signature);
		}

		/// <exception cref="InvalidDataException">The data is not a valid ZAR file.</exception>
		public static ZarImage Read(Byte[] data)
		{
			Int32 position = 0;
			return Decode(data, ref position, null, false);
		}

		/// <summary>
		/// Decodes the picture that starts at a position, and moves the position to the end of its data.
		/// </summary>
		/// <param name="externalPalette">
		/// The palette (256 colors of 4 bytes: blue, green, red, alpha) of a picture that has none, as the pictures of a sprite, which share
		/// the palette of their animation; null for a picture that must hold its own (a gray one is used when it has none).
		/// </param>
		/// <param name="allowEmpty">True when a picture without pixels is valid (some frames of a sprite are).</param>
		/// <exception cref="InvalidDataException">The data is not a valid picture.</exception>
		public static ZarImage Decode(Byte[] data, ref Int32 position, Byte[]? externalPalette, Boolean allowEmpty)
		{
			ReadOnlySpan<Byte> span = data;
			if (position < 0 || span.Length - position < HeaderSize + 4 || !span.Slice(position, Signature.Length).SequenceEqual(Signature))
				throw new InvalidDataException("This is not a ZAR picture.");

			Int32 start = position;
			Byte type = span[start + 6];
			UInt32 width = BinaryPrimitives.ReadUInt32LittleEndian(span[(start + 8)..]);
			UInt32 height = BinaryPrimitives.ReadUInt32LittleEndian(span[(start + 12)..]);
			Boolean hasPalette = span[start + 16] != 0;
			if (width > MaxSide || height > MaxSide || ((width == 0 || height == 0) && !allowEmpty))
				throw new InvalidDataException("The size of the picture is impossible.");

			Int32 at = start + HeaderSize;
			Byte[] palette = externalPalette ?? GrayPalette();
			Int32 colorCount = PaletteColors;
			Int32 defaultColor = 0;
			if (hasPalette)
			{
				UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
				at += 4;
				Int64 needed = ((Int64)count * 4) + (type == TypeWithDefaultColor ? 1 : 0) + 4;
				if (count > PaletteColors || needed > span.Length - at)
					throw new InvalidDataException("The palette of the picture is impossible.");

				palette = new Byte[PaletteColors * 4];
				span.Slice(at, (Int32)count * 4).CopyTo(palette);
				colorCount = (Int32)count;
				at += (Int32)count * 4;
				if (type == TypeWithDefaultColor)
					defaultColor = span[at++];
			}

			UInt32 dataSize = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
			at += 4;
			if (dataSize > span.Length - at)
				throw new InvalidDataException("The picture is incomplete.");

			Byte[] bgra = new Byte[(Int32)width * (Int32)height * 4];
			DecodeRuns(span.Slice(at, (Int32)dataSize), bgra, palette, colorCount, defaultColor);
			position = at + (Int32)dataSize;
			return new ZarImage((Int32)width, (Int32)height, bgra);
		}

		private static void DecodeRuns(ReadOnlySpan<Byte> runs, Byte[] bgra, Byte[] palette, Int32 colorCount, Int32 defaultColor)
		{
			Int32 total = bgra.Length / 4;
			Int32 pixel = 0;
			Int32 read = 0;
			while (read < runs.Length && pixel < total)
			{
				Int32 control = runs[read++];
				Int32 mode = control & 3;
				Int32 count = control >> 2;
				if (mode == 0)
				{
					// Transparent pixels: the array is already clear.
					pixel += count;
					continue;
				}

				for (Int32 i = 0; i < count && pixel < total; i++)
				{
					Int32 index;
					Byte alpha;
					if (mode == 1)
					{
						if (read >= runs.Length)
							return;

						index = runs[read++];
						alpha = 255;
					}
					else if (mode == 2)
					{
						if (read + 1 >= runs.Length)
							return;

						index = runs[read++];
						alpha = runs[read++];
					}
					else
					{
						if (read >= runs.Length)
							return;

						index = defaultColor;
						alpha = runs[read++];
					}

					if (index < colorCount)
					{
						Int32 target = pixel * 4;
						bgra[target] = palette[index * 4];
						bgra[target + 1] = palette[(index * 4) + 1];
						bgra[target + 2] = palette[(index * 4) + 2];
						bgra[target + 3] = alpha;
					}

					pixel++;
				}
			}
		}

		private static Byte[] GrayPalette()
		{
			Byte[] palette = new Byte[PaletteColors * 4];
			for (Int32 i = 0; i < PaletteColors; i++)
			{
				palette[i * 4] = palette[(i * 4) + 1] = palette[(i * 4) + 2] = (Byte)i;
			}

			return palette;
		}
	}
}
