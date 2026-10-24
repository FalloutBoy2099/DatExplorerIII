namespace DatExplorerIII.Models.Interface
{
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
	/// How the files are extracted: the question is asked each time, or the folders of the DAT are kept, or every file goes straight in the destination.
	/// </summary>
	public enum ExtractMode
	{
		Ask,
		KeepTree,
		Flat,
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
}