namespace DatExplorerIII.Core
{
	using System;
	using System.IO;

	/// <summary>
	/// Fallout 1 LZSS decompression.
	/// See https://falloutmods.fandom.com/wiki/DAT_file_format#Fallout_1_LZSS_uncompression_algorithm
	/// </summary>
	internal static class Lzss
	{
		private const Int32 DictionarySize = 4096;
		private const Int32 MinMatch = 3;
		private const Int32 MaxMatch = 18;

		/// <summary>
		/// Decompresses a sequence of LZSS blocks.
		/// </summary>
		/// <param name="source">Stored data of the file.</param>
		/// <param name="unpackedSize">Expected size of the decompressed data.</param>
		/// <exception cref="InvalidDataException">The data is damaged or does not have the announced size.</exception>
		public static Byte[] Decompress(ReadOnlySpan<Byte> source, Int32 unpackedSize)
		{
			// A match of 18 bytes takes 2 bytes (and a flag bit): the data cannot expand more than about 8.5 times.
			if (unpackedSize < 0 || unpackedSize > source.Length * 10L + 64)
				throw new InvalidDataException("The announced size of the file is impossible.");

			Byte[] output = new Byte[unpackedSize];
			Byte[] dictionary = new Byte[DictionarySize];
			Int32 outputIndex = 0;
			Int32 position = 0;

			while (position + 2 <= source.Length && outputIndex < unpackedSize)
			{
				// Size of the next block: negative for raw bytes, zero for the end of the data.
				Int32 blockSize = (Int16)((source[position] << 8) | source[position + 1]);
				position += 2;

				if (blockSize == 0)
					break;

				if (blockSize < 0)
				{
					Int32 length = Math.Min(-blockSize, Math.Min(source.Length - position, unpackedSize - outputIndex));
					source.Slice(position, length).CopyTo(output.AsSpan(outputIndex));
					outputIndex += length;
					position += -blockSize;
					continue;
				}

				Int32 blockEnd = Math.Min(position + blockSize, source.Length);
				Array.Fill(dictionary, (Byte)0x20);
				Int32 dictionaryIndex = DictionarySize - MaxMatch;

				while (position < blockEnd && outputIndex < unpackedSize)
				{
					Int32 flags = source[position++];

					for (Int32 bit = 0; bit < 8 && position < blockEnd && outputIndex < unpackedSize; bit++)
					{
						if ((flags & 1) == 1)
						{
							Byte value = source[position++];
							output[outputIndex++] = value;
							dictionary[dictionaryIndex++ % DictionarySize] = value;
						}
						else
						{
							if (position + 1 >= blockEnd)
							{
								position = blockEnd;
								break;
							}

							Int32 low = source[position++];
							Int32 high = source[position++];
							Int32 offset = low | ((high & 0xF0) << 4);
							Int32 length = (high & 0x0F) + MinMatch;

							for (Int32 i = 0; i < length && outputIndex < unpackedSize; i++)
							{
								Byte value = dictionary[(offset + i) % DictionarySize];
								output[outputIndex++] = value;
								dictionary[dictionaryIndex++ % DictionarySize] = value;
							}
						}

						flags >>= 1;
					}
				}

				position = blockEnd;
			}

			if (outputIndex != unpackedSize)
				throw new InvalidDataException("The compressed data of the file is truncated.");

			return output;
		}
	}
}