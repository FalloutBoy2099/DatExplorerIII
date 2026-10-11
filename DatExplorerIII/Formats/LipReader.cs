namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Models;

	/// <summary>
	/// Reads the lip synchronization files (.lip) of Fallout (all numbers big endian): a header of 0x2C bytes (the version, the
	/// length of the unpacked sound, the number of phonemes, the number of markers, the name of the sound file), the phoneme
	/// codes, one byte each, then the markers, a type and a position in the sound on 32 bits each.
	/// </summary>
	public static class LipReader
	{
		private const Int32 HeaderSize = 0x2C;
		private const Int32 MarkerSize = 8;
		private const Int32 NameStart = 0x20;
		private const Int32 NameLength = 8;
		private const Int32 MaxCount = 1_000_000;

		/// <summary>The phonemes of the game by code: how they sound (IPA) and the picture of the talking head they show.</summary>
		public static readonly IReadOnlyList<(String Ipa, Int32 Frame)> Phonemes =
		[
			("silent", 0), ("i:", 3), ("ɪ", 1), ("eɪ", 1), ("e", 3), ("æ", 1), ("ɑ:", 1), ("ɔ:", 1), ("oʊ", 7), ("ʊ", 8),
			("u:", 7), ("ʊəʳ", 3), ("ɒ", 1), ("ʌ", 8), ("aɪ", 1), ("aʊ", 7), ("ɔɪ", 7), ("p", 6), ("b", 6), ("t", 2),
			("d", 2), ("k", 2), ("g", 2), ("f", 4), ("v", 4), ("θ", 5), ("ð", 5), ("s", 2), ("z", 2), ("ʃ", 2),
			("ʒ", 2), ("h", 2), ("m", 6), ("n", 2), ("ŋ", 2), ("l", 5), ("w", 8), ("j", 2), ("r", 2), ("tʃ", 2),
			("dʒ", 2), ("**", 8),
		];

		/// <exception cref="InvalidDataException">The data is not a valid lip synchronization file.</exception>
		public static LipFile Read(Byte[] data)
		{
			if (data.Length < HeaderSize)
			{
				throw new InvalidDataException("This is not a lip synchronization file.");
			}

			Int32 version = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0));
			Int32 unpackedLength = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0x10));
			Int32 phonemeCount = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0x14));
			Int32 markerCount = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0x1C));
			if (version is < 0 or > 255 || phonemeCount is < 0 or > MaxCount || markerCount is < 0 or > MaxCount)
			{
				throw new InvalidDataException("This is not a lip synchronization file.");
			}

			Int64 end = HeaderSize + (Int64)phonemeCount + (Int64)markerCount * MarkerSize;
			if (end > data.Length)
			{
				throw new InvalidDataException("The file is cut.");
			}

			ReadOnlySpan<Byte> nameBytes = data.AsSpan(NameStart, NameLength);
			Int32 nameEnd = nameBytes.IndexOf((Byte)0);
			String name = Encoding.ASCII.GetString(nameBytes[..(nameEnd < 0 ? NameLength : nameEnd)]);

			Byte[] phonemes = data.AsSpan(HeaderSize, phonemeCount).ToArray();
			List<LipMarker> markers = new(markerCount);
			Int32 position = HeaderSize + phonemeCount;
			for (Int32 index = 0; index < markerCount; index++)
			{
				markers.Add(new LipMarker(BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position)), BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position + 4))));
				position += MarkerSize;
			}

			return new LipFile(version, unpackedLength, name, phonemes, markers);
		}
	}
}