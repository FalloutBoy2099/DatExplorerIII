namespace DatExplorerIII.Models
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

		/// <summary>Temple of Elemental Evil: the footer of Arcanum, and an index that is a tree of folders. It is read, not written.</summary>
		Toee = 4,
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

	/// <summary>
	/// Column the file list is sorted by.
	/// </summary>
	public enum DatSortColumn
	{
		Name,
		Type,
		Size,
		PackedSize,
	}

	/// <summary>
	/// How the files of a folder are presented.
	/// </summary>
	public enum DatViewMode
	{
		Details,
		List,
		Icons,
	}

	/// <summary>
	/// The picture of a message dialog.
	/// </summary>
	public enum DialogIcon
	{
		None,
		Information,
		Question,
		Warning,
		Error,
	}

	/// <summary>
	/// The theme choice of the user: follow Windows, or always light, or always dark.
	/// </summary>
	public enum ThemePreference
	{
		Automatic,
		Light,
		Dark,
	}

	/// <summary>
	/// How the content of a file is shown.
	/// </summary>
	public enum FileViewerKind
	{
		/// <summary>As a text.</summary>
		Text,

		/// <summary>As a picture.</summary>
		Image,

		/// <summary>As a sound that can be played.</summary>
		Audio,

		/// <summary>As a movie that can be played.</summary>
		Video,

		/// <summary>As the sheet of a character.</summary>
		Character,

		/// <summary>As the description of an object of the game (a prototype).</summary>
		Prototype,

		/// <summary>As a table (messages, dialogs).</summary>
		Table,

		/// <summary>As what a map of the game holds.</summary>
		Map,
	}

	/// <summary>
	/// The kind of object a prototype file (.pro) describes. It is the high byte of the identifier of the object.
	/// </summary>
	public enum ProtoType
	{
		Item,
		Critter,
		Scenery,
		Wall,
		Tile,
		Misc,
	}

	/// <summary>
	/// What an item is.
	/// </summary>
	public enum ItemType
	{
		Armor,
		Container,
		Drug,
		Weapon,
		Ammo,
		Misc,
		Key,
	}

	/// <summary>
	/// What a piece of scenery is.
	/// </summary>
	public enum SceneryType
	{
		Door,
		Stairs,
		Elevator,
		LadderBottom,
		LadderTop,
		Generic,
	}

	/// <summary>
	/// The kind of object a prototype file (.pro) of Arcanum describes.
	/// </summary>
	public enum ArcanumObjectType
	{
		Wall = 0,
		Portal = 1,
		Container = 2,
		Scenery = 3,
		Projectile = 4,
		Weapon = 5,
		Ammo = 6,
		Armor = 7,
		Gold = 8,
		Food = 9,
		Scroll = 10,
		Key = 11,
		KeyRing = 12,
		Written = 13,
		Generic = 14,
		Pc = 15,
		Npc = 16,
		Trap = 17,
	}

	/// <summary>
	/// What an object is made of (it gives the sound it makes when it is hit).
	/// </summary>
	public enum MaterialType
	{
		Glass,
		Metal,
		Plastic,
		Wood,
		Dirt,
		Stone,
		Cement,
		Leather,
	}

	/// <summary>
	/// The statistics of a character, in the order they are stored in a character file (.gcd).
	/// </summary>
	public enum CharacterStat
	{
		Strength,
		Perception,
		Endurance,
		Charisma,
		Intelligence,
		Agility,
		Luck,
		HitPoints,
		ActionPoints,
		ArmorClass,
		UnarmedDamage,
		MeleeDamage,
		CarryWeight,
		Sequence,
		HealingRate,
		CriticalChance,
		BetterCriticals,
		ThresholdNormal,
		ThresholdLaser,
		ThresholdFire,
		ThresholdPlasma,
		ThresholdElectrical,
		ThresholdEmp,
		ThresholdExplosive,
		ResistanceNormal,
		ResistanceLaser,
		ResistanceFire,
		ResistancePlasma,
		ResistanceElectrical,
		ResistanceEmp,
		ResistanceExplosive,
		RadiationResistance,
		PoisonResistance,
		Age,
		Gender,
	}
}