namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Models;

	/// <summary>
	/// A Fallout 1, Fallout 2 or Arcanum DAT archive: reading, editing, verifying, repairing and saving.
	/// Paths inside the archive use backslashes; lookups are case insensitive.
	/// </summary>
	public sealed partial class DatArchive : IDisposable
	{
		internal const Int32 MaxNameLength = 255;

		/// <summary>DAT files store single byte characters.</summary>
		internal static readonly Encoding NameEncoding = Encoding.Latin1;

		private readonly List<DatEntry> _entries = new();
		private readonly Dictionary<String, DatEntry> _byKey = new(StringComparer.Ordinal);
		private readonly Dictionary<String, List<DatEntry>> _byDirectory = new(StringComparer.Ordinal);

		/// <summary>Lower case folder path to original case folder path. Includes empty folders.</summary>
		private readonly Dictionary<String, String> _folders = new(StringComparer.Ordinal);

		private readonly List<DatIssue> _issues = new();
		private readonly Object _streamLock = new();

		private FileStream? _stream;
		private Int32 _newCount;
		private Boolean _renamed;
		private Boolean _deletedStored;
		private Boolean _repairPending;
		private Boolean _forceRebuild;

		/// <summary>Size of the Fallout 2 directory tree (entries and file count), from the archive footer.</summary>
		private Int32 _treeSize;

		/// <summary>Where the archive ends in the file; bytes after it are not part of the archive.</summary>
		private Int64 _archiveEnd;

		/// <summary>Where the data of the files must end: the start of the directory tree.</summary>
		private Int64 _dataLimit;

		private DatArchive(String filePath, DatFormat format)
		{
			FilePath = filePath;
			Format = format;
		}

		public String FilePath { get; private set; }

		/// <summary>The format of the archive; it can only change when an archive made from a folder is saved.</summary>
		public DatFormat Format { get; private set; }

		/// <summary>False for an archive that was created in memory and never saved.</summary>
		public Boolean HasBackingFile => _stream != null;

		public Int32 Count => _entries.Count;

		public Int32 NewCount => _newCount;

		public IReadOnlyList<DatEntry> Entries => _entries;

		/// <summary>All folders (original case, trailing backslash), including empty ones, excluding the root.</summary>
		public IEnumerable<String> Folders => _folders.Values;

		/// <summary>The problems found when the archive was read or verified.</summary>
		public IReadOnlyList<DatIssue> Issues => _issues;

		/// <summary>True when the archive was opened with <see cref="DatOpenMode.Recover"/> and something had to be recovered.</summary>
		public Boolean IsRecovered { get; private set; }

		/// <summary>Number of files whose data is unusable.</summary>
		public Int32 DamagedCount => _entries.Count(entry => entry.IsDamaged);

		/// <summary>
		/// True when the archive must be written again completely: the quick save would carry the damage along.
		/// </summary>
		public Boolean RequiresRebuild => _forceRebuild;

		/// <summary>Where the original file was kept by the last save of a repaired archive.</summary>
		public String? BackupPath { get; private set; }

		public DatPendingChanges PendingChanges
		{
			get
			{
				DatPendingChanges changes = DatPendingChanges.None;
				if (_sourceFolder != null && _folderPristine)
				{
					return changes;
				}

				if (!HasBackingFile)
				{
					changes |= DatPendingChanges.Created;
				}

				if (_newCount > 0)
				{
					changes |= DatPendingChanges.Added;
				}

				if (_deletedStored)
				{
					changes |= DatPendingChanges.Deleted;
				}

				if (_renamed)
				{
					changes |= DatPendingChanges.Renamed;
				}

				if (_repairPending)
				{
					changes |= DatPendingChanges.Repaired;
				}

				return changes;
			}
		}

		/// <summary>Creates an empty archive in memory; the file is written by <see cref="Save"/>.</summary>
		public static DatArchive Create(String filePath, DatFormat format)
		{
			return new DatArchive(filePath, format);
		}

		public void Dispose()
		{
			lock (_streamLock)
			{
				_zip?.Dispose();
				_zip = null;
				_stream?.Dispose();
				_stream = null;
			}
		}

		#region Lookup

		public DatEntry? Find(String path)
		{
			_byKey.TryGetValue(NormalizePath(path).ToLowerInvariant(), out DatEntry? entry);
			return entry;
		}

		public Boolean FolderExists(String directory)
		{
			String normalized = NormalizeDirectory(directory);
			return normalized.Length == 0 || _folders.ContainsKey(normalized.ToLowerInvariant());
		}

		/// <summary>Files located directly in a folder.</summary>
		public IReadOnlyList<DatEntry> GetEntries(String directory)
		{
			if (_byDirectory.TryGetValue(NormalizeDirectory(directory).ToLowerInvariant(), out List<DatEntry>? list))
			{
				return list;
			}

			return Array.Empty<DatEntry>();
		}

		/// <summary>Files located in a folder or in any of its sub folders.</summary>
		public List<DatEntry> GetEntriesUnder(String directory)
		{
			String key = NormalizeDirectory(directory).ToLowerInvariant();
			return _entries.Where(entry => entry.DirectoryKey.StartsWith(key, StringComparison.Ordinal)).ToList();
		}

		#endregion

		#region Editing

		/// <summary>Adds a file of the disk to the archive. It is written on the next <see cref="Save"/>.</summary>
		public DatEntry AddFile(String sourceFile, String directory, String? name = null)
		{
			System.IO.FileInfo info = new(sourceFile);
			if (!info.Exists)
			{
				throw new DatException(DatErrorKind.FileNotFound, $"The file '{sourceFile}' does not exist.", sourceFile);
			}

			if (info.Length > Int32.MaxValue)
			{
				throw new DatException(DatErrorKind.FileTooLarge, $"The file '{sourceFile}' is too large for a DAT archive.", sourceFile);
			}

			Touch();
			String normalizedDirectory = NormalizeDirectory(directory);
			String fileName = name ?? info.Name;
			ValidateDirectory(normalizedDirectory);
			ValidateName(fileName);

			DatEntry entry = new(normalizedDirectory, fileName)
			{
				UnpackedSize = (Int32)info.Length,
				SourceFile = info.FullName,
			};

			if (_byKey.ContainsKey(entry.Key))
			{
				throw new DatException(DatErrorKind.AlreadyExists, $"The file '{entry.FullPath}' already exists.", entry.FullPath);
			}

			Register(entry);
			_newCount++;
			return entry;
		}

		/// <summary>Creates an empty folder. The DAT files of Fallout do not store empty folders; those of Arcanum do.</summary>
		public void AddFolder(String directory)
		{
			String normalized = NormalizeDirectory(directory);
			ValidateDirectory(normalized);
			Touch();
			RegisterFolder(normalized);
		}

		public void Remove(IEnumerable<DatEntry> entries)
		{
			HashSet<DatEntry> removed = new(entries);
			if (removed.Count == 0)
			{
				return;
			}

			Touch();

			_entries.RemoveAll(removed.Contains);

			foreach (IGrouping<String, DatEntry> group in removed.GroupBy(entry => entry.DirectoryKey))
			{
				if (_byDirectory.TryGetValue(group.Key, out List<DatEntry>? list))
				{
					HashSet<DatEntry> inDirectory = new(group);
					list.RemoveAll(inDirectory.Contains);
				}
			}

			foreach (DatEntry entry in removed)
			{
				if (_byKey.TryGetValue(entry.Key, out DatEntry? indexed) && indexed == entry)
				{
					_byKey.Remove(entry.Key);
				}

				if (entry.IsNew)
				{
					_newCount--;
				}
				else
				{
					_deletedStored = true;
				}
			}
		}

		/// <summary>Removes a folder with everything it contains.</summary>
		public void RemoveFolder(String directory)
		{
			String normalized = NormalizeDirectory(directory);
			String key = normalized.ToLowerInvariant();
			if (key.Length == 0)
			{
				return;
			}

			Remove(GetEntriesUnder(normalized));

			foreach (String folderKey in _folders.Keys.Where(k => k.StartsWith(key, StringComparison.Ordinal)).ToList())
			{
				_folders.Remove(folderKey);
				_byDirectory.Remove(folderKey);
			}
		}

		public void Rename(DatEntry entry, String newName)
		{
			ValidateName(newName);
			Touch();

			String newKey = (entry.Directory + newName).ToLowerInvariant();
			if (newKey != entry.Key && _byKey.ContainsKey(newKey))
			{
				throw new DatException(DatErrorKind.AlreadyExists, $"The file '{entry.Directory + newName}' already exists.", entry.Directory + newName);
			}

			if (_byKey.TryGetValue(entry.Key, out DatEntry? indexed) && indexed == entry)
			{
				_byKey.Remove(entry.Key);
			}

			entry.SetLocation(entry.Directory, newName);
			_byKey[entry.Key] = entry;

			if (!entry.IsNew)
			{
				_renamed = true;
			}
		}

		/// <summary>
		/// Puts in lower case the name of a folder, of its sub folders and of the files under it (the whole archive when
		/// <paramref name="directory"/> is empty); the folders above it keep their names. The names are compared without regard to
		/// case, so no name can clash with another.
		/// </summary>
		/// <param name="directory">The folder; empty for all the archive.</param>
		/// <param name="apply">False only counts the names that would change.</param>
		/// <returns>How many names (of folders and files) are, or were, not in lower case.</returns>
		public Int32 LowercaseTree(String directory, Boolean apply = true)
		{
			String path = NormalizeDirectory(directory);
			String key = path.ToLowerInvariant();

			// What is above the folder is kept as it is.
			String prefix = path.Substring(0, path.Length == 0 ? 0 : path.TrimEnd('\\').LastIndexOf('\\') + 1);
			String Lower(String text) => prefix + text.Substring(prefix.Length).ToLowerInvariant();

			List<KeyValuePair<String, String>> folders = _folders.Where(pair => pair.Key.Length > 0 && pair.Key.StartsWith(key, StringComparison.Ordinal) && Lower(pair.Value) != pair.Value).ToList();
			List<DatEntry> entries = _entries.Where(entry => entry.DirectoryKey.StartsWith(key, StringComparison.Ordinal) && (Lower(entry.Directory) != entry.Directory || entry.Name.ToLowerInvariant() != entry.Name)).ToList();
			Int32 count = folders.Count + entries.Count;
			if (!apply || count == 0)
			{
				return count;
			}

			Touch();
			foreach (KeyValuePair<String, String> pair in folders)
			{
				_folders[pair.Key] = Lower(pair.Value);
			}

			foreach (DatEntry entry in entries)
			{
				entry.SetLocation(Lower(entry.Directory), entry.Name.ToLowerInvariant());
				if (!entry.IsNew)
				{
					_renamed = true;
				}
			}

			RebuildIndexes();
			return count;
		}

		/// <summary>Renames a folder (not its path) and returns its new path.</summary>
		public String RenameFolder(String directory, String newName)
		{
			String oldPath = NormalizeDirectory(directory);
			if (oldPath.Length == 0)
			{
				throw new DatException(DatErrorKind.RootFolderRename, "The root folder cannot be renamed.");
			}

			ValidateName(newName);
			Touch();

			String oldKey = oldPath.ToLowerInvariant();
			Int32 parentLength = oldPath.TrimEnd('\\').LastIndexOf('\\') + 1;
			String newPath = oldPath.Substring(0, parentLength) + newName + '\\';
			String newKey = newPath.ToLowerInvariant();

			if (newKey != oldKey && _folders.ContainsKey(newKey))
			{
				throw new DatException(DatErrorKind.FolderAlreadyExists, $"The folder '{newPath}' already exists.", newPath);
			}

			List<KeyValuePair<String, String>> oldFolders = _folders.Where(pair => pair.Key.StartsWith(oldKey, StringComparison.Ordinal)).ToList();

			foreach (DatEntry entry in _entries.Where(e => e.DirectoryKey.StartsWith(oldKey, StringComparison.Ordinal)).ToList())
			{
				entry.SetLocation(newPath + entry.Directory.Substring(oldPath.Length), entry.Name);
				if (!entry.IsNew)
				{
					_renamed = true;
				}
			}

			foreach (KeyValuePair<String, String> pair in oldFolders)
			{
				_folders.Remove(pair.Key);
			}

			foreach (KeyValuePair<String, String> pair in oldFolders)
			{
				String path = newPath + pair.Value.Substring(oldPath.Length);
				_folders[path.ToLowerInvariant()] = path;
			}

			RebuildIndexes();
			return newPath;
		}

		#endregion

		#region Repair

		/// <summary>
		/// Removes the files whose data is unusable. The archive can then be saved; the original file is kept as a backup.
		/// </summary>
		/// <returns>The number of files removed.</returns>
		public Int32 RemoveDamaged()
		{
			List<DatEntry> damaged = _entries.Where(entry => entry.IsDamaged).ToList();
			if (damaged.Count > 0)
			{
				Remove(damaged);
				RequestRepair();
			}

			return damaged.Count;
		}

		/// <summary>
		/// Marks the archive to be written again completely at the next save (the original is kept as a backup).
		/// </summary>
		public void RequestRepair()
		{
			_forceRebuild = true;
			_repairPending = true;
		}

		#endregion

		#region Paths

		/// <summary>Converts a folder path to the archive form: backslashes, no leading one, one trailing one.</summary>
		public static String NormalizeDirectory(String directory)
		{
			String normalized = directory.Replace('/', '\\').Trim('\\');
			return normalized.Length == 0 ? String.Empty : normalized + '\\';
		}

		public static String NormalizePath(String path)
		{
			return path.Replace('/', '\\').TrimStart('\\');
		}

		/// <summary>Checks that a file or folder name can be stored in a DAT archive and extracted to disk.</summary>
		public static void ValidateName(String name)
		{
			if (String.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
			{
				throw new DatException(DatErrorKind.NameInvalid, "The name is empty or too long.", name);
			}

			if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name != name.Trim() || name.EndsWith('.'))
			{
				throw new DatException(DatErrorKind.NameCharacters, $"The name '{name}' contains invalid characters.", name);
			}

			if (name.Any(character => character > 0xFF))
			{
				throw new DatException(DatErrorKind.NameNotStorable, $"The name '{name}' contains characters that a DAT archive cannot store.", name);
			}
		}

		private static void ValidateDirectory(String directory)
		{
			if (directory.Length > MaxNameLength)
			{
				throw new DatException(DatErrorKind.PathTooLong, $"The folder path '{directory}' is too long.", directory);
			}

			foreach (String segment in directory.Split('\\', StringSplitOptions.RemoveEmptyEntries))
			{
				ValidateName(segment);
			}
		}

		#endregion

		#region Indexes

		/// <param name="fromDisk">True while the archive is read: two files with the same path are then reported.</param>
		internal void Register(DatEntry entry, Boolean fromDisk = false)
		{
			_entries.Add(entry);
			if (!_byKey.TryAdd(entry.Key, entry) && fromDisk)
			{
				_issues.Add(new DatIssue(DatIssueKind.DuplicateEntry, entry.FullPath));
			}

			IndexDirectory(entry);
			RegisterFolder(entry.Directory);
		}

		private void IndexDirectory(DatEntry entry)
		{
			String key = entry.DirectoryKey;
			if (!_byDirectory.TryGetValue(key, out List<DatEntry>? list))
			{
				list = new List<DatEntry>();
				_byDirectory.Add(key, list);
			}

			list.Add(entry);
		}

		/// <summary>Registers a folder and all of its parents.</summary>
		private void RegisterFolder(String directory)
		{
			Int32 end = directory.IndexOf('\\');
			while (end >= 0)
			{
				String path = directory.Substring(0, end + 1);
				_folders.TryAdd(path.ToLowerInvariant(), path);
				end = directory.IndexOf('\\', end + 1);
			}
		}

		private void RebuildIndexes()
		{
			_byKey.Clear();
			_byDirectory.Clear();
			foreach (DatEntry entry in _entries)
			{
				_byKey.TryAdd(entry.Key, entry);
				IndexDirectory(entry);
			}
		}

		#endregion
	}
}