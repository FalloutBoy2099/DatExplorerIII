namespace DatExplorerIII.Models.Archives
{
	using System;

	/// <summary>
	/// What to do with a file that already exists.
	/// </summary>
	public enum ConflictDecision
	{
		Replace,
		Skip,
		Cancel,
	}

	/// <summary>
	/// What went wrong, independent of the language: the user interface turns it into a translated message.
	/// </summary>
	public enum DatErrorKind
	{
		/// <summary>An error that has no dedicated message; the English text of the exception is shown.</summary>
		Unknown,

		/// <summary>The file name is missing.</summary>
		InvalidFileName,

		/// <summary>The file cannot be read (arguments: the message of the system).</summary>
		Io,

		/// <summary>The file has no content.</summary>
		EmptyFile,

		/// <summary>The file is not a DAT archive.</summary>
		NotADat,

		/// <summary>The end of the file is missing (the archive is incomplete).</summary>
		TruncatedFile,

		/// <summary>The list of files of the archive is damaged.</summary>
		InvalidTree,

		/// <summary>The archive does not exist on disk yet (arguments: none).</summary>
		NotOnDisk,

		/// <summary>The data of a file lies outside of the archive (arguments: the path of the file).</summary>
		OutsideArchive,

		/// <summary>The data of a file cannot be decompressed (arguments: the path of the file).</summary>
		CorruptData,

		/// <summary>A file to add does not exist (arguments: the path of the file).</summary>
		FileNotFound,

		/// <summary>A file is larger than a DAT archive can hold (arguments: the path of the file).</summary>
		FileTooLarge,

		/// <summary>A name is empty or too long.</summary>
		NameInvalid,

		/// <summary>A name contains characters that cannot be used (arguments: the name).</summary>
		NameCharacters,

		/// <summary>A name contains characters that a DAT archive cannot store (arguments: the name).</summary>
		NameNotStorable,

		/// <summary>A path is too long (arguments: the path).</summary>
		PathTooLong,

		/// <summary>A file already exists (arguments: the path).</summary>
		AlreadyExists,

		/// <summary>A folder already exists (arguments: the path).</summary>
		FolderAlreadyExists,

		/// <summary>The root folder cannot be renamed.</summary>
		RootFolderRename,

		/// <summary>The archive would be larger than 4 GB.</summary>
		ArchiveTooLarge,

		/// <summary>A path would be extracted outside of the destination folder (arguments: the path).</summary>
		PathNotAllowed,

		/// <summary>Damaged files must be removed before the archive can be saved.</summary>
		DamagedEntriesPresent,

		/// <summary>The format of the archive can be read but not written (Temple of Elemental Evil).</summary>
		ReadOnlyFormat,
	}

	/// <summary>
	/// Supported DAT archive layouts.
	/// </summary>
	public enum DatFormat
	{
		Fallout1 = 1,
		Fallout2 = 2,
		Arcanum = 3,
		Toee = 4,
		Zip = 5,
	}

	/// <summary>
	/// A kind of damage found in an archive.
	/// </summary>
	public enum DatIssueKind
	{
		/// <summary>Bytes follow the end of the archive (value: their number).</summary>
		TrailingData,

		/// <summary>The list of files is cut short (value: the number of files that could be read).</summary>
		TruncatedTree,

		/// <summary>The data of a file lies outside of the archive.</summary>
		EntryOutsideArchive,

		/// <summary>The size of a file is impossible.</summary>
		EntryInvalidSize,

		/// <summary>The data of a file cannot be decompressed.</summary>
		EntryCorrupt,

		/// <summary>Two files have the same path.</summary>
		DuplicateEntry,
	}

	/// <summary>
	/// How strictly an archive is read.
	/// </summary>
	public enum DatOpenMode
	{
		/// <summary>Any structural problem is an error.</summary>
		Strict,

		/// <summary>
		/// Recovers what can be recovered: bytes after the end of the archive are ignored and a list of files that
		/// is cut short is read as far as it goes. The problems are listed in <see cref="DatArchive.Issues"/>.
		/// </summary>
		Recover,
	}

	/// <summary>
	/// Kind of modifications not yet written to disk.
	/// </summary>
	[Flags]
	public enum DatPendingChanges
	{
		None = 0,

		/// <summary>The archive does not exist on disk yet.</summary>
		Created = 1,

		/// <summary>Files or folders were renamed (directory tree update only).</summary>
		Renamed = 2,

		/// <summary>Files were added.</summary>
		Added = 4,

		/// <summary>Files that are stored in the archive were removed.</summary>
		Deleted = 8,

		/// <summary>The archive was recovered and is to be written again (the original is kept as a backup).</summary>
		Repaired = 16,
	}
}