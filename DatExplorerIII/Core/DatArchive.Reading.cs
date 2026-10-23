namespace DatExplorerIII.Core
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Models.Archives;

	public sealed partial class DatArchive
	{
		private const Int32 Fallout2FooterSize = 8;
		private const Int32 Fallout1HeaderSize = 16;
		private const Int32 MaxFallout1Directories = 0xFFFFF;

		/// <summary>The smallest possible entry of a Fallout 2 directory tree: name length, a one byte name, flag, three numbers.</summary>
		private const Int32 MinimumFallout2EntrySize = 4 + 1 + 13;

		/// <summary>How far from the end of the file a footer is searched when the file does not end with one.</summary>
		private const Int64 FooterSearchWindow = 64L * 1024 * 1024;

		/// <summary>The best compression ratio of zlib is about 1032 to 1.</summary>
		private const Int64 MaxZlibRatio = 1100;

		/// <summary>The largest file inflated straight into an array of its announced size (the files of the games are much smaller).</summary>
		private const Int32 DirectInflateLimit = 64 * 1024 * 1024;

		/// <summary>How many files are extracted at the same time, at most, and how many bytes they may hold together.</summary>
		private const Int32 ExtractBatchFiles = 64;

		private const Int64 ExtractBatchBytes = 64L * 1024 * 1024;

		/// <summary>
		/// Opens a DAT archive. The format (Fallout 1, Fallout 2 or Arcanum) is detected from the content.
		/// </summary>
		/// <param name="filePath">The archive.</param>
		/// <param name="mode">With <see cref="DatOpenMode.Recover"/>, an archive that is damaged is read as far as possible.</param>
		/// <exception cref="DatException">The file is not a valid DAT archive (or cannot be recovered).</exception>
		public static DatArchive Open(String filePath, DatOpenMode mode = DatOpenMode.Strict)
		{
			FileStream stream = OpenStream(filePath);
			try
			{
				DatArchive archive = ReadArchive(filePath, stream, mode);
				archive._stream = stream;
				return archive;
			}
			catch
			{
				stream.Dispose();
				throw;
			}
		}

		private static FileStream OpenStream(String filePath)
		{
			if (String.IsNullOrWhiteSpace(filePath))
				throw new DatException(DatErrorKind.InvalidFileName, "Invalid DAT file name.");

			try
			{
				return new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
			}
			catch (UnauthorizedAccessException)
			{
				// Read only file or medium: the archive can be browsed and extracted but not modified in place.
				return OpenReadOnly(filePath);
			}
			catch (IOException exception)
			{
				throw new DatException(DatErrorKind.Io, exception.Message, exception, exception.Message);
			}
		}

		private static FileStream OpenReadOnly(String filePath)
		{
			try
			{
				return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				throw new DatException(DatErrorKind.Io, exception.Message, exception, exception.Message);
			}
		}

		private static DatArchive ReadArchive(String filePath, FileStream stream, DatOpenMode mode)
		{
			Int64 length = stream.Length;
			if (length == 0)
				throw new DatException(DatErrorKind.EmptyFile, "The DAT file is empty.");

			try
			{
				// A file that starts like a ZIP is a ZIP only if it reads as one: the first bytes of a DAT can be those of a ZIP it stores.
				if (IsZipFile(stream, length))
				{
					try
					{
						return ReadZipArchive(filePath, stream, length);
					}
					catch (DatException exception) when (exception.Kind == DatErrorKind.NotADat)
					{ }
				}

				if (TryReadArcanumFooter(stream, length, out ArcanumFooter arcanumFooter))
					return ReadArcanumArchive(filePath, stream, arcanumFooter, length, mode);

				if (length >= Fallout2FooterSize && TryReadFooter(stream, length, out Int32 treeSize))
					return ReadFallout2Archive(filePath, stream, treeSize, length, mode);

				if (mode == DatOpenMode.Recover && TryFindFooter(stream, out Int64 archiveEnd, out Int32 foundTreeSize))
					return ReadFallout2Archive(filePath, stream, foundTreeSize, archiveEnd, mode);

				if (length >= Fallout1HeaderSize)
				{
					Byte[] header = new Byte[4];
					stream.Position = 0;
					stream.ReadExactly(header);

					Int32 directoryCount = BinaryPrimitives.ReadInt32BigEndian(header);

					// An archive without directories is only the 16 bytes header.
					if ((directoryCount > 0 && directoryCount <= MaxFallout1Directories) || (directoryCount == 0 && length == Fallout1HeaderSize))
					{
						DatArchive fallout1 = new(filePath, DatFormat.Fallout1);
						fallout1.ReadFallout1(stream, directoryCount, mode);
						fallout1.FinishReading(mode);
						return fallout1;
					}
				}
			}
			catch (EndOfStreamException exception)
			{
				throw new DatException(DatErrorKind.TruncatedFile, "The DAT file is truncated or corrupted.", exception);
			}

			throw new DatException(DatErrorKind.NotADat, "The file is not a valid DAT archive.");
		}

		private static DatArchive ReadFallout2Archive(String filePath, FileStream stream, Int32 treeSize, Int64 archiveEnd, DatOpenMode mode)
		{
			DatArchive fallout2 = new(filePath, DatFormat.Fallout2);
			if (archiveEnd < stream.Length)
				fallout2._issues.Add(new DatIssue(DatIssueKind.TrailingData, null, stream.Length - archiveEnd));

			fallout2.ReadFallout2(stream, treeSize, archiveEnd, mode);
			fallout2.FinishReading(mode);
			return fallout2;
		}

		/// <summary>
		/// Reads the footer of a Fallout 2 archive: it is the last 8 bytes, and the archive size it holds is the file size.
		/// </summary>
		private static Boolean TryReadFooter(FileStream stream, Int64 length, out Int32 treeSize)
		{
			Byte[] footer = new Byte[Fallout2FooterSize];
			stream.Position = length - Fallout2FooterSize;
			stream.ReadExactly(footer);

			treeSize = BinaryPrimitives.ReadInt32LittleEndian(footer);
			return BinaryPrimitives.ReadUInt32LittleEndian(footer.AsSpan(4)) == length;
		}

		/// <summary>
		/// Looks for the footer of a Fallout 2 archive that is followed by other bytes: it is the last place of the file
		/// where the stored archive size is the position just after it and where the tree starts with a plausible file count.
		/// </summary>
		private static Boolean TryFindFooter(FileStream stream, out Int64 archiveEnd, out Int32 treeSize)
		{
			archiveEnd = 0;
			treeSize = 0;

			Int64 length = stream.Length;
			Int64 floor = Math.Max(0, length - FooterSearchWindow);
			const Int32 ChunkSize = 1 << 20;
			Byte[] buffer = new Byte[ChunkSize];
			Byte[] countBuffer = new Byte[4];
			Int64 windowEnd = length;

			while (windowEnd - floor >= Fallout2FooterSize)
			{
				Int64 start = Math.Max(floor, windowEnd - ChunkSize);
				Int32 count = (Int32)(windowEnd - start);
				stream.Position = start;
				stream.ReadExactly(buffer, 0, count);

				for (Int32 i = count - Fallout2FooterSize; i >= 0; i--)
				{
					Int64 position = start + i;
					if (BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(i + 4)) != (UInt32)(position + Fallout2FooterSize))
						continue;

					Int32 candidate = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(i));
					if (candidate < 4 || candidate > position)
						continue;

					// The tree starts with the number of files, which cannot be more than the tree can hold.
					Int64 treeStart = position - candidate;
					Int64 resume = stream.Position;
					stream.Position = treeStart;
					stream.ReadExactly(countBuffer);
					stream.Position = resume;
					Int32 fileCount = BinaryPrimitives.ReadInt32LittleEndian(countBuffer);
					if (fileCount < 0 || fileCount > candidate / MinimumFallout2EntrySize + 1)
						continue;

					archiveEnd = position + Fallout2FooterSize;
					treeSize = candidate;
					return true;
				}

				if (start == floor)
					break;

				// Overlap by 7 bytes so that a footer cut by the window boundary is seen whole by the next window.
				windowEnd = start + Fallout2FooterSize - 1;
			}

			return false;
		}

		/// <summary>
		/// Fallout 2 layout: [file data][file count][entries][tree size][archive size].
		/// </summary>
		private void ReadFallout2(FileStream stream, Int32 treeSize, Int64 archiveEnd, DatOpenMode mode)
		{
			if (treeSize < 4 || treeSize + (Int64)Fallout2FooterSize > archiveEnd)
				throw new DatException(DatErrorKind.InvalidTree, "The directory tree of the DAT file is invalid.");

			_treeSize = treeSize;
			_archiveEnd = archiveEnd;
			_dataLimit = archiveEnd - treeSize - Fallout2FooterSize;

			// The tree starts with the file count, followed by the entries.
			Byte[] tree = new Byte[treeSize];
			stream.Position = _dataLimit;
			stream.ReadExactly(tree);

			Int32 fileCount = BinaryPrimitives.ReadInt32LittleEndian(tree);
			Int32 position = 4;
			Int32 parsed = 0;

			while (parsed < fileCount && TryReadFallout2Entry(tree, ref position, out DatEntry? entry))
			{
				Register(entry!, true);
				parsed++;
			}

			if (parsed < Math.Max(fileCount, 0) || fileCount < 0)
			{
				if (mode == DatOpenMode.Strict)
					throw new DatException(DatErrorKind.InvalidTree, "The directory tree of the DAT file is damaged.");

				_issues.Add(new DatIssue(DatIssueKind.TruncatedTree, null, parsed));
			}
		}

		private static Boolean TryReadFallout2Entry(Byte[] tree, ref Int32 position, out DatEntry? entry)
		{
			entry = null;
			if (position + 4 > tree.Length)
				return false;

			Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position));
			if (nameLength <= 0 || (Int64)position + 4 + nameLength + 13 > tree.Length)
				return false;

			position += 4;
			String path = NameEncoding.GetString(tree, position, nameLength);
			position += nameLength;

			Boolean compressed = tree[position++] == 1;
			Int32 unpackedSize = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position));
			Int32 packedSize = BinaryPrimitives.ReadInt32LittleEndian(tree.AsSpan(position + 4));
			UInt32 offset = BinaryPrimitives.ReadUInt32LittleEndian(tree.AsSpan(position + 8));
			position += 12;

			// Some tools do not set the flag; a size difference means that the data is compressed.
			if (!compressed && unpackedSize != packedSize)
				compressed = true;

			entry = CreateEntry(path, compressed, unpackedSize, packedSize, offset);
			return true;
		}

		/// <summary>
		/// Fallout 1 layout: [header][directory names][directory contents][file data], all values big endian.
		/// See https://falloutmods.fandom.com/wiki/DAT_file_format
		/// </summary>
		private void ReadFallout1(FileStream stream, Int32 directoryCount, DatOpenMode mode)
		{
			_archiveEnd = stream.Length;
			_dataLimit = stream.Length;
			stream.Position = Fallout1HeaderSize;

			// Not disposed on purpose: that would close the archive stream.
			BufferedStream buffered = new(stream, 65536);

			try
			{
				List<String> directories = [with(directoryCount)];
				for (Int32 index = 0; index < directoryCount; index++)
				{
					String name = ReadString(buffered).Trim('\\');
					directories.Add(name == "." || name.Length == 0 ? String.Empty : name + '\\');
				}

				Byte[] buffer = new Byte[16];
				foreach (String directory in directories)
				{
					buffered.ReadExactly(buffer);
					Int32 fileCount = BinaryPrimitives.ReadInt32BigEndian(buffer);
					if (fileCount < 0)
					{
						if (mode == DatOpenMode.Strict)
							throw new DatException(DatErrorKind.InvalidTree, "The directory contents of the DAT file are corrupted.");

						_issues.Add(new DatIssue(DatIssueKind.TruncatedTree, null, _entries.Count));
						return;
					}

					for (Int32 index = 0; index < fileCount; index++)
					{
						String name = ReadString(buffered);
						buffered.ReadExactly(buffer);

						Boolean compressed = BinaryPrimitives.ReadInt32BigEndian(buffer) == 0x40;
						UInt32 offset = BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(4));
						Int32 unpackedSize = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(8));
						Int32 packedSize = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(12));

						Register(CreateEntry(directory + name, compressed, unpackedSize, packedSize, offset), true);
					}
				}
			}
			catch (EndOfStreamException) when (mode == DatOpenMode.Recover)
			{
				_issues.Add(new DatIssue(DatIssueKind.TruncatedTree, null, _entries.Count));
			}
		}

		private static String ReadString(Stream stream)
		{
			Int32 length = stream.ReadByte();
			if (length < 0)
				throw new EndOfStreamException();

			Byte[] bytes = new Byte[length];
			stream.ReadExactly(bytes);
			return NameEncoding.GetString(bytes);
		}

		private static DatEntry CreateEntry(String path, Boolean compressed, Int32 unpackedSize, Int32 packedSize, UInt32 offset)
		{
			path = NormalizePath(path);
			Int32 separator = path.LastIndexOf('\\') + 1;

			return new DatEntry(path.Substring(0, separator), path.Substring(separator))
			{
				IsCompressed = compressed,
				UnpackedSize = unpackedSize,
				PackedSize = packedSize,
				Offset = offset,
			};
		}

		/// <summary>
		/// Flags the files whose data cannot be where the directory says, and notes what was recovered.
		/// </summary>
		private void FinishReading(DatOpenMode mode)
		{
			foreach (DatEntry entry in _entries)
			{
				Int32 size = entry.IsCompressed ? entry.PackedSize : entry.UnpackedSize;
				if (entry.UnpackedSize < 0 || size < 0)
				{
					MarkDamaged(entry, DatIssueKind.EntryInvalidSize);
				}
				else if (entry.Offset + (Int64)size > _dataLimit)
				{
					MarkDamaged(entry, DatIssueKind.EntryOutsideArchive);
				}
			}

			if (mode == DatOpenMode.Recover && _issues.Count > 0)
			{
				// Written again completely at the next save, from what could be read.
				IsRecovered = true;
				RequestRepair();
			}
		}

		private void MarkDamaged(DatEntry entry, DatIssueKind problem)
		{
			entry.IsDamaged = true;
			entry.Problem = problem;
			_issues.Add(new DatIssue(problem, entry.FullPath));
		}

		#region Data

		/// <summary>Reads the data of a file as it is stored in the archive.</summary>
		internal Byte[] ReadStored(DatEntry entry)
		{
			if (entry.IsDamaged)
			{
				DatErrorKind kind = entry.Problem == DatIssueKind.EntryCorrupt ? DatErrorKind.CorruptData : DatErrorKind.OutsideArchive;
				throw new DatException(kind, $"The file '{entry.FullPath}' is damaged.", entry.FullPath);
			}

			Int32 size = entry.IsCompressed ? entry.PackedSize : entry.UnpackedSize;

			lock (_streamLock)
			{
				FileStream stream = _stream ?? throw new DatException(DatErrorKind.NotOnDisk, "The archive is not stored on disk.");
				if (size < 0 || entry.Offset + (Int64)size > _dataLimit)
					throw new DatException(DatErrorKind.OutsideArchive, $"The file '{entry.FullPath}' is outside of the archive.", entry.FullPath);

				Byte[] data = new Byte[size];
				stream.Position = entry.Offset;
				stream.ReadExactly(data);
				return data;
			}
		}

		/// <summary>Reads the complete, decompressed content of a file.</summary>
		/// <exception cref="DatException">The data is damaged.</exception>
		public Byte[] ReadData(DatEntry entry)
		{
			if (entry.IsNew)
				return File.ReadAllBytes(entry.SourceFile!);

			if (Format == DatFormat.Zip)
				return ReadZipData(entry);

			return Unpack(entry, ReadStored(entry));
		}

		/// <summary>The content of a stored file of the archive from its data as it is stored (<see cref="ReadStored"/>).</summary>
		/// <exception cref="DatException">The data is damaged.</exception>
		internal Byte[] Unpack(DatEntry entry, Byte[] stored)
		{
			if (!entry.IsCompressed)
				return stored;

			try
			{
				return Format == DatFormat.Fallout1
					? Lzss.Decompress(stored, entry.UnpackedSize)
					: InflateZlib(stored, entry.UnpackedSize);
			}
			catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException or IOException)
			{
				// The decompressors report damaged data with these exceptions (zlib's is an IOException).
				throw new DatException(DatErrorKind.CorruptData, $"The data of '{entry.FullPath}' is corrupted.", exception, entry.FullPath);
			}
		}

		/// <summary>
		/// Inflates data of announced size. A file of usual size is inflated straight into an array of that size; a larger one goes
		/// into an output that grows as the data is really produced, so that a lying size costs nothing.
		/// </summary>
		private static Byte[] InflateZlib(Byte[] stored, Int32 unpackedSize)
		{
			if (unpackedSize < 0 || unpackedSize > stored.Length * MaxZlibRatio + 1024)
				throw new InvalidDataException("The announced size of the file is impossible.");

			using MemoryStream source = new(stored);
			using ZLibStream inflater = new(source, CompressionMode.Decompress);
			if (unpackedSize <= DirectInflateLimit)
			{
				// Data that ends too soon throws (EndOfStreamException); reading on checks the trailer (the checksum of the data).
				Byte[] data = new Byte[unpackedSize];
				inflater.ReadExactly(data);
				Span<Byte> after = stackalloc Byte[1];
				_ = inflater.Read(after);
				return data;
			}

			using MemoryStream result = new(1 << 20);

			Byte[] buffer = new Byte[81920];
			while (result.Length < unpackedSize)
			{
				Int32 read = inflater.Read(buffer, 0, (Int32)Math.Min(buffer.Length, unpackedSize - result.Length));
				if (read == 0)
					throw new InvalidDataException("The compressed data of the file is truncated.");

				result.Write(buffer, 0, read);
			}

			// Reading to the end of the stream checks its trailer (the checksum of the data).
			_ = inflater.Read(buffer, 0, 1);
			return result.ToArray();
		}

		/// <summary>
		/// Reads every file and notes the ones that cannot be read: the damage is found before it is needed.
		/// </summary>
		public DatVerifyResult Verify(IProgress<DatProgress>? progress = null, CancellationToken cancellationToken = default)
		{
			List<DatEntry> stored = _entries.Where(entry => !entry.IsNew).ToList();
			DatProgressThrottle throttle = new(progress, stored.Count);

			// The files are read and inflated on a few threads at once; what was found is noted afterwards, in the order of the files.
			DatErrorKind?[] failures = new DatErrorKind?[stored.Count];
			Int32 done = 0;
			ForEachParallel(0, stored.Count, cancellationToken, index =>
			{
				DatEntry entry = stored[index];
				if (!entry.IsDamaged)
				{
					try
					{
						ReadData(entry);
					}
					catch (DatException exception) when (exception.Kind is DatErrorKind.CorruptData or DatErrorKind.OutsideArchive)
					{
						failures[index] = exception.Kind;
					}
				}

				throttle.Report(entry.FullPath, Interlocked.Increment(ref done));
			});

			List<DatEntry> damaged = [];
			for (Int32 index = 0; index < stored.Count; index++)
			{
				DatEntry entry = stored[index];
				if (failures[index] is DatErrorKind kind)
					MarkDamaged(entry, kind == DatErrorKind.CorruptData ? DatIssueKind.EntryCorrupt : DatIssueKind.EntryOutsideArchive);

				if (entry.IsDamaged)
					damaged.Add(entry);
			}

			return new DatVerifyResult(stored.Count, damaged);
		}

		/// <summary>How many files are read at the same time: the reads of the archive take turns, their decompression does not.</summary>
		private static Int32 ReadThreads => Math.Min(4, Environment.ProcessorCount);

		/// <summary>Runs a step for each index on a few threads at once. The error of a step is thrown as it is, not wrapped.</summary>
		private static void ForEachParallel(Int32 from, Int32 to, CancellationToken cancellationToken, Action<Int32> body)
		{
			ParallelOptions options = new() { MaxDegreeOfParallelism = ReadThreads, CancellationToken = cancellationToken };
			try
			{
				Parallel.For(from, to, options, body);
			}
			catch (AggregateException exception) when (exception.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			}
		}

		/// <summary>
		/// Extracts files to a folder of the disk, keeping their folder structure.
		/// </summary>
		/// <param name="entries">Files to extract.</param>
		/// <param name="destination">Destination folder.</param>
		/// <param name="stripPrefix">Leading part of the archive folder that is not recreated on disk (empty to keep everything).</param>
		/// <param name="flatNames">Null to keep the folders; else the name of each file (see <see cref="GetFlatNames"/>): every file goes straight in the destination.</param>
		public DatExtractResult Extract(
			IReadOnlyList<DatEntry> entries,
			String destination,
			String stripPrefix,
			IProgress<DatProgress>? progress = null,
			CancellationToken cancellationToken = default,
			IReadOnlyDictionary<DatEntry, String>? flatNames = null)
		{
			String root = Path.GetFullPath(destination);
			String rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
			List<String> failures = [];
			Int32 succeeded = 0;
			DatProgressThrottle throttle = new(progress, entries.Count);

			// The files are read and inflated a batch at a time on a few threads, then written one after the other, in their order (two
			// files of the same path are written as they come).
			Int32 start = 0;
			while (start < entries.Count)
			{
				cancellationToken.ThrowIfCancellationRequested();
				Int32 end = start;
				Int64 bytes = 0;
				while (end < entries.Count && end - start < ExtractBatchFiles && (end == start || bytes + Math.Max(0, entries[end].UnpackedSize) <= ExtractBatchBytes))
				{
					bytes += Math.Max(0, entries[end].UnpackedSize);
					end++;
				}

				Byte[]?[] data = new Byte[end - start][];
				Int32 first = start;
				ForEachParallel(start, end, cancellationToken, index =>
				{
					DatEntry entry = entries[index];
					if (entry.IsDamaged)
						return;

					try
					{
						data[index - first] = ReadData(entry);
					}
					catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
					{
						// Counted as a failure below.
					}
				});

				for (Int32 index = start; index < end; index++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					DatEntry entry = entries[index];
					throttle.Report(entry.FullPath, index + 1);

					Byte[]? content = data[index - start];
					if (content == null)
					{
						failures.Add(entry.FullPath);
						continue;
					}

					try
					{
						String target = ResolveTarget(rootWithSeparator, entry, stripPrefix, flatNames?[entry]);
						Directory.CreateDirectory(Path.GetDirectoryName(target)!);
						File.WriteAllBytes(target, content);
						succeeded++;
					}
					catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
					{
						failures.Add(entry.FullPath);
					}
				}

				start = end;
			}

			return new DatExtractResult(succeeded, failures);
		}

		/// <summary>
		/// Returns where a file is extracted, refusing paths that would leave the destination folder.
		/// </summary>
		/// <exception cref="DatException">The path of the file is not allowed.</exception>
		public static String GetExtractionPath(String destination, DatEntry entry, String stripPrefix, String? flatName = null)
		{
			String root = Path.GetFullPath(destination);
			return ResolveTarget(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, entry, stripPrefix, flatName);
		}

		/// <summary>
		/// The names the files have when they are extracted with no folders. Files of different folders can have the same name: the
		/// first one keeps it, the next ones get a suffix before the extension, <c>_1</c>, then <c>_2</c>... (a.txt, a_1.txt, a_2.txt).
		/// The names depend on all the files given, so that a file that is not extracted does not change the name of the others.
		/// </summary>
		public static Dictionary<DatEntry, String> GetFlatNames(IEnumerable<DatEntry> entries)
		{
			Dictionary<DatEntry, String> names = [];
			HashSet<String> taken = [with(StringComparer.OrdinalIgnoreCase)];
			foreach (DatEntry entry in entries)
			{
				String name = entry.Name;
				String stem = Path.GetFileNameWithoutExtension(name);
				String extension = Path.GetExtension(name);
				for (Int32 number = 1; !taken.Add(name); number++)
				{
					name = stem + "_" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + extension;
				}

				names[entry] = name;
			}

			return names;
		}

		internal static String ResolveTarget(String rootWithSeparator, DatEntry entry, String stripPrefix, String? flatName = null)
		{
			String relative = entry.FullPath;
			if (flatName != null)
			{
				relative = flatName;
			}
			else if (stripPrefix.Length > 0 && relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
			{
				relative = relative.Substring(stripPrefix.Length);
			}

			String target = Path.GetFullPath(Path.Combine(rootWithSeparator, relative));
			if (Path.IsPathRooted(relative) || !target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
				throw new DatException(DatErrorKind.PathNotAllowed, $"The path '{entry.FullPath}' is not allowed.", entry.FullPath);

			return target;
		}

		#endregion
	}
}