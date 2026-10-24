namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the picture files (.art) of Arcanum. All numbers are little endian 32 bit.
	/// </summary>
	/// <remarks>
	/// <para>The file is a header of 33 numbers (the flags, the frame rate, the bits per pixel, which palettes the file has, the
	/// action frame, the number of frames, then tables that the game fills in memory), the palettes (256 colors of four bytes:
	/// blue, green, red and a zero), a table with one description for each picture (width, height, size of the data, the
	/// hotspot and the offset), then the data of the pictures one after the other.</para>
	/// <para>A picture is stored as it is when its size is its width times its height. Otherwise it is run-length encoded: a
	/// byte whose high bit is set is followed by as many literal colors as its low bits say, a byte whose high bit is clear by one
	/// color repeated as many times as its low bits say. The rows follow each other, a run can go from a row to the next.
	/// The color 0 is transparent.</para>
	/// <para>Static art (flag 1) is one picture, or a list of pictures; the other art has eight rotations of every frame.</para>
	/// </remarks>
	public static class ArtReader
	{
		private const Int32 HeaderSize = 0x84;
		private const Int32 PaletteSize = 0x400;
		private const Int32 InfoSize = 28;
		private const Int32 Rotations = 8;
		private const Int32 MaxPictureSide = 8192;
		private const Int64 MaxPixels = 64L * 1024 * 1024;

		/// <summary>True when the data starts like an art file: a plausible header and 8 bits per pixel.</summary>
		public static Boolean IsArt(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize + PaletteSize && BinaryPrimitives.ReadInt32LittleEndian(data[8..]) == 8;
		}

		/// <exception cref="InvalidDataException">The data is not an art file, or a picture lies outside of it.</exception>
		public static ArtImage Read(Byte[] data)
		{
			if (!IsArt(data))
				throw new InvalidDataException("This is not an art file.");

			UInt32 Header(Int32 index) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4));

			UInt32 flags = Header(0);
			Int32 framesPerSecond = (Int32)Math.Min(Header(1), 1000);
			Int32 paletteCount = Header(6) != 0 ? 4 : Header(5) != 0 ? 3 : Header(4) != 0 ? 2 : 1;
			Int32 actionFrame = (Int32)Math.Min(Header(7), Int32.MaxValue);
			Int64 frameCount = Header(8);
			Boolean isStatic = (flags & 1) != 0;

			List<Palette> palettes = new(paletteCount);
			Int64 position = HeaderSize;
			if (position + ((Int64)paletteCount * PaletteSize) > data.Length)
				throw new InvalidDataException("The palettes lie outside of the file.");

			for (Int32 p = 0; p < paletteCount; p++)
			{
				palettes.Add(ReadPalette(data, (Int32)position));
				position += PaletteSize;
			}

			// The pictures: a static art is a list of frames; the others have the same frames seen from several rotations.
			// A file that holds fewer than eight rotations (a sprite whose other sides are mirrored by the game) is recognized
			// by the table of pictures, which would not fit the file.
			Int32[] rotationChoices = isStatic ? [1] : [Rotations, 5, 1];
			foreach (Int32 rotations in rotationChoices)
			{
				ArtImage? image = TryRead(data, position, rotations, (Int32)Math.Min(frameCount, 100_000), framesPerSecond, actionFrame, palettes, isStatic);
				if (image != null)
					return image;
			}

			throw new InvalidDataException("The pictures lie outside of the file.");
		}

		private static ArtImage? TryRead(Byte[] data, Int64 tableStart, Int32 rotations, Int32 frames, Int32 framesPerSecond, Int32 actionFrame, List<Palette> palettes, Boolean isStatic)
		{
			Int64 pictureCount = (Int64)rotations * frames;
			if (frames <= 0 || tableStart + (pictureCount * InfoSize) > data.Length)
				return null;

			(Int32 Width, Int32 Height, Int64 Size, Int32 HotX, Int32 HotY)[] infos = new (Int32, Int32, Int64, Int32, Int32)[pictureCount];
			Int64 position = tableStart;
			Int64 total = 0;
			for (Int64 i = 0; i < pictureCount; i++)
			{
				Int32 at = (Int32)position;
				Int64 width = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
				Int64 height = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 4));
				Int64 size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 8));
				if (width > MaxPictureSide || height > MaxPictureSide || width * height > MaxPixels)
					return null;

				infos[i] = ((Int32)width, (Int32)height, size, BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 12)), BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at + 16)));
				total += size;
				position += InfoSize;
			}

			if (position + total > data.Length)
				return null;

			List<FrmDirection> directions = new(rotations);
			Int64 dataPosition = position;
			for (Int32 r = 0; r < rotations; r++)
			{
				List<FrmFrame> frameList = new(frames);
				for (Int32 f = 0; f < frames; f++)
				{
					var info = infos[((Int64)r * frames) + f];
					Byte[] pixels = DecodePixels(data, dataPosition, info.Size, info.Width, info.Height);
					frameList.Add(new FrmFrame(info.Width, info.Height, -info.HotX, -info.HotY, pixels));
					dataPosition += info.Size;
				}

				directions.Add(new FrmDirection(0, 0, frameList));
			}

			FrmImage image = new(0, frames > 1 ? framesPerSecond : 0, actionFrame, frames, directions);
			return new ArtImage(image, palettes, isStatic);
		}

		/// <summary>The palette of the file (blue, green, red and a zero for each color) as red, green and blue.</summary>
		private static Palette ReadPalette(Byte[] data, Int32 start)
		{
			Byte[] rgb = new Byte[Palette.Size * 3];
			for (Int32 i = 0; i < Palette.Size; i++)
			{
				rgb[i * 3] = data[start + (i * 4) + 2];
				rgb[(i * 3) + 1] = data[start + (i * 4) + 1];
				rgb[(i * 3) + 2] = data[start + (i * 4)];
			}

			return new Palette(rgb);
		}

		/// <summary>Unpacks the pixels of a picture: raw when the size says so, else run-length encoded.</summary>
		private static Byte[] DecodePixels(Byte[] data, Int64 start, Int64 size, Int32 width, Int32 height)
		{
			Byte[] pixels = new Byte[(Int64)width * height];
			if (size == pixels.Length)
			{
				Array.Copy(data, start, pixels, 0, size);
				return pixels;
			}

			Int64 end = start + size;
			Int64 position = start;
			Int32 output = 0;

			while (position < end && output < pixels.Length)
			{
				Int32 control = data[position++];
				Int32 count = control & 0x7F;

				if ((control & 0x80) == 0)
				{
					// One color repeated.
					if (position >= end)
						break;

					Byte color = data[position++];
					count = Math.Min(count, pixels.Length - output);
					Array.Fill(pixels, color, output, count);
					output += count;
				}
				else
				{
					// Colors as they are.
					count = (Int32)Math.Min(Math.Min(count, end - position), pixels.Length - output);
					Array.Copy(data, position, pixels, output, count);
					position += count;
					output += count;
				}
			}

			return pixels;
		}
	}
}