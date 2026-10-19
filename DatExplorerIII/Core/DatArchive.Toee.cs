namespace DatExplorerIII.Core
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models;

	/// <summary>
	/// The DAT archives of Temple of Elemental Evil (Troika): the footer is the one of the archives of Arcanum (see
	/// <c>DatArchive.Arcanum.cs</c>), but the index is a tree. All numbers are little endian.
	/// </summary>
	/// <remarks>
	/// <para>The index is the number of entries, then each entry: the length of its name, the name (only the last part of the path,
	/// ended by a zero that the length counts), eight numbers: one that is not used, the flags (1 stored, 2 compressed with zlib,
	/// 0x400 a folder), the size, the compressed size, the position of the data (from the start of the file), the index of the
	/// folder that holds the entry, and the indexes of the first and of the last entry of a folder. An entry of Arcanum has five
	/// numbers, so the size of the entries tells the two formats apart.</para>
	/// <para>The path of a file is the names of its folders, from the first one, then its name. The first entry is the root of
	/// the tree: it has no name worth keeping.</para>
	/// <para>Format: https://rewiki.miraheze.org/wiki/Temple_of_Elemental_Evil_DAT</para>
	/// </remarks>
	public sealed partial class DatArchive
	{
		/// <summary>The numbers after the name of an entry of the tree: eight of four bytes.</summary>
		private const Int32 ToeeEntryFixedSize = 32;

		/// <summary>The numbers after the name of an entry of Arcanum: five of four bytes.</summary>
		private const Int32 ArcanumEntryFixedSize = 20;

		private const Int32 ToeeMaxDepth = 128;

		private readonly record struct ToeeRecord(String Name, Int32 Flags, Int32 UnpackedSize, Int32 PackedSize, UInt32 Offset, Int32 Parent);

		/// <summary>True when the entries of the index are those of Temple of Elemental Evil: they fill the index exactly only with their eight numbers.</summary>
		private static Boolean IsToeeIndex(FileStream stream, ArcanumFooter footer, Int64 length)
		{
			Int32 indexLength = footer.IndexSize - footer.Size;
			if (indexLength < 4 || footer.IndexSize > length)
			{
				return false;
			}

			Byte[] index = new Byte[indexLength];
			stream.Position = length - footer.IndexSize;
			stream.ReadExactly(index);

			Int32 count = BinaryPrimitives.ReadInt32LittleEndian(index);
			if (count <= 0)
			{
				return false;
			}

			return !FillsIndex(index, count, ArcanumEntryFixedSize) && FillsIndex(index, count, ToeeEntryFixedSize);
		}

		/// <summary>True when <paramref name="count"/> entries with this size of numbers end exactly at the end of the index.</summary>
		private static Boolean FillsIndex(Byte[] index, Int32 count, Int32 fixedSize)
		{
			Int64 position = 4;
			for (Int32 i = 0; i < count; i++)
			{
				if (position + 4 > index.Length)
				{
					return false;
				}

				Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan((Int32)position));
				if (nameLength < 0)
				{
					return false;
				}

				position += 4L + nameLength + fixedSize;
				if (position > index.Length)
				{
					return false;
				}
			}

			return position == index.Length;
		}

		private void ReadToee(FileStream stream, ArcanumFooter footer, Int64 length, DatOpenMode mode)
		{
			_arcanumGuid = footer.HasGuid ? footer.GuidBytes : null;
			_treeSize = footer.IndexSize;
			_archiveEnd = length;
			_dataLimit = length - footer.IndexSize;

			Byte[] index = new Byte[footer.IndexSize - footer.Size];
			stream.Position = _dataLimit;
			stream.ReadExactly(index);

			Int32 count = BinaryPrimitives.ReadInt32LittleEndian(index);
			List<ToeeRecord> records = new(Math.Max(count, 0));
			Int32 position = 4;
			while (records.Count < count && position + 4 <= index.Length)
			{
				Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position));
				if (nameLength < 0 || (Int64)position + 4 + nameLength + ToeeEntryFixedSize > index.Length)
				{
					break;
				}

				position += 4;
				String name = NameEncoding.GetString(index, position, nameLength).TrimEnd('\0');
				position += nameLength;

				// The first number is not used; the last two (the first and the last entry of a folder) are not needed to read.
				Int32 flags = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 4));
				Int32 unpackedSize = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 8));
				Int32 packedSize = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 12));
				UInt32 offset = BinaryPrimitives.ReadUInt32LittleEndian(index.AsSpan(position + 16));
				Int32 parent = BinaryPrimitives.ReadInt32LittleEndian(index.AsSpan(position + 20));
				position += ToeeEntryFixedSize;

				records.Add(new ToeeRecord(name, flags, unpackedSize, packedSize, offset, parent));
			}

			if (records.Count < count)
			{
				if (mode == DatOpenMode.Strict)
				{
					throw new DatException(DatErrorKind.InvalidTree, "The index of the DAT file is damaged.");
				}

				_issues.Add(new DatIssue(DatIssueKind.TruncatedTree, null, records.Count));
			}

			foreach (ToeeRecord record in records)
			{
				String directory = ToeeDirectoryOf(records, record.Parent);
				String? part = ToeePart(record.Name);

				if ((record.Flags & ArcanumFlagFolder) != 0)
				{
					String folder = part == null ? directory : directory + part + '\\';
					if (folder.Length > 0)
					{
						RegisterFolder(folder);
					}

					continue;
				}

				if (part == null)
				{
					continue;
				}

				Boolean compressed = (record.Flags & ArcanumFlagCompressed) != 0;
				Register(CreateEntry(directory + part, compressed, record.UnpackedSize, compressed ? record.PackedSize : record.UnpackedSize, record.Offset), true);
			}
		}

		/// <summary>A name of the tree as a part of a path; null for the names of the root ("", ".").</summary>
		private static String? ToeePart(String name)
		{
			String trimmed = name.Trim('\\', '/');
			return trimmed.Length == 0 || trimmed == "." ? null : trimmed;
		}

		/// <summary>The path of the folder that holds an entry (ending with a backslash; empty for the root), from the chain of the folders above it.</summary>
		private static String ToeeDirectoryOf(List<ToeeRecord> records, Int32 parent)
		{
			List<String> parts = new();
			for (Int32 depth = 0; parent >= 0 && parent < records.Count && depth < ToeeMaxDepth; depth++)
			{
				ToeeRecord folder = records[parent];
				if (ToeePart(folder.Name) is { } part)
				{
					parts.Add(part);
				}

				parent = folder.Parent;
			}

			parts.Reverse();
			return parts.Count == 0 ? String.Empty : String.Join('\\', parts) + '\\';
		}
	}
}