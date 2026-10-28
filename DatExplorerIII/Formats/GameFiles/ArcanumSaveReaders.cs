namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Models.GameFiles;

	/// <summary>
	/// Reads the small files of Arcanum that the saved games and the maps use. The layouts are those of ArcNET of Bia10.
	/// </summary>
	public static class ArcanumSaveReaders
	{
		private const Int32 JumpSize = 32;
		private const Int32 MaxNameLength = 4096;
		private const Int32 MaxDepth = 64;
		private const Int32 MaxGsiVersion = 25;

		/// <summary>
		/// Reads a list of jump points (.jmp): a count (32 bits), then for each a 32 byte record: flags, 4 bytes of zeros, the source
		/// tile (X then Y, 32 bits each), the destination map, 4 bytes of zeros, and the destination tile.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not a valid list.</exception>
		public static JmpFile ReadJmp(Byte[] data)
		{
			if (data.Length < 4)
				throw new InvalidDataException("The file is too short.");

			Int32 count = BinaryPrimitives.ReadInt32LittleEndian(data);
			if (count < 0 || (Int64)count * JumpSize > data.Length - 4)
				throw new InvalidDataException("The count of jump points is impossible.");

			List<JumpPoint> jumps = [];
			for (Int32 i = 0; i < count; i++)
			{
				ReadOnlySpan<Byte> record = data.AsSpan(4 + (i * JumpSize), JumpSize);
				jumps.Add(new JumpPoint(
					BinaryPrimitives.ReadUInt32LittleEndian(record),
					BinaryPrimitives.ReadInt32LittleEndian(record[8..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[12..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[16..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[24..]),
					BinaryPrimitives.ReadInt32LittleEndian(record[28..])));
			}

			return new JmpFile(jumps);
		}

		/// <summary>
		/// Reads the index of a saved game (.tfai): entries that start with a type (32 bits): 0 a file (its name as a 32 bit length and
		/// characters, then its size, 32 bits), 1 a folder (its name; what follows belongs to it), 2 the end of the folder, 3 the end of the
		/// index.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not a valid index.</exception>
		public static TfaiFile ReadTfai(Byte[] data)
		{
			List<TfaiNode> root = [];
			Stack<(String Name, List<TfaiNode> Children)> open = [];
			List<TfaiNode> current = root;
			Int32 at = 0;
			while (true)
			{
				switch (ReadUInt32(data, ref at))
				{
					case 0:
						String fileName = ReadName(data, ref at);
						Int32 size = ReadInt32(data, ref at);
						if (size < 0)
							throw new InvalidDataException("A file of the index has a negative size.");

						current.Add(new TfaiNode(fileName, size, [], false));
						break;

					case 1:
						if (open.Count >= MaxDepth)
							throw new InvalidDataException("The folders of the index are nested too deep.");

						open.Push((ReadName(data, ref at), current));
						current = [];
						break;

					case 2:
						if (open.Count == 0)
							throw new InvalidDataException("The index closes a folder that is not open.");

						(String name, List<TfaiNode> parent) = open.Pop();
						parent.Add(new TfaiNode(name, 0, current, true));
						current = parent;
						break;

					case 3:
						if (open.Count > 0)
							throw new InvalidDataException("The index ends inside a folder.");

						return new TfaiFile(root);

					default:
						throw new InvalidDataException("The index holds an entry of an unknown type.");
				}
			}
		}

		/// <summary>
		/// Reads the information of a saved game (.gsi): a version (0 to 25), the module and the leader (as a 32 bit length and
		/// characters), the map, the days and the milliseconds of the game time, the portrait, the level, the tile of the leader (X then Y,
		/// 32 bits each), the state of the story, and the name of the save.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not valid information of a save.</exception>
		public static GsiFile ReadGsi(Byte[] data)
		{
			Int32 at = 0;
			Int32 version = ReadInt32(data, ref at);
			if (version is < 0 or > MaxGsiVersion)
				throw new InvalidDataException("The version of the saved game is not supported.");

			String module = ReadName(data, ref at);
			String leader = ReadName(data, ref at);
			Int32 map = ReadInt32(data, ref at);
			Int32 days = ReadInt32(data, ref at);
			Int32 milliseconds = ReadInt32(data, ref at);
			Int32 portrait = ReadInt32(data, ref at);
			Int32 level = ReadInt32(data, ref at);
			Int32 x = ReadInt32(data, ref at);
			Int32 y = ReadInt32(data, ref at);
			Int32 story = ReadInt32(data, ref at);
			String display = ReadName(data, ref at);
			return new GsiFile(version, module, leader, display, map, days, milliseconds, portrait, level, x, y, story);
		}

		/// <summary>Reads the fog of a town map (.tmf): bits with no header at all.</summary>
		public static TmfFile ReadTmf(Byte[] data)
		{
			return new TmfFile(data);
		}

		private static UInt32 ReadUInt32(Byte[] data, ref Int32 at)
		{
			if (data.Length - at < 4)
				throw new InvalidDataException("The file ends too early.");

			UInt32 value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
			at += 4;
			return value;
		}

		private static Int32 ReadInt32(Byte[] data, ref Int32 at)
		{
			return (Int32)ReadUInt32(data, ref at);
		}

		/// <summary>A name: a length (32 bits; 0 or less for none) and its characters.</summary>
		private static String ReadName(Byte[] data, ref Int32 at)
		{
			Int32 length = ReadInt32(data, ref at);
			if (length <= 0)
				return String.Empty;

			if (length > MaxNameLength || length > data.Length - at)
				throw new InvalidDataException("A name is impossible.");

			String name = Encoding.Latin1.GetString(data, at, length);
			at += length;
			return name;
		}
	}
}
