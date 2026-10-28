namespace DatExplorerIII.Core
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Models.Archives;

	/// <summary>
	/// The DAT archives of Arcanum (Troika): the data of the files, then an index, then a footer. All numbers are little endian.
	/// </summary>
	/// <remarks>
	/// <para>The index is the number of entries, then each entry: the length of its name, the name (backslashes, ended by a zero
	/// that the length counts), a number that is not used, flags (1 stored, 2 compressed with zlib, 0x400 a folder), the size,
	/// the compressed size and the position of the data (from the start of the file). A folder has no data. The index holds the
	/// folders of the archive, empty ones included.</para>
	/// <para>The footer is a GUID of 16 bytes (the identity of a module, which saved games remember), the signature "1TAD",
	/// the total length of the names, and the length of the index and the footer together. Some archives have no GUID: their
	/// signature is " TAD" and the footer is 12 bytes. The archives of the beta versions have no length of the names: the footer is
	/// the GUID, the signature and the length of the index and the footer together (8 bytes without the GUID).</para>
	/// </remarks>
	public sealed partial class DatArchive
	{
		private const UInt32 ArcanumGuidMagic = 0x44415431;
		private const UInt32 ArcanumPlainMagic = 0x44415420;
		private const Int32 ArcanumTailSize = 12;
		private const Int32 ArcanumLegacyTailSize = 8;
		private const Int32 ArcanumGuidSize = 16;
		private const Int32 ArcanumFlagStored = 0x01;
		private const Int32 ArcanumFlagCompressed = 0x02;
		private const Int32 ArcanumFlagFolder = 0x400;

		/// <summary>The longest path of the engine of the game.</summary>
		internal const Int32 ArcanumMaxPathLength = 259;

		/// <summary>The identity of the archive (the GUID of its footer); it is kept when the archive is written again.</summary>
		private Byte[]? _arcanumGuid;

		private readonly record struct ArcanumFooter(Boolean HasGuid, Int32 NameTableSize, Int32 IndexSize, Byte[] GuidBytes, Boolean IsLegacy = false)
		{
			public Int32 Size => (HasGuid ? ArcanumGuidSize : 0) + (IsLegacy ? ArcanumLegacyTailSize : ArcanumTailSize);
		}

		/// <summary>Reads the end of the file; true when it is the footer of an Arcanum archive.</summary>
		private static Boolean TryReadArcanumFooter(FileStream stream, Int64 length, out ArcanumFooter footer)
		{
			footer = default;
			if (length < ArcanumTailSize)
				return false;

			Byte[] tail = new Byte[ArcanumTailSize];
			stream.Position = length - ArcanumTailSize;
			stream.ReadExactly(tail);

			UInt32 magic = BinaryPrimitives.ReadUInt32LittleEndian(tail);
			if (magic != ArcanumGuidMagic && magic != ArcanumPlainMagic)
				return TryReadLegacyArcanumFooter(stream, length, tail, out footer);

			Boolean hasGuid = magic == ArcanumGuidMagic;
			Int64 footerSize = hasGuid ? ArcanumGuidSize + ArcanumTailSize : ArcanumTailSize;
			UInt32 nameTableSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(4));
			UInt32 indexSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(8));

			// The length counts the entry count (4 bytes) and the footer; the names are part of the entries.
			if (length < footerSize || indexSize < footerSize + 4 || indexSize > length || nameTableSize > indexSize)
				return false;

			Byte[] guid = new Byte[ArcanumGuidSize];
			if (hasGuid)
			{
				stream.Position = length - footerSize;
				stream.ReadExactly(guid);
			}

			footer = new ArcanumFooter(hasGuid, (Int32)nameTableSize, (Int32)indexSize, guid);
			return true;
		}

		/// <summary>The footer of the beta versions: the signature, then the length of the index and the footer; the names have no length.</summary>
		private static Boolean TryReadLegacyArcanumFooter(FileStream stream, Int64 length, Byte[] tail, out ArcanumFooter footer)
		{
			footer = default;
			UInt32 magic = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(4));
			if (magic != ArcanumGuidMagic && magic != ArcanumPlainMagic)
				return false;

			Boolean hasGuid = magic == ArcanumGuidMagic;
			Int64 footerSize = (hasGuid ? ArcanumGuidSize : 0) + ArcanumLegacyTailSize;
			UInt32 indexSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(8));
			if (length < footerSize || indexSize < footerSize + 4 || indexSize > length)
				return false;

			Byte[] guid = new Byte[ArcanumGuidSize];
			if (hasGuid)
			{
				stream.Position = length - footerSize;
				stream.ReadExactly(guid);
			}

			footer = new ArcanumFooter(hasGuid, 0, (Int32)indexSize, guid, true);
			return true;
		}

		private static DatArchive ReadArcanumArchive(String filePath, FileStream stream, ArcanumFooter footer, Int64 length, DatOpenMode mode)
		{
			if (IsToeeIndex(stream, footer, length))
			{
				DatArchive toee = new(filePath, DatFormat.Toee);
				toee.ReadToee(stream, footer, length, mode);
				toee.FinishReading(mode);
				return toee;
			}

			DatArchive arcanum = new(filePath, DatFormat.Arcanum);
			arcanum.ReadArcanum(stream, footer, length, mode);
			arcanum.FinishReading(mode);
			return arcanum;
		}

		private void ReadArcanum(FileStream stream, ArcanumFooter footer, Int64 length, DatOpenMode mode)
		{
			_arcanumGuid = footer.HasGuid ? footer.GuidBytes : null;
			_treeSize = footer.IndexSize;
			_archiveEnd = length;
			_dataLimit = length - footer.IndexSize;

			Byte[] index = new Byte[footer.IndexSize - footer.Size];
			stream.Position = _dataLimit;
			stream.ReadExactly(index);

			Int32 count = BinaryPrimitives.ReadInt32LittleEndian(index);
			Int32 position = 4;
			Int32 parsed = 0;

			while (parsed < count && TryReadArcanumEntry(index, ref position, out DatEntry? entry, out String? folder))
			{
				if (folder != null)
				{
					RegisterFolder(folder);
				}
				else
				{
					Register(entry!, true);
				}

				parsed++;
			}

			if (count < 0 || parsed < count)
			{
				if (mode == DatOpenMode.Strict)
					throw new DatException(DatErrorKind.InvalidTree, "The index of the DAT file is damaged.");

				_issues.Add(new DatIssue(DatIssueKind.TruncatedTree, null, parsed));
			}
		}

		/// <param name="folder">The path of the folder (ending with a backslash) when the entry is a folder; the entry is then null.</param>
		private static Boolean TryReadArcanumEntry(Byte[] index, ref Int32 position, out DatEntry? entry, out String? folder)
		{
			entry = null;
			folder = null;
			if (position + 4 > index.Length)
				return false;

			Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position));
			if (nameLength <= 0 || (Int64)position + 4 + nameLength + 20 > index.Length)
				return false;

			position += 4;
			String path = NameEncoding.GetString(index, position, nameLength).TrimEnd('\0');
			position += nameLength;

			// The first number is not used.
			Int32 flags = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 4));
			Int32 unpackedSize = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 8));
			Int32 packedSize = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 12));
			UInt32 offset = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(position + 16));
			position += 20;

			if ((flags & ArcanumFlagFolder) != 0)
			{
				folder = NormalizeDirectory(path);
				return true;
			}

			Boolean compressed = (flags & ArcanumFlagCompressed) != 0;
			entry = CreateEntry(path, compressed, unpackedSize, compressed ? packedSize : unpackedSize, offset);
			return true;
		}

		private static String ArcanumIndexKey(String path)
		{
			Byte[] bytes = NameEncoding.GetBytes(path);
			for (Int32 index = 0; index < bytes.Length; index++)
			{
				if (bytes[index] >= (Byte)'A' && bytes[index] <= (Byte)'Z')
					bytes[index] += (Byte)('a' - 'A');
			}
			return Encoding.Latin1.GetString(bytes);
		}

		/// <summary>
		/// Writes the data of the files, then the index in path order and
		/// the footer.
		/// </summary>
		private SavePlan WriteArcanum(Stream output, IProgress<DatProgress>? progress)
		{
			SavePlan plan = WriteDataBlock(output, progress);

			using MemoryStream index = new();
			Int32 nameTableSize = 0;
			using (BinaryWriter writer = new(index, NameEncoding, true))
			{
				List<(String Path, DatEntry? Entry)> records = _folders.Values
					.Select(folder => (Path: folder.TrimEnd('\\'), Entry: (DatEntry?)null))
					.Concat(_entries.Select(entry => (Path: entry.FullPath, Entry: (DatEntry?)entry)))
					.OrderBy(record => ArcanumIndexKey(record.Path), StringComparer.Ordinal)
					.ToList();
				writer.Write(records.Count);

				void WriteEntry(String path, Int32 flags, Int32 unpackedSize, Int32 packedSize, UInt32 offset)
				{
					Byte[] name = NameEncoding.GetBytes(path + '\0');
					writer.Write(name.Length);
					writer.Write(name);
					writer.Write(0);
					writer.Write(flags);
					writer.Write(unpackedSize);
					writer.Write(packedSize);
					writer.Write(offset);
					nameTableSize += name.Length;
				}

				foreach ((String path, DatEntry? entry) in records)
				{
					if (entry == null)
					{
						WriteEntry(path, ArcanumFlagFolder, 0, 0, 0);
						continue;
					}

					Placement placement = plan.Placements[entry];
					WriteEntry(path, placement.IsCompressed ? ArcanumFlagCompressed : ArcanumFlagStored,
						placement.UnpackedSize, placement.IsCompressed ? placement.PackedSize : placement.UnpackedSize,
						placement.Offset);
				}
			}

			Int64 archiveSize = output.Position + 4 + index.Length + ArcanumGuidSize + ArcanumTailSize;
			if (archiveSize > UInt32.MaxValue)
				throw new DatException(DatErrorKind.ArchiveTooLarge, "The archive would exceed 4 GB.");

			Byte[] marker = new Byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(marker, checked((UInt32)(output.Position + 4)));
			output.Write(marker);
			index.Position = 0;
			index.CopyTo(output);

			Byte[] footer = new Byte[ArcanumGuidSize + ArcanumTailSize];
			(_arcanumGuid ?? Guid.NewGuid().ToByteArray()).CopyTo(footer, 0);
			BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(ArcanumGuidSize), ArcanumGuidMagic);
			BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(ArcanumGuidSize + 4), nameTableSize);
			BinaryPrimitives.WriteInt32LittleEndian(footer.AsSpan(ArcanumGuidSize + 8), (Int32)index.Length + footer.Length);
			output.Write(footer);

			_arcanumGuid = footer.AsSpan(0, ArcanumGuidSize).ToArray();
			plan.TreeSize = (Int32)index.Length + footer.Length;
			return plan;
		}
	}
}