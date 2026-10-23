namespace DatExplorerIII.Services.Media
{
	using System;
	using System.Linq;
	using Models.Graphics;

	/// <summary>
	/// Turns the indexed pictures of Fallout into pictures made of pixels (<see cref="PixelImage"/>) the views can draw.
	/// </summary>
	public static class ImageRenderer
	{
		private const Int32 SwatchSize = 16;

		/// <summary>
		/// Draws a frame on a canvas, centered horizontally and resting on the bottom (the origin of a Fallout sprite
		/// is the bottom middle), so that the frames of an animation stay aligned. The index 0 is transparent.
		/// </summary>
		public static PixelImage Render(FrmFrame frame, Int32 canvasWidth, Int32 canvasHeight, Palette palette)
		{
			canvasWidth = Math.Max(1, canvasWidth);
			canvasHeight = Math.Max(1, canvasHeight);
			Byte[] bgra = new Byte[canvasWidth * canvasHeight * 4];

			Int32 left = (canvasWidth - frame.Width) / 2;
			Int32 top = canvasHeight - frame.Height;
			for (Int32 y = 0; y < frame.Height; y++)
			{
				for (Int32 x = 0; x < frame.Width; x++)
				{
					Int32 index = frame.Pixels[(y * frame.Width) + x];
					if (index != 0)
						SetPixel(bgra, ((((top + y) * canvasWidth) + left + x) * 4), palette, index);
				}
			}

			return Create(canvasWidth, canvasHeight, bgra);
		}

		/// <summary>Draws a full screen picture, whose colors all show.</summary>
		public static PixelImage Render(RixImage image)
		{
			Byte[] bgra = new Byte[image.Width * image.Height * 4];
			for (Int32 i = 0; i < image.Pixels.Length; i++)
			{
				SetPixel(bgra, i * 4, image.Palette, image.Pixels[i]);
			}

			return Create(image.Width, image.Height, bgra);
		}

		/// <summary>Draws a mask: the pixels that are set are white, the others black.</summary>
		public static PixelImage Render(MaskImage mask)
		{
			Byte[] bgra = new Byte[mask.Width * mask.Height * 4];
			for (Int32 i = 0; i < mask.Pixels.Length; i++)
			{
				SetGray(bgra, i * 4, mask.Pixels[i] ? (Byte)255 : (Byte)0);
			}

			return Create(mask.Width, mask.Height, bgra);
		}

		/// <summary>Draws the 256 colors of a palette as a grid of 16 by 16 squares.</summary>
		public static PixelImage RenderPalette(Palette palette)
		{
			Int32 side = 16 * SwatchSize;
			Byte[] bgra = new Byte[side * side * 4];
			for (Int32 y = 0; y < side; y++)
			{
				for (Int32 x = 0; x < side; x++)
				{
					SetPixel(bgra, ((y * side) + x) * 4, palette, ((y / SwatchSize) * 16) + (x / SwatchSize));
				}
			}

			return Create(side, side, bgra);
		}

		private static void SetPixel(Byte[] bgra, Int32 offset, Palette palette, Int32 index)
		{
			bgra[offset] = palette.GetBlue(index);
			bgra[offset + 1] = palette.GetGreen(index);
			bgra[offset + 2] = palette.GetRed(index);
			bgra[offset + 3] = 255;
		}

		/// <summary>
		/// Draws the characters of a font in a table of 16 columns, a character per cell, from the code 0: light pixels on a dark
		/// background (the nine levels of an AAF font are nine grays).
		/// </summary>
		public static PixelImage Render(FontFile font)
		{
			const Int32 columns = 16;
			Int32 cellWidth = Math.Max(1, font.Glyphs.Max(glyph => glyph.Width)) + 3;
			Int32 cellHeight = Math.Max(1, font.Glyphs.Max(glyph => glyph.Height)) + 3;
			Int32 rows = (font.Glyphs.Count + columns - 1) / columns;
			Int32 width = (columns * cellWidth) + 1;
			Int32 height = (rows * cellHeight) + 1;
			Byte[] bgra = new Byte[width * height * 4];

			// The lines of the table, on a dark background.
			for (Int32 y = 0; y < height; y++)
			{
				for (Int32 x = 0; x < width; x++)
				{
					Byte shade = (Byte)(x % cellWidth == 0 || y % cellHeight == 0 ? 70 : 32);
					SetGray(bgra, ((y * width) + x) * 4, shade);
				}
			}

			for (Int32 index = 0; index < font.Glyphs.Count; index++)
			{
				FontGlyph glyph = font.Glyphs[index];
				Int32 left = ((index % columns) * cellWidth) + 2;
				Int32 top = ((index / columns) * cellHeight) + 2;
				for (Int32 y = 0; y < glyph.Height; y++)
				{
					for (Int32 x = 0; x < glyph.Width; x++)
					{
						Int32 level = glyph.Pixels[(y * glyph.Width) + x];
						if (level > 0)
							SetGray(bgra, (((top + y) * width) + left + x) * 4, font.Format == FontFormat.Aaf ? (Byte)Math.Min(255, 50 + (Math.Min(level, 9) * 23)) : (Byte)255);
					}
				}
			}

			// Enlarged, the characters are too small to read otherwise.
			const Int32 zoom = 3;
			Byte[] large = new Byte[width * zoom * height * zoom * 4];
			for (Int32 y = 0; y < height * zoom; y++)
			{
				for (Int32 x = 0; x < width * zoom; x++)
				{
					Array.Copy(bgra, (((y / zoom) * width) + (x / zoom)) * 4, large, ((y * width * zoom) + x) * 4, 4);
				}
			}

			return Create(width * zoom, height * zoom, large);
		}

		private static void SetGray(Byte[] bgra, Int32 offset, Byte shade)
		{
			bgra[offset] = shade;
			bgra[offset + 1] = shade;
			bgra[offset + 2] = shade;
			bgra[offset + 3] = 255;
		}

		private static PixelImage Create(Int32 width, Int32 height, Byte[] bgra)
		{
			return new PixelImage(width, height, bgra);
		}
	}
}