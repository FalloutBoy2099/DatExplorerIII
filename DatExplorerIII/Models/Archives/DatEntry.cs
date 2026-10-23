namespace DatExplorerIII.Models.Archives
{
	using System;

	/// <summary>
	/// A file stored in (or waiting to be added to) a DAT archive.
	/// </summary>
	public sealed class DatEntry
	{
		internal DatEntry(String directory, String name)
		{
			Directory = directory;
			Name = name;
			Key = (directory + name).ToLowerInvariant();
			DirectoryKey = directory.ToLowerInvariant();
		}

		/// <summary>Folder of the file, original case, ending with a backslash (empty for the root).</summary>
		public String Directory { get; private set; }

		/// <summary>File name, original case.</summary>
		public String Name { get; private set; }

		/// <summary>Folder and name, original case.</summary>
		public String FullPath => Directory + Name;

		/// <summary>Lower case <see cref="FullPath"/>, used for lookups.</summary>
		public String Key { get; private set; }

		/// <summary>Lower case <see cref="Directory"/>, used for lookups.</summary>
		public String DirectoryKey { get; private set; }

		public Int32 UnpackedSize { get; internal set; }

		/// <summary>Size of the stored data; only meaningful for compressed entries.</summary>
		public Int32 PackedSize { get; internal set; }

		public Boolean IsCompressed { get; internal set; }

		/// <summary>Position of the stored data in the archive.</summary>
		internal UInt32 Offset { get; set; }

		/// <summary>File on disk the data will be taken from, for entries that are not saved yet.</summary>
		public String? SourceFile { get; internal set; }

		/// <summary>True for a file that is not stored in the archive yet.</summary>
		public Boolean IsNew => SourceFile != null;

		/// <summary>True when the data of the file is known to be unusable (see <see cref="Problem"/>).</summary>
		public Boolean IsDamaged { get; internal set; }

		/// <summary>Why the file is damaged.</summary>
		public DatIssueKind? Problem { get; internal set; }

		internal void SetLocation(String directory, String name)
		{
			Directory = directory;
			Name = name;
			Key = (directory + name).ToLowerInvariant();
			DirectoryKey = directory.ToLowerInvariant();
		}
	}
}