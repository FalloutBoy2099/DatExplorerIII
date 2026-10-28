namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models.GameFiles;

	/// <summary>
	/// Reads the sectors of the maps of Arcanum (.sec): the count of lights (32 bits) and the lights (48 bytes each), the 4096 tiles
	/// (32 bits each), a flag (32 bits) that is followed by the 256 roofs when it is 0, a version (32 bits, 0x00AA0000 to 0x00AA0004),
	/// then according to the version the scripts of the tiles (a count and 6 numbers each), the script of the sector (12 bytes), the
	/// town map, the aptitude and the light scheme (4 bytes each) with 12 bytes of sounds, 512 bytes, and the objects, whose count
	/// is the last 32 bits of the file. The objects are counted, not read. The layout is the one of ArcanumFileFormats of iamkisly.
	/// </summary>
	public static class SecReader
	{
		/// <summary>The tiles on a side of a sector.</summary>
		public const Int32 Side = 64;

		private const Int32 LightSize = 48;
		private const Int32 TileCount = Side * Side;
		private const Int32 RoofCount = 256;
		private const Int32 VersionBase = 0xAA0000;
		private const Int32 LastVersion = 4;
		private const Int32 TileScriptSize = 24;

		/// <summary>The kind of a tile: its number, without the 3 bits (9 to 11) that choose among the 8 variants of a floor.</summary>
		public static UInt32 KindOf(UInt32 tile)
		{
			return tile & ~0xE00u;
		}

		/// <summary>True when the data can be a sector: it is large enough for the tiles, and the version is one that is known.</summary>
		public static Boolean IsSec(ReadOnlySpan<Byte> data)
		{
			return TryReadHeader(data, out _, out _, out _);
		}

		/// <exception cref="InvalidDataException">The data is not a valid SEC file.</exception>
		public static SecFile Read(Byte[] data)
		{
			if (!TryReadHeader(data, out Int32 lightCount, out Int32 tilesAt, out Int32 versionAt))
				throw new InvalidDataException("This is not a SEC sector.");

			ReadOnlySpan<Byte> span = data;
			List<SecLight> lights = [];
			for (Int32 i = 0; i < lightCount; i++)
			{
				ReadOnlySpan<Byte> light = span.Slice(4 + (i * LightSize), LightSize);
				lights.Add(new SecLight(
					BinaryPrimitives.ReadInt32LittleEndian(light[8..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[12..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[16..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[20..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[24..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[28..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[32..]),
					BinaryPrimitives.ReadInt32LittleEndian(light[36..])));
			}

			UInt32[] tiles = new UInt32[TileCount];
			for (Int32 i = 0; i < tiles.Length; i++)
			{
				tiles[i] = BinaryPrimitives.ReadUInt32LittleEndian(span[(tilesAt + (i * 4))..]);
			}

			// The flag before the version: the roofs follow it when it is 0.
			Int32 at = tilesAt + (TileCount * 4);
			UInt32[]? roof = null;
			Int32 roofFlag = BinaryPrimitives.ReadInt32LittleEndian(span[at..]);
			at += 4;
			if (roofFlag == 0)
			{
				roof = new UInt32[RoofCount];
				for (Int32 i = 0; i < roof.Length; i++)
				{
					roof[i] = BinaryPrimitives.ReadUInt32LittleEndian(span[(at + (i * 4))..]);
				}

				at += RoofCount * 4;
			}

			Int32 version = BinaryPrimitives.ReadInt32LittleEndian(span[at..]) - VersionBase;
			if (at != versionAt)
				throw new InvalidDataException("The sector is damaged.");

			at += 4;
			List<Int32[]> tileScripts = [];
			if (version >= 1)
			{
				Int32 count = ReadInt32(span, ref at);
				if (count < 0 || (Int64)count * TileScriptSize > span.Length - at)
					throw new InvalidDataException("The scripts of the tiles are impossible.");

				for (Int32 i = 0; i < count; i++)
				{
					Int32[] script = new Int32[6];
					for (Int32 f = 0; f < script.Length; f++)
					{
						script[f] = ReadInt32(span, ref at);
					}

					tileScripts.Add(script);
				}
			}

			Int32 scriptId = 0;
			if (version >= 2)
			{
				// The counters, the flags, then the script.
				Require(span, at, 12);
				scriptId = BinaryPrimitives.ReadInt32LittleEndian(span[(at + 8)..]);
				at += 12;
			}

			Int32 townMap = 0, aptitude = 0, lightScheme = 0;
			if (version >= 3)
			{
				townMap = ReadInt32(span, ref at);
				aptitude = ReadInt32(span, ref at);
				lightScheme = ReadInt32(span, ref at);
				Require(span, at, 12);
				at += 12;
			}

			if (version >= 4)
			{
				Require(span, at, 512);
				at += 512;
			}

			// The count of the objects is the last 32 bits of the file.
			if (span.Length - at < 4)
				throw new InvalidDataException("The sector is cut.");

			Int32 objectCount = BinaryPrimitives.ReadInt32LittleEndian(span[^4..]);
			if (objectCount < 0)
				throw new InvalidDataException("The count of objects is impossible.");

			return new SecFile(version, lights, tiles, roof, tileScripts, scriptId, townMap, aptitude, lightScheme, objectCount, span.Length - 4 - at);
		}

		/// <summary>Checks the count of lights and finds where the tiles and the version are.</summary>
		private static Boolean TryReadHeader(ReadOnlySpan<Byte> data, out Int32 lightCount, out Int32 tilesAt, out Int32 versionAt)
		{
			lightCount = 0;
			tilesAt = 0;
			versionAt = 0;
			if (data.Length < 4)
				return false;

			lightCount = BinaryPrimitives.ReadInt32LittleEndian(data);
			if (lightCount < 0 || (Int64)lightCount * LightSize > data.Length)
				return false;

			tilesAt = 4 + (lightCount * LightSize);
			Int64 flagAt = tilesAt + (TileCount * 4L);
			if (flagAt + 8 > data.Length)
				return false;

			Int32 roofFlag = BinaryPrimitives.ReadInt32LittleEndian(data[(Int32)flagAt..]);
			Int64 at = flagAt + 4 + (roofFlag == 0 ? RoofCount * 4L : 0);
			if (at + 8 > data.Length)
				return false;

			versionAt = (Int32)at;
			Int32 version = BinaryPrimitives.ReadInt32LittleEndian(data[versionAt..]) - VersionBase;
			return version is >= 0 and <= LastVersion;
		}

		private static Int32 ReadInt32(ReadOnlySpan<Byte> span, ref Int32 at)
		{
			Require(span, at, 4);
			Int32 value = BinaryPrimitives.ReadInt32LittleEndian(span[at..]);
			at += 4;
			return value;
		}

		private static void Require(ReadOnlySpan<Byte> span, Int32 at, Int32 count)
		{
			if (span.Length - at < count)
				throw new InvalidDataException("The sector is cut.");
		}
	}
}
