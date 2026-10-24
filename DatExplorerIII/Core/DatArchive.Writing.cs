namespace DatExplorerIII.Core
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading.Tasks;
	using Models.Archives;

	public sealed partial class DatArchive
	{
		/// <summary>Where the data of a file ends up in the saved archive.</summary>
		private readonly record struct Placement(UInt32 Offset, Int32 UnpackedSize, Int32 PackedSize, Boolean IsCompressed);

		private sealed class SavePlan
		{
			public Dictionary<DatEntry, Placement> Placements { get; } = [];

			public Int32 TreeSize { get; set; }
		}

		/// <summary>
		/// Writes the pending changes to disk.
		/// </summary>
		/// <param name="quick">
		/// Fallout 2 only: appends the new files and rewrites the directory tree in place instead of rebuilding the
		/// whole archive. Much faster, but the space of deleted files is not reclaimed and an interrupted save
		/// corrupts the archive. Ignored for an archive that was recovered.
		/// </param>
		/// <param name="progress">Receives the file being written.</param>
		/// <exception cref="DatException">The archive cannot be written.</exception>
		public void Save(Boolean quick = false, IProgress<DatProgress>? progress = null)
		{
			DatPendingChanges changes = PendingChanges;
			if (changes == DatPendingChanges.None)
				return;

			if (_sourceFolder != null)
				throw new InvalidOperationException("An archive made from a folder is saved with SaveAs.");

			PrepareToSave();

			Boolean wasRepair = _forceRebuild;
			Boolean rewriteInPlace = Format == DatFormat.Fallout2
				&& !_forceRebuild
				&& _stream is { CanWrite: true }
				&& (quick || (changes & (DatPendingChanges.Added | DatPendingChanges.Deleted | DatPendingChanges.Created)) == 0);

			SavePlan plan = rewriteInPlace ? RewriteFallout2Tail(progress) : Rebuild(progress);
			CompleteSave(plan, wasRepair);
		}

		/// <summary>
		/// Writes the archive, complete, to another file; the file the archive was read from (or the folder it was made from) is not
		/// changed. The archive then is that new file. An archive made from a folder can be given another format.
		/// </summary>
		/// <param name="path">The new file. It is replaced when it exists.</param>
		/// <param name="format">The format of the new file; only an archive made from a folder (or with no stored file) can change it.</param>
		/// <param name="progress">Receives the file being written.</param>
		/// <exception cref="DatException">The archive cannot be written.</exception>
		public void SaveAs(String path, DatFormat? format = null, IProgress<DatProgress>? progress = null)
		{
			String target = Path.GetFullPath(path);
			if (Directory.Exists(target))
				throw new DatException(DatErrorKind.Io, $"'{target}' is a folder.", $"'{target}' is a folder.");

			DatFormat newFormat = format ?? Format;
			if (newFormat != Format && _entries.Any(entry => !entry.IsNew))
				throw new InvalidOperationException("The format of an archive that holds stored files cannot change.");

			if (_entries.Any(entry => entry.IsDamaged))
				throw new DatException(DatErrorKind.DamagedEntriesPresent, "The damaged files must be removed before the archive is saved.");

			String previousPath = FilePath;
			DatFormat previousFormat = Format;
			Boolean wasRepair = _forceRebuild;
			Boolean repairPending = _repairPending;

			try
			{
				Format = newFormat;
				PrepareToSave();

				// The original file is kept as it is: there is nothing to put aside, whatever was repaired.
				_repairPending = false;
				FilePath = target;

				SavePlan plan = Rebuild(progress);
				CompleteSave(plan, wasRepair);
				_sourceFolder = null;
			}
			catch
			{
				FilePath = previousPath;
				Format = previousFormat;
				_repairPending = repairPending;
				lock (_streamLock)
				{
					if (_stream == null && File.Exists(FilePath))
						ReopenStream();
				}

				throw;
			}
		}

		private void PrepareToSave()
		{
			if (_entries.Any(entry => entry.IsDamaged))
				throw new DatException(DatErrorKind.DamagedEntriesPresent, "The damaged files must be removed before the archive is saved.");

			if (IsReadOnly)
				throw new DatException(DatErrorKind.ReadOnlyFormat, "This kind of archive cannot be saved yet.");

			ValidateForSaving();

			// Sorted as the game engine expects it (binary search by path).
			_entries.Sort((a, b) => String.CompareOrdinal(a.Key, b.Key));
		}

		/// <summary>The archive is on disk as it was planned: the files know where their data is now.</summary>
		private void CompleteSave(SavePlan plan, Boolean wasRepair)
		{
			foreach (KeyValuePair<DatEntry, Placement> pair in plan.Placements)
			{
				DatEntry entry = pair.Key;
				Placement placement = pair.Value;
				entry.Offset = placement.Offset;
				entry.UnpackedSize = placement.UnpackedSize;
				entry.PackedSize = placement.PackedSize;
				entry.IsCompressed = placement.IsCompressed;
				entry.SourceFile = null;
			}

			_treeSize = plan.TreeSize;
			lock (_streamLock)
			{
				_archiveEnd = _stream?.Length ?? 0;
				_dataLimit = Format switch
				{
					DatFormat.Fallout2 => _archiveEnd - _treeSize - Fallout2FooterSize,
					DatFormat.Arcanum => _archiveEnd - _treeSize,
					_ => _archiveEnd,
				};
			}

			_newCount = 0;
			_renamed = false;
			_deletedStored = false;
			_repairPending = false;
			_forceRebuild = false;

			if (wasRepair)
			{
				_issues.Clear();
				IsRecovered = false;
			}
		}

		private void ValidateForSaving()
		{
			foreach (DatEntry entry in _entries)
			{
				Boolean tooLong = Format switch
				{
					DatFormat.Fallout1 => entry.Name.Length > MaxNameLength || entry.Directory.Length > MaxNameLength + 1,
					DatFormat.Arcanum => entry.FullPath.Length > ArcanumMaxPathLength,
					_ => entry.FullPath.Length > Int16.MaxValue,
				};

				if (tooLong)
					throw new DatException(DatErrorKind.PathTooLong, $"The path '{entry.FullPath}' is too long.", entry.FullPath);
			}
		}

		#region Whole archive

		/// <summary>
		/// Writes a new file next to the archive, then replaces the archive with it.
		/// A repaired archive keeps the original file as a backup.
		/// </summary>
		private SavePlan Rebuild(IProgress<DatProgress>? progress)
		{
			String temporary = FilePath + ".tmp";
			SavePlan plan;

			try
			{
				using (FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
				{
					plan = Format switch
					{
						DatFormat.Fallout1 => WriteFallout1(output, progress),
						DatFormat.Arcanum => WriteArcanum(output, progress),
						_ => WriteFallout2(output, progress),
					};
					output.Flush(true);
				}
			}
			catch
			{
				TryDelete(temporary);
				throw;
			}

			lock (_streamLock)
			{
				_stream?.Dispose();
				_stream = null;

				try
				{
					ReplaceFile(temporary);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					TryDelete(temporary);
					ReopenStream();
					throw new DatException(DatErrorKind.Io, exception.Message, exception, exception.Message);
				}

				ReopenStream();
			}

			return plan;
		}

		/// <summary>
		/// Puts the new file in place of the archive. For a repaired archive the original is moved aside first.
		/// </summary>
		private void ReplaceFile(String temporary)
		{
			if (!_repairPending || !File.Exists(FilePath))
			{
				File.Move(temporary, FilePath, true);
				return;
			}

			String backup = GetBackupPath();
			File.Move(FilePath, backup);

			try
			{
				File.Move(temporary, FilePath);
			}
			catch
			{
				File.Move(backup, FilePath);
				throw;
			}

			BackupPath = backup;
		}

		private String GetBackupPath()
		{
			String backup = FilePath + ".bak";
			for (Int32 number = 2; File.Exists(backup); number++)
			{
				backup = FilePath + ".bak" + number;
			}

			return backup;
		}

		private void ReopenStream()
		{
			if (File.Exists(FilePath))
				_stream = OpenStream(FilePath);
		}

		private static void TryDelete(String path)
		{
			try
			{
				File.Delete(path);
			}
			catch (IOException)
			{
				// The temporary file is removed by the next save.
			}
			catch (UnauthorizedAccessException)
			{
				// Same as above.
			}
		}

		/// <summary>
		/// Writes the data of every file: the stored ones in their current order (sequential reads), then the new ones.
		/// </summary>
		private SavePlan WriteDataBlock(Stream output, IProgress<DatProgress>? progress)
		{
			SavePlan plan = new();
			List<DatEntry> ordered = _entries
				.Where(entry => !entry.IsNew)
				.OrderBy(entry => entry.Offset)
				.Concat(_entries.Where(entry => entry.IsNew))
				.ToList();

			DatProgressThrottle throttle = new(progress, ordered.Count + 1);

			Int32 position = 0;
			while (position < ordered.Count)
			{
				if (!ordered[position].IsNew)
				{
					DatEntry stored = ordered[position];
					throttle.Report(stored.FullPath, position + 1);
					plan.Placements[stored] = WriteFile(output, stored);
					position++;
					continue;
				}

				// The new files are compressed several at a time (this is what takes the time), then written in order.
				List<DatEntry> batch = TakeBatch(ordered, position);
				Prepared[] prepared = PrepareNewFiles(batch);
				for (Int32 index = 0; index < batch.Count; index++)
				{
					throttle.Report(batch[index].FullPath, position + index + 1);
					plan.Placements[batch[index]] = WritePrepared(output, prepared[index]);
				}

				position += batch.Count;
			}

			progress?.Report(new DatProgress(String.Empty, ordered.Count + 1, ordered.Count + 1));
			return plan;
		}

		private Placement WriteFile(Stream output, DatEntry entry)
		{
			Int64 position = output.Position;
			if (position > UInt32.MaxValue)
				throw new DatException(DatErrorKind.ArchiveTooLarge, "The archive would exceed 4 GB.");

			if (!entry.IsNew)
			{
				output.Write(ReadStored(entry));
				return new Placement((UInt32)position, entry.UnpackedSize, entry.PackedSize, entry.IsCompressed);
			}

			return WritePrepared(output, PrepareNewFile(entry));
		}

		/// <summary>The bytes of a new file as they are written, with their sizes.</summary>
		private readonly record struct Prepared(Byte[] Bytes, Int32 UnpackedSize, Int32 PackedSize, Boolean IsCompressed);

		/// <summary>The most files, and the most bytes, that are read and compressed at the same time.</summary>
		private const Int32 MaxBatchFiles = 64;

		private const Int64 MaxBatchBytes = 64L * 1024 * 1024;

		private static List<DatEntry> TakeBatch(List<DatEntry> ordered, Int32 start)
		{
			List<DatEntry> batch = [];
			Int64 bytes = 0;
			for (Int32 index = start; index < ordered.Count && ordered[index].IsNew && batch.Count < MaxBatchFiles; index++)
			{
				if (batch.Count > 0 && bytes + ordered[index].UnpackedSize > MaxBatchBytes)
					break;

				batch.Add(ordered[index]);
				bytes += ordered[index].UnpackedSize;
			}

			return batch;
		}

		private Prepared[] PrepareNewFiles(List<DatEntry> batch)
		{
			Prepared[] prepared = new Prepared[batch.Count];
			try
			{
				Parallel.For(0, batch.Count, index => prepared[index] = PrepareNewFile(batch[index]));
			}
			catch (AggregateException exception) when (exception.InnerException != null)
			{
				// The caller handles the error of the file, not the wrapper of the loop.
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			}

			return prepared;
		}

		private Prepared PrepareNewFile(DatEntry entry)
		{
			Byte[] data = File.ReadAllBytes(entry.SourceFile!);

			if (Format != DatFormat.Fallout1 && data.Length > 0)
			{
				Byte[] compressed = DeflateZlib(data);
				if (compressed.Length < data.Length)
					return new Prepared(compressed, data.Length, compressed.Length, true);
			}

			// Fallout 1 compression is not supported: the data is stored as is, without packed size.
			return new Prepared(data, data.Length, Format == DatFormat.Fallout1 ? 0 : data.Length, false);
		}

		private static Placement WritePrepared(Stream output, Prepared prepared)
		{
			Int64 position = output.Position;
			if (position > UInt32.MaxValue)
				throw new DatException(DatErrorKind.ArchiveTooLarge, "The archive would exceed 4 GB.");

			output.Write(prepared.Bytes);
			return new Placement((UInt32)position, prepared.UnpackedSize, prepared.PackedSize, prepared.IsCompressed);
		}

		private static Byte[] DeflateZlib(Byte[] data)
		{
			using MemoryStream target = new();
			using (ZLibStream deflater = new(target, CompressionLevel.SmallestSize, true))
			{
				deflater.Write(data);
			}

			return target.ToArray();
		}

		#endregion

		#region Fallout 2

		private SavePlan WriteFallout2(Stream output, IProgress<DatProgress>? progress)
		{
			SavePlan plan = WriteDataBlock(output, progress);
			WriteFallout2Tree(output, _entries, plan);
			return plan;
		}

		/// <summary>
		/// Appends the new files over the old directory tree, then writes the tree again.
		/// </summary>
		private SavePlan RewriteFallout2Tail(IProgress<DatProgress>? progress)
		{
			lock (_streamLock)
			{
				FileStream stream = _stream!;
				SavePlan plan = new();

				// The old tree starts where the data ends; bytes after the archive are dropped by the final SetLength.
				stream.Position = _dataLimit;

				List<DatEntry> added = _entries.Where(entry => entry.IsNew).ToList();
				DatProgressThrottle throttle = new(progress, added.Count + 1);

				foreach (DatEntry entry in _entries.Where(entry => !entry.IsNew))
				{
					plan.Placements[entry] = new Placement(entry.Offset, entry.UnpackedSize, entry.PackedSize, entry.IsCompressed);
				}

				for (Int32 index = 0; index < added.Count; index++)
				{
					throttle.Report(added[index].FullPath, index + 1);
					plan.Placements[added[index]] = WriteFile(stream, added[index]);
				}

				progress?.Report(new DatProgress(String.Empty, added.Count + 1, added.Count + 1));

				WriteFallout2Tree(stream, _entries, plan);
				stream.SetLength(stream.Position);
				stream.Flush(true);
				return plan;
			}
		}

		/// <summary>
		/// Writes [file count][entries][tree size][archive size] at the current position.
		/// </summary>
		private static void WriteFallout2Tree(Stream output, IReadOnlyList<DatEntry> entries, SavePlan plan)
		{
			using MemoryStream tree = new();
			using (BinaryWriter writer = new(tree, NameEncoding, true))
			{
				writer.Write(entries.Count);

				foreach (DatEntry entry in entries)
				{
					Placement placement = plan.Placements[entry];
					Byte[] name = NameEncoding.GetBytes(entry.FullPath);

					writer.Write(name.Length);
					writer.Write(name);
					writer.Write((Byte)(placement.IsCompressed ? 1 : 0));
					writer.Write(placement.UnpackedSize);
					writer.Write(placement.PackedSize);
					writer.Write(placement.Offset);
				}
			}

			Int64 archiveSize = output.Position + tree.Length + Fallout2FooterSize;
			if (archiveSize > UInt32.MaxValue)
				throw new DatException(DatErrorKind.ArchiveTooLarge, "The archive would exceed 4 GB.");

			tree.Position = 0;
			tree.CopyTo(output);

			Byte[] footer = new Byte[Fallout2FooterSize];
			BinaryPrimitives.WriteInt32LittleEndian(footer, (Int32)tree.Length);
			BinaryPrimitives.WriteUInt32LittleEndian(footer.AsSpan(4), (UInt32)archiveSize);
			output.Write(footer);

			plan.TreeSize = (Int32)tree.Length;
		}

		#endregion

		#region Fallout 1

		/// <summary>
		/// Writes [header][directory names][directory contents][file data].
		/// The index is written last because it contains the position of the data.
		/// </summary>
		private SavePlan WriteFallout1(FileStream output, IProgress<DatProgress>? progress)
		{
			SortedDictionary<String, List<DatEntry>> directories = new(StringComparer.Ordinal);
			foreach (DatEntry entry in _entries)
			{
				String directory = ToUpperAscii(entry.Directory.TrimEnd('\\'));
				if (directory.Length == 0)
					directory = ".";

				if (!directories.TryGetValue(directory, out List<DatEntry>? list))
				{
					list = [];
					directories.Add(directory, list);
				}

				list.Add(entry);
			}

			Int32 indexSize = Fallout1HeaderSize;
			foreach (KeyValuePair<String, List<DatEntry>> pair in directories)
			{
				indexSize += 1 + pair.Key.Length + 16;
				indexSize += pair.Value.Sum(entry => 1 + entry.Name.Length + 16);
			}

			output.Write(new Byte[indexSize]);
			SavePlan plan = WriteDataBlock(output, progress);

			Byte[] index = BuildFallout1Index(directories, plan, indexSize);
			output.Position = 0;
			output.Write(index);
			output.Position = output.Length;
			return plan;
		}

		/// <summary>
		/// Upper case for the letters A to Z only: other characters have no upper case in the single byte character set
		/// of a DAT file (the upper case of 'ÿ' is not even a Latin 1 character).
		/// </summary>
		internal static String ToUpperAscii(String text)
		{
			return String.Create(text.Length, text, (span, source) =>
			{
				for (Int32 i = 0; i < source.Length; i++)
				{
					Char character = source[i];
					span[i] = character is >= 'a' and <= 'z' ? (Char)(character - 32) : character;
				}
			});
		}

		private static Byte[] BuildFallout1Index(SortedDictionary<String, List<DatEntry>> directories, SavePlan plan, Int32 indexSize)
		{
			Byte[] index = new Byte[indexSize];
			Int32 position = 0;

			void WriteInt32(Int32 value)
			{
				BinaryPrimitives.WriteInt32BigEndian(index.AsSpan(position), value);
				position += 4;
			}

			void WriteName(String name)
			{
				index[position++] = (Byte)name.Length;
				position += NameEncoding.GetBytes(name, index.AsSpan(position));
			}

			WriteInt32(directories.Count);
			WriteInt32(directories.Count);
			position += 8;

			foreach (String directory in directories.Keys)
			{
				WriteName(directory);
			}

			foreach (List<DatEntry> files in directories.Values)
			{
				WriteInt32(files.Count);
				WriteInt32(files.Count);
				WriteInt32(16);
				WriteInt32(0);

				foreach (DatEntry entry in files)
				{
					Placement placement = plan.Placements[entry];
					WriteName(entry.Name);
					WriteInt32(placement.IsCompressed ? 0x40 : 0x20);
					WriteInt32((Int32)placement.Offset);
					WriteInt32(placement.UnpackedSize);
					WriteInt32(placement.PackedSize);
				}
			}

			return index;
		}

		#endregion
	}
}