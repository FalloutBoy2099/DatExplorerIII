namespace DatExplorerIII.Services.Media
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Formats.GameFiles;
	using Models.GameFiles;
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

		/// <summary>Draws a picture of Fallout Tactics, which already has its colors and its transparency.</summary>
		public static PixelImage Render(ZarImage image)
		{
			return RenderSheet([image], 1);
		}

		/// <summary>
		/// Draws a picture of a sprite of Fallout Tactics on the area that holds all the pictures of its collection, at its place from the
		/// origin of the sprite, so that the frames of an animation stay aligned.
		/// </summary>
		public static PixelImage Render(SprCollection collection, SprFrame frame)
		{
			Byte[] bgra = new Byte[collection.Width * collection.Height * 4];
			ZarImage image = frame.Image;
			Int32 left = frame.OffsetX - collection.Left;
			Int32 top = frame.OffsetY - collection.Top;
			for (Int32 y = 0; y < image.Height; y++)
			{
				Array.Copy(image.Bgra, y * image.Width * 4, bgra, (((top + y) * collection.Width) + left) * 4, image.Width * 4);
			}

			return Create(collection.Width, collection.Height, bgra);
		}

		/// <summary>
		/// Draws pictures of Fallout Tactics side by side in a table, a picture per cell (the size of the largest), from left to right then
		/// from top to bottom.
		/// </summary>
		/// <param name="columns">How many pictures a line holds.</param>
		public static PixelImage RenderSheet(IReadOnlyList<ZarImage> frames, Int32 columns)
		{
			columns = Math.Clamp(columns, 1, Math.Max(1, frames.Count));
			Int32 gap = frames.Count > 1 ? 2 : 0;
			Int32 cellWidth = Math.Max(1, frames.Max(frame => frame.Width));
			Int32 cellHeight = Math.Max(1, frames.Max(frame => frame.Height));
			Int32 rows = (frames.Count + columns - 1) / columns;
			Int32 width = (columns * cellWidth) + ((columns - 1) * gap);
			Int32 height = (rows * cellHeight) + ((rows - 1) * gap);
			Byte[] bgra = new Byte[width * height * 4];

			for (Int32 index = 0; index < frames.Count; index++)
			{
				ZarImage frame = frames[index];
				Int32 left = (index % columns) * (cellWidth + gap);
				Int32 top = (index / columns) * (cellHeight + gap);
				for (Int32 y = 0; y < frame.Height; y++)
				{
					Array.Copy(frame.Bgra, y * frame.Width * 4, bgra, (((top + y) * width) + left) * 4, frame.Width * 4);
				}
			}

			return Create(width, height, bgra);
		}

		/// <summary>
		/// Draws the tiles of a sector of Arcanum as a map of colors, 4 pixels a tile: each kind of tile has a color of its own (the pictures
		/// of the tiles are not drawn). The tiles that are empty are left clear.
		/// </summary>
		public static PixelImage Render(SecFile sector)
		{
			const Int32 cell = 4;
			Int32 side = SecReader.Side;
			Int32 width = side * cell;
			Byte[] bgra = new Byte[width * width * 4];
			for (Int32 tile = 0; tile < sector.Tiles.Length; tile++)
			{
				UInt32 value = sector.Tiles[tile];
				if (value is 0 or UInt32.MaxValue)
					continue;

				// The kind of the tile is its number without the variant that is drawn at a place.
				UInt32 mixed = SecReader.KindOf(value) * 2654435761u;
				Double hue = (mixed >> 8) % 360;
				Double lightness = 0.55 + (((mixed >> 4) & 7) * 0.04);
				(Byte red, Byte green, Byte blue) = FromHsv(hue, 0.5, lightness);
				Int32 left = (tile % side) * cell;
				Int32 top = (tile / side) * cell;
				for (Int32 y = 0; y < cell; y++)
				{
					for (Int32 x = 0; x < cell; x++)
					{
						Int32 offset = (((top + y) * width) + left + x) * 4;
						bgra[offset] = blue;
						bgra[offset + 1] = green;
						bgra[offset + 2] = red;
						bgra[offset + 3] = 255;
					}
				}
			}

			return Create(width, width, bgra);
		}

		private static (Byte Red, Byte Green, Byte Blue) FromHsv(Double hue, Double saturation, Double value)
		{
			Double chroma = value * saturation;
			Double sector = hue / 60;
			Double second = chroma * (1 - Math.Abs((sector % 2) - 1));
			(Double r, Double g, Double b) = (Int32)sector switch
			{
				0 => (chroma, second, 0.0),
				1 => (second, chroma, 0.0),
				2 => (0.0, chroma, second),
				3 => (0.0, second, chroma),
				4 => (second, 0.0, chroma),
				_ => (chroma, 0.0, second),
			};
			Double match = value - chroma;
			return ((Byte)Math.Round((r + match) * 255), (Byte)Math.Round((g + match) * 255), (Byte)Math.Round((b + match) * 255));
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