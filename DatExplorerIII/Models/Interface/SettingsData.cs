namespace DatExplorerIII.Models.Interface
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Models.Comparison;
	using Models.GameFiles;
	using Services.Platform;

	/// <summary>
	/// The persisted user preferences.
	/// </summary>
	public sealed class SettingsData
	{
		/// <summary>The number of columns of the list of the files (name, type, size, packed size).</summary>
		public const Int32 ColumnCount = 4;

		public const Double MinimumColumnWidth = 20;

		public const Double MaximumColumnWidth = 2000;

		/// <summary>The limits of the width of the block of the folders, and of the share of the preview.</summary>
		public const Double MinimumTreeWidth = 100;

		public const Double MinimumPreviewShare = 0.1;

		public const Double MaximumPreviewShare = 10;

		public Boolean AssociationAsked { get; set; }

		public String? LastExtractFolder { get; set; }

		public String? LastImportFolder { get; set; }

		public String? LastDatFolder { get; set; }

		/// <summary>The archives opened last (full paths), the most recent first.</summary>
		public List<String> RecentFiles { get; set; } = [];

		public String ViewMode { get; set; } = "Details";

		/// <summary>The column the list of the files is sorted by (a name of <c>DatSortColumn</c>).</summary>
		public String SortColumn { get; set; } = "Name";

		/// <summary>Whether the list of the files is sorted from the last to the first.</summary>
		public Boolean SortDescending { get; set; }

		/// <summary>The widths of the columns of the list of the files (in the details view), in pixels; empty for the widths of the design.</summary>
		public List<Double> ColumnWidths { get; set; } = [];

		/// <summary>Whether the preview pane next to the file list is shown (the name is new: an old setting made when it was off by default is ignored).</summary>
		public Boolean PreviewPaneVisible { get; set; } = true;

		/// <summary>Whether the block of the folders (the tree) is shown.</summary>
		public Boolean TreeVisible { get; set; } = true;

		/// <summary>Whether the block of the files of the selected folder is shown.</summary>
		public Boolean FilesVisible { get; set; } = true;

		/// <summary>Whether a line shows the limit between the three blocks of the main window: folders, files, preview (on by default).</summary>
		public Boolean BlockSeparatorsVisible { get; set; } = true;

		/// <summary>Whether the files are extracted with no folders (the last choice of the extraction); false keeps the folders of the DAT.</summary>
		public Boolean ExtractFlat { get; set; }

		/// <summary>How the files are extracted (<see cref="ExtractMode"/>): "Ask" asks each time, with the last answer as the default one.</summary>
		public String ExtractMode { get; set; } = "Ask";

		/// <summary>How many notches of the wheel of the mouse change the magnification of a picture by one step (1 to 3).</summary>
		public Int32 WheelNotchesPerZoom { get; set; } = 3;

		/// <summary>Message files and dialogs: whether the comments at the end of the lines are shown (a column).</summary>
		public Boolean ShowLineComments { get; set; }

		/// <summary>Message files and dialogs: whether the comments between the lines are shown.</summary>
		public Boolean ShowBlockComments { get; set; }

		/// <summary>Whether a folder opened as an archive shows its hidden folders (hidden by Windows, or with a name that starts with a dot); off by default.</summary>
		public Boolean ShowHiddenFolders { get; set; }

		/// <summary>Comparison of two archives: the block of the folders and files above the block of the two archives (true), or beside it (false, the default).</summary>
		public Boolean CompareGroupsStacked { get; set; }

		/// <summary>Comparison: the tree of the folders above the list of the files (true, the default), or beside it (false).</summary>
		public Boolean CompareListStacked { get; set; } = true;

		/// <summary>Comparison: the left archive above the right one (true), or beside it (false).</summary>
		public Boolean CompareViewersStacked { get; set; }

		/// <summary>Comparison: whether the tree of the folders is shown (the list of the files has a box to choose the folder anyway).</summary>
		public Boolean CompareTreeVisible { get; set; } = true;

		/// <summary>Comparison: whether the differences of the lines of the files made of text are shown instead of the two versions side by side.</summary>
		public Boolean CompareShowDifferences { get; set; }

		/// <summary>Comparison: the message files (.msg, .mes) are compared by their ids only (the ids that are added or missing), not by their text.</summary>
		public Boolean CompareLanguageIds { get; set; }

		/// <summary>Comparison: what the comparison of the files ignores, and how it compares their content.</summary>
		public ComparisonOptions ComparisonOptions { get; set; } = new();

		/// <summary>Comparison: whether the two versions of a file scroll together.</summary>
		public Boolean CompareSyncScroll { get; set; } = true;

		/// <summary>The two archives that were compared last (offered again when the comparison is asked and not two archives are open).</summary>
		public String? CompareLastLeft { get; set; }

		public String? CompareLastRight { get; set; }

		/// <summary>Whether a frame is drawn around the main window (off by default).</summary>
		public Boolean WindowFrameVisible { get; set; }

		/// <summary>Whether a second launch of the program hands its files to the one that is running instead of opening a new window.</summary>
		public Boolean SingleInstance { get; set; } = true;

		/// <summary>The width of the scroll bars, in pixels.</summary>
		public Int32 ScrollBarWidth { get; set; } = 16;

		/// <summary>The smallest length of the slider of a scroll bar, in pixels (a long list would make it tiny).</summary>
		public Int32 ScrollBarMinimumThumb { get; set; } = 40;

		/// <summary>Whether the movies are shown with their subtitles (the .sve file that has the name of the movie), when they have some.</summary>
		public Boolean ShowSubtitles { get; set; } = true;

		/// <summary>The volume of the sounds of the viewer, 0 to 100.</summary>
		public Int32 AudioVolume { get; set; } = 100;

		/// <summary>Whether a sound of the viewer starts again when it ends.</summary>
		public Boolean AudioLoop { get; set; }

		/// <summary>Whether the list of the files shows every file (the default), or only those of the extensions of <see cref="VisibleExtensions"/>.</summary>
		public Boolean ShowAllExtensions { get; set; } = true;

		/// <summary>The extensions (with the dot, in lower case) that the list shows when it does not show all of them; null: all the formats the program knows.</summary>
		public List<String>? VisibleExtensions { get; set; }

		/// <summary>Whether a file of an extension that the program does not know is opened in the viewer as a text (the default), instead of not being viewable.</summary>
		public Boolean UnknownExtensionsAsText { get; set; } = true;

		/// <summary>The reader chosen for an extension (with the dot, in lower case), which comes before the one the program has for it.</summary>
		public Dictionary<String, ViewerReader> ExtensionReaders { get; set; } = [];

		/// <summary>The size in megabytes above which a file is not read for a preview.</summary>
		public Int32 PreviewLimitMegabytes { get; set; } = PreviewLimits.DefaultMegabytes;

		/// <summary>The limits of some extensions (with the dot), in megabytes: they come before <see cref="PreviewLimitMegabytes"/>.</summary>
		public Dictionary<String, Int32> PreviewLimitsByExtension { get; set; } = PreviewLimits.DefaultByExtension();

		/// <summary>Whether a double click on a file that has a viewer opens it there instead of in the default program.</summary>
		public Boolean DoubleClickOpensViewer { get; set; } = true;

		/// <summary>Whether a double click on a file does nothing (a folder still opens); the choice between the viewer and the program is kept for when it is off.</summary>
		public Boolean DoubleClickDisabled { get; set; }

		/// <summary>Whether the main window comes back with the size it had (and maximized if it was).</summary>
		public Boolean SaveWindowSize { get; set; } = true;

		/// <summary>Whether the main window comes back where it was.</summary>
		public Boolean SaveWindowPosition { get; set; } = true;

		/// <summary>Whether the width of the blocks of the main window (the folders, the preview) comes back as it was.</summary>
		public Boolean SaveBlockWidths { get; set; } = true;

		/// <summary>The width of the block of the folders, in pixels (0: the width of the design).</summary>
		public Double TreeWidth { get; set; }

		/// <summary>The width of the preview compared with the one of the list of the files (0: the proportion of the design).</summary>
		public Double PreviewShare { get; set; }

		/// <summary>The kinds of problems of the message files (.msg) that are not looked for (by default only the missing empty line at the end of the file, which the game does not mind).</summary>
		public List<MsgIssueKind> DisabledMessageChecks { get; set; } = [MsgIssueKind.NoFinalEmptyLine];

		/// <summary>Whether a double click on a message file (.msg) that has problems opens the window of the problems instead of the file.</summary>
		public Boolean DoubleClickMessageProblems { get; set; }

		/// <summary>The extensions that the list of the files shows, or null when it shows every file.</summary>
		public HashSet<String>? GetVisibleExtensionSet()
		{
			return ShowAllExtensions ? null : new HashSet<String>(VisibleExtensions ?? FileViewers.AllExtensions.ToList(), StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>Where the main window was when the program ended.</summary>
		public WindowPlacement? Window { get; set; }

		/// <summary>Automatic, Light or Dark.</summary>
		public String Theme { get; set; } = "Automatic";

		/// <summary>Code of the language chosen by the user; null follows Windows.</summary>
		public String? Language { get; set; }
	}
}