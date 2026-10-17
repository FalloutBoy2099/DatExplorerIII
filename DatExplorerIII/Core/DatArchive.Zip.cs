namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using Models;

	/// <summary>
	/// ZIP files, browsed like a DAT archive: the folders and the files of the ZIP are those of the archive, and the data is read
	/// from the ZIP. They can be browsed, viewed and extracted, not saved.
	/// </summary>
	public sealed partial class DatArchive
	{
		/// <summary>The archive as a ZIP (null for the other formats), and the entry of the ZIP that holds the data of each file.</summary>
		private ZipArchive? _zip;

		private Dictionary<DatEntry, ZipArchiveEntry>? _zipEntries;

		/// <summary>True when the file starts like a ZIP: "PK", then 3 4 (a file), 5 6 (an empty ZIP) or 7 8 (a ZIP in several parts).</summary>
		private static Boolean IsZipFile(FileStream stream, Int64 length)
		{
			if (length < 4)
			{
				return false;
			}

			Byte[] header = new Byte[4];
			stream.Position = 0;
			stream.ReadExactly(header);
			return header[0] == (Byte)'P' && header[1] == (Byte)'K' && ((header[2] == 3 && header[3] == 4) || (header[2] == 5 && header[3] == 6) || (header[2] == 7 && header[3] == 8));
		}

		private static DatArchive ReadZipArchive(String filePath, FileStream stream, Int64 length)
		{
			DatArchive archive = new(filePath, DatFormat.Zip);
			try
			{
				archive.ReadZip(stream, length);
			}
			catch (InvalidDataException exception)
			{
				throw new DatException(DatErrorKind.NotADat, "The file is not a valid ZIP archive.", exception);
			}

			return archive;
		}

		private void ReadZip(FileStream stream, Int64 length)
		{
			_zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
			_zipEntries = new Dictionary<DatEntry, ZipArchiveEntry>();
			_archiveEnd = length;
			_dataLimit = Int64.MaxValue;

			foreach (ZipArchiveEntry zipEntry in _zip.Entries)
			{
				String path = zipEntry.FullName.Replace('/', '\\');

				// A folder of the ZIP is an entry whose name ends with a separator.
				if (path.EndsWith('\\'))
				{
					RegisterFolder(NormalizeDirectory(path));
					continue;
				}

				Int64 unpacked = zipEntry.Length;
				Int64 packed = zipEntry.CompressedLength;
				Boolean compressed = packed != unpacked;
				DatEntry entry = CreateEntry(path, compressed, unpacked > Int32.MaxValue ? -1 : (Int32)unpacked, packed > Int32.MaxValue ? -1 : (Int32)packed, 0);
				Register(entry, true);
				_zipEntries[entry] = zipEntry;
			}

			FinishReading(DatOpenMode.Strict);
		}

		/// <summary>Reads the content of a file of a ZIP.</summary>
		private Byte[] ReadZipData(DatEntry entry)
		{
			if (entry.IsDamaged)
			{
				throw new DatException(DatErrorKind.OutsideArchive, $"The file '{entry.FullPath}' is damaged.", entry.FullPath);
			}

			lock (_streamLock)
			{
				if (_zipEntries == null || !_zipEntries.TryGetValue(entry, out ZipArchiveEntry? zipEntry))
				{
					throw new DatException(DatErrorKind.NotOnDisk, "The archive is not stored on disk.");
				}

				try
				{
					using Stream source = zipEntry.Open();
					using MemoryStream result = new(Math.Min(entry.UnpackedSize, 1 << 20));

					// The announced size is the limit: a ZIP that lies about it does not fill the memory.
					Byte[] buffer = new Byte[81920];
					Int32 read;
					while ((read = source.Read(buffer, 0, (Int32)Math.Min(buffer.Length, entry.UnpackedSize - result.Length + 1))) > 0)
					{
						result.Write(buffer, 0, read);
						if (result.Length > entry.UnpackedSize)
						{
							break;
						}
					}

					if (result.Length != entry.UnpackedSize)
					{
						throw new InvalidDataException("The size of the file is not the one announced.");
					}

					return result.ToArray();
				}
				catch (Exception exception) when (exception is InvalidDataException or IOException or NotSupportedException or ObjectDisposedException)
				{
					throw new DatException(DatErrorKind.CorruptData, $"The data of '{entry.FullPath}' is corrupted.", exception, entry.FullPath);
				}
			}
		}
	}
}
