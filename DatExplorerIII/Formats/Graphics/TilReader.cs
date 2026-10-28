namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the tiles of Fallout Tactics (.til): the signature "&lt;tile&gt;" and a zero, a name ending with a zero, 11 bytes that are not
	/// understood, the width and the height (32 bits), then, after the tag "&lt;tiledata&gt;" and 3 bytes, the count of pictures (32 bits)
	/// and the pictures (<see cref="ZarReader"/>), each followed by 8 bytes.
	/// </summary>
	public static class TilReader
	{
		private const Int32 MaxFrames = 4096;
		private const Int32 FrameTrailer = 8;
		private const Int32 AfterTag = 3;

		/// <summary>True when the data starts like a TIL file.</summary>
		public static Boolean IsTil(ReadOnlySpan<Byte> data)
		{
			return data.StartsWith("<tile>\0"u8);
		}

		/// <exception cref="InvalidDataException">The data is not a valid TIL file.</exception>
		public static TilImage Read(Byte[] data)
		{
			if (!IsTil(data))
				throw new InvalidDataException("This is not a TIL tile.");

			ReadOnlySpan<Byte> span = data;
			Int32 position = "<tile>\0"u8.Length;
			Int32 nameEnd = span[position..].IndexOf((Byte)0);
			if (nameEnd < 0)
				throw new InvalidDataException("The tile is incomplete.");

			// The name and its zero, then 7 and 4 bytes that are not understood.
			position += nameEnd + 1 + 7 + 4;
			if (span.Length - position < 8)
				throw new InvalidDataException("The tile is incomplete.");

			Int32 width = (Int32)Math.Min(Int32.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(span[position..]));
			Int32 height = (Int32)Math.Min(Int32.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(span[(position + 4)..]));
			position += 8;

			ReadOnlySpan<Byte> tag = "<tiledata>"u8;
			Int32 tagAt = span[position..].IndexOf(tag);
			if (tagAt < 0)
				throw new InvalidDataException("The tile has no pictures.");

			position += tagAt + tag.Length + AfterTag;
			if (span.Length - position < 4)
				throw new InvalidDataException("The tile is incomplete.");

			UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(span[position..]);
			position += 4;
			if (count > MaxFrames)
				throw new InvalidDataException("The count of pictures is impossible.");

			List<ZarImage> frames = [];
			for (Int32 i = 0; i < count; i++)
			{
				frames.Add(ZarReader.Decode(data, ref position, null, false));
				position = Math.Min(span.Length, position + FrameTrailer);
			}

			return new TilImage(width, height, frames);
		}
	}
}
