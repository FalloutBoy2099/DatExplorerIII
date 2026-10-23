namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Models.Graphics;

	/// <summary>
	/// Reads the AAF fonts of Fallout 1 and 2: the signature "AAFF", the height of the font, the space between characters, the
	/// width of the space and the space between lines (16 bits, big endian), a directory of 256 characters (width and height on
	/// 16 bits, the place of the picture on 32 bits, counted from 0x80C), then one byte per pixel, from 0 (empty) to 9 (bright).
	/// </summary>
	public static class AafReader
	{
		private const Int32 DirectoryStart = 0x0C;
		private const Int32 EntrySize = 8;
		private const Int32 GlyphCount = 256;
		private const Int32 DataStart = DirectoryStart + GlyphCount * EntrySize;
		private const Int32 MaxSide = 1024;

		/// <summary>True when the data starts like an AAF font.</summary>
		public static Boolean IsAaf(ReadOnlySpan<Byte> data)
		{
			return data.Length >= DataStart && Encoding.ASCII.GetString(data[..4]) == "AAFF";
		}

		/// <exception cref="InvalidDataException">The data is not a valid AAF font.</exception>
		public static FontFile Read(Byte[] data)
		{
			if (!IsAaf(data))
				throw new InvalidDataException("This is not an AAF font.");

			Int32 height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
			Int32 spacing = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6));
			Int32 spaceWidth = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8));
			Int32 lineSpacing = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(10));

			List<FontGlyph> glyphs = [with(GlyphCount)];
			for (Int32 index = 0; index < GlyphCount; index++)
			{
				ReadOnlySpan<Byte> entry = data.AsSpan(DirectoryStart + index * EntrySize, EntrySize);
				Int32 width = BinaryPrimitives.ReadUInt16BigEndian(entry);
				Int32 glyphHeight = BinaryPrimitives.ReadUInt16BigEndian(entry[2..]);
				Int64 offset = DataStart + (Int64)BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);
				if (width > MaxSide || glyphHeight > MaxSide)
					throw new InvalidDataException("The size of a character is impossible.");

				Int32 size = width * glyphHeight;
				if (size == 0)
				{
					// A character without picture: its place in the file is the one of the next picture, which is not its own.
					glyphs.Add(new FontGlyph(width, glyphHeight, []));
					continue;
				}

				if (offset + size > data.Length)
					throw new InvalidDataException("A character is outside the file.");

				glyphs.Add(new FontGlyph(width, glyphHeight, data.AsSpan((Int32)offset, size).ToArray()));
			}

			return new FontFile(FontFormat.Aaf, height, spacing, spaceWidth, lineSpacing, glyphs);
		}
	}

	/// <summary>
	/// Reads the FON fonts of Fallout 1: the number of characters, the height of the font and the space between characters
	/// (32 bits), two pointers that mean nothing in the file, then for each character its width and the place of its picture
	/// (32 bits each), and the pictures, one bit per pixel, a row after the other, each row padded to a whole byte, the most
	/// significant bit being the leftmost pixel. The numbers are little endian (the file was made on a PC); a file written the other
	/// way round is read as well.
	/// </summary>
	public static class FonReader
	{
		private const Int32 HeaderSize = 20;
		private const Int32 EntrySize = 8;
		private const Int32 MaxGlyphs = 1024;
		private const Int32 MaxSide = 1024;

		/// <exception cref="InvalidDataException">The data is not a valid FON font.</exception>
		public static FontFile Read(Byte[] data)
		{
			if (data.Length < HeaderSize)
				throw new InvalidDataException("This is not a FON font.");

			// The number of characters tells how the numbers are written: the wrong order gives a huge number.
			Boolean little = BinaryPrimitives.ReadInt32LittleEndian(data) is > 0 and <= MaxGlyphs;
			Int32 ReadInt(Int32 position) => little ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position)) : BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position));

			Int32 count = ReadInt(0);
			Int32 height = ReadInt(4);
			Int32 spacing = ReadInt(8);
			if (count is <= 0 or > MaxGlyphs || height is <= 0 or > MaxSide || spacing is < 0 or > MaxSide)
				throw new InvalidDataException("This is not a FON font.");

			Int64 dataStart = HeaderSize + (Int64)count * EntrySize;
			if (dataStart > data.Length)
				throw new InvalidDataException("The list of the characters is cut.");

			List<FontGlyph> glyphs = [with(count)];
			for (Int32 index = 0; index < count; index++)
			{
				Int32 width = ReadInt(HeaderSize + index * EntrySize);
				Int32 offset = ReadInt(HeaderSize + index * EntrySize + 4);
				if (width is < 0 or > MaxSide || offset < 0)
					throw new InvalidDataException("The size of a character is impossible.");

				Int32 bytesPerRow = (width + 7) / 8;
				Int64 end = dataStart + offset + (Int64)bytesPerRow * height;
				if (end > data.Length)
					throw new InvalidDataException("A character is outside the file.");

				Byte[] pixels = new Byte[width * height];
				for (Int32 row = 0; row < height; row++)
				{
					Int32 rowStart = (Int32)dataStart + offset + row * bytesPerRow;
					for (Int32 column = 0; column < width; column++)
					{
						if ((data[rowStart + column / 8] & (0x80 >> (column % 8))) != 0)
							pixels[row * width + column] = 1;
					}
				}

				glyphs.Add(new FontGlyph(width, height, pixels));
			}

			return new FontFile(FontFormat.Fon, height, spacing, 0, 0, glyphs);
		}
	}
}