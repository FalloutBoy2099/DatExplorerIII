namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The persisted user preferences.
	/// </summary>
	public sealed class SettingsData
	{
		public Boolean AssociationAsked { get; set; }

		public String? LastExtractFolder { get; set; }

		public String? LastImportFolder { get; set; }

		public String? LastDatFolder { get; set; }

		/// <summary>The archives opened last (full paths), the most recent first.</summary>
		public List<String> RecentFiles { get; set; } = new();

		public String ViewMode { get; set; } = "Details";

		/// <summary>Whether the preview pane next to the file list is shown (the name is new: an old setting made when it was off by default is ignored).</summary>
		public Boolean PreviewPaneVisible { get; set; } = true;

		/// <summary>Whether a line shows the limit between the three blocks of the main window: folders, files, preview (on by default).</summary>
		public Boolean BlockSeparatorsVisible { get; set; } = true;

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

		/// <summary>Whether a double click on a file that has a viewer opens it there instead of in the default program.</summary>
		public Boolean DoubleClickOpensViewer { get; set; } = true;

		/// <summary>Whether a double click on a file does nothing (a folder still opens); the choice between the viewer and the program is kept for when it is off.</summary>
		public Boolean DoubleClickDisabled { get; set; }

		/// <summary>Whether the main window comes back with the size it had (and maximized if it was).</summary>
		public Boolean SaveWindowSize { get; set; } = true;

		/// <summary>Whether the main window comes back where it was.</summary>
		public Boolean SaveWindowPosition { get; set; } = true;

		/// <summary>Where the main window was when the program ended.</summary>
		public WindowPlacement? Window { get; set; }

		/// <summary>Automatic, Light or Dark.</summary>
		public String Theme { get; set; } = "Automatic";

		/// <summary>Code of the language chosen by the user; null follows Windows.</summary>
		public String? Language { get; set; }
	}
}