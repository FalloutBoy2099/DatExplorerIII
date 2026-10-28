namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;
	using Graphics;
	using Models.GameFiles;
	using Models.Graphics;

	/// <summary>
	/// Reads the missions of Fallout Tactics (.mis): the signature "&lt;world&gt;" and a zero, the version as text with a zero, the size of
	/// the data twice (32 bits), then a zlib stream. The data start with "&lt;mph&gt;" and a zero, a version, the count of teams and their names
	/// (a 32 bit length and the characters), then further fields that are not understood, the path of the text of the mission, and
	/// (in some missions) the picture of the map (<see cref="ZarReader"/>). The format is not documented: only these parts are read.
	/// </summary>
	public static class MisReader
	{
		private const Int32 MaxTeams = 64;
		private const Int32 MaxNameLength = 256;
		private const Int32 TextSearchLimit = 4096;
		private const Int32 MaxSize = 512 * 1024 * 1024;

		private static ReadOnlySpan<Byte> Signature => "<world>\0"u8;

		/// <summary>True when the data starts like a MIS file.</summary>
		public static Boolean IsMis(ReadOnlySpan<Byte> data)
		{
			return data.StartsWith(Signature);
		}

		/// <exception cref="InvalidDataException">The data is not a valid MIS file.</exception>
		public static MisFile Read(Byte[] data)
		{
			if (!IsMis(data))
				throw new InvalidDataException("This is not a MIS mission.");

			ReadOnlySpan<Byte> span = data;
			Int32 at = Signature.Length;
			Int32 versionEnd = span[at..].IndexOf((Byte)0);
			if (versionEnd < 0 || span.Length - (at + versionEnd + 1) < 10)
				throw new InvalidDataException("The mission is incomplete.");

			String version = Encoding.ASCII.GetString(span.Slice(at, versionEnd));
			at += versionEnd + 1;
			UInt32 size = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
			at += 8;
			if (size == 0 || size > MaxSize)
				throw new InvalidDataException("The size of the mission is impossible.");

			Byte[] content = Inflate(data, at, (Int32)size);
			List<String> teams = ReadTeams(content);

			// The picture of the map is only in some missions.
			ZarImage? preview = null;
			Int32 pictureAt = content.AsSpan().IndexOf(ZarReader.Signature);
			if (pictureAt >= 0)
			{
				Int32 position = pictureAt;
				preview = ZarReader.Decode(content, ref position, null, false);
			}

			(Int32 prototypes, Int32 instances) = ReadMap(content);
			return new MisFile(version, teams, ReadTextFile(content), preview, content.Length, prototypes, instances);
		}

		private static Byte[] Inflate(Byte[] data, Int32 start, Int32 size)
		{
			using MemoryStream compressed = new(data, start, data.Length - start, false);
			using ZLibStream inflater = new(compressed, CompressionMode.Decompress);
			Byte[] content = new Byte[size];
			try
			{
				inflater.ReadExactly(content);
			}
			catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException)
			{
				throw new InvalidDataException("The data of the mission cannot be inflated.", exception);
			}

			return content;
		}

		/// <summary>
		/// How many kinds of tile and how many tiles the map manager holds: after its tag, 63 bytes that are not understood, the count of
		/// kinds (one more than there are), a zero, the path of each kind, 33 bytes for each, 28 bytes that are not understood, the count of
		/// regions, and for each region its tag, a count and 56 bytes for each tile. (-1, -1) when it does not fit.
		/// </summary>
		private static (Int32 Prototypes, Int32 Instances) ReadMap(Byte[] content)
		{
			ReadOnlySpan<Byte> span = content;
			ReadOnlySpan<Byte> tag = "<mapManager>\0"u8;
			Int32 at = span.IndexOf(tag);
			if (at < 0)
				return (-1, -1);

			try
			{
				at += tag.Length + 2 + 49 + 12;
				Int32 stored = ReadInt32(span, ref at);
				if (stored < 1 || stored > 1_000_000 || ReadInt32(span, ref at) != 0)
					return (-1, -1);

				Int32 prototypes = stored - 1;
				for (Int32 i = 0; i < prototypes; i++)
				{
					Int32 length = ReadInt32(span, ref at);
					if (length < 0 || length > 32_768 || length > span.Length - at)
						return (-1, -1);

					at += length;
				}

				ReadOnlySpan<Byte> tileTag = "<tile>\0\u0031\u0030\0"u8;
				for (Int32 i = 0; i < prototypes; i++)
				{
					if (!span[at..].StartsWith(tileTag))
						return (-1, -1);

					at += tileTag.Length + 23;
				}

				at += 28;
				Int32 regions = ReadInt32(span, ref at);
				if (regions < 0 || regions > 1_000_000)
					return (-1, -1);

				ReadOnlySpan<Byte> regionTag = "<region>\0\u0038\0"u8;
				Int64 instances = 0;
				for (Int32 i = 0; i < regions; i++)
				{
					if (!span[at..].StartsWith(regionTag))
						return (-1, -1);

					at += regionTag.Length;
					Int32 count = ReadInt32(span, ref at);
					if (count < 0 || (Int64)count * 56 > span.Length - at)
						return (-1, -1);

					at += count * 56;
					instances += count;
				}

				return (prototypes, (Int32)instances);
			}
			catch (ArgumentOutOfRangeException)
			{
				return (-1, -1);
			}
		}

		private static Int32 ReadInt32(ReadOnlySpan<Byte> span, ref Int32 at)
		{
			Int32 value = BinaryPrimitives.ReadInt32LittleEndian(span[at..]);
			at += 4;
			return value;
		}

		/// <summary>The names of the teams, after the tag "&lt;mph&gt;", its zero, a version and a zero.</summary>
		private static List<String> ReadTeams(Byte[] content)
		{
			ReadOnlySpan<Byte> span = content;
			ReadOnlySpan<Byte> tag = "<mph>\0"u8;
			List<String> teams = [];
			if (!span.StartsWith(tag))
				return teams;

			Int32 at = tag.Length;
			Int32 versionEnd = span[at..].IndexOf((Byte)0);
			if (versionEnd < 0 || span.Length - (at + versionEnd + 1) < 4)
				return teams;

			at += versionEnd + 1;
			UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
			at += 4;
			for (Int32 i = 0; i < count && i < MaxTeams && span.Length - at >= 4; i++)
			{
				UInt32 length = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
				at += 4;
				if (length > MaxNameLength || length > span.Length - at)
					break;

				teams.Add(Encoding.Latin1.GetString(span.Slice(at, (Int32)length)));
				at += (Int32)length;
			}

			return teams;
		}

		/// <summary>The path of the text (a 32 bit length, then "locale/..."), which is among the first fields.</summary>
		private static String ReadTextFile(Byte[] content)
		{
			ReadOnlySpan<Byte> span = content.AsSpan(0, Math.Min(content.Length, TextSearchLimit));
			Int32 at = span.IndexOf("locale/"u8);
			if (at < 4)
				return String.Empty;

			UInt32 length = BinaryPrimitives.ReadUInt32LittleEndian(span[(at - 4)..]);
			return length > 0 && length <= MaxNameLength && length <= span.Length - at ? Encoding.Latin1.GetString(span.Slice(at, (Int32)length)) : String.Empty;
		}
	}
}
