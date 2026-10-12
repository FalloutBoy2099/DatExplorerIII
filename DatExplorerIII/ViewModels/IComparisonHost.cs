namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Models;

	/// <summary>What the comparison needs of the main view model: the archives that are open, and the operations on them.</summary>
	internal interface IComparisonHost
	{
		ObservableCollection<FolderNode> Roots { get; }

		BusyState Busy { get; }

		String Title { get; }

		String StatusText { get; set; }

		/// <summary>Whether a long operation or a comparison leaves the archives free to be browsed and changed.</summary>
		Boolean CanBrowse();

		/// <summary>Opens an archive (a file or a folder) in the window.</summary>
		Task OpenArchiveAsync(String path);

		Task<Boolean> RunBusyAsync(String title, Boolean canCancel, Func<IProgress<DatProgress>, CancellationToken, Task> work);

		Task SaveDocumentAsync(DatDocument? document, Boolean quick);

		/// <summary>Lists the files of the selected folder again.</summary>
		void RefreshItems();

		/// <summary>The archives that are open.</summary>
		IEnumerable<DatArchive> OpenArchives();
	}
}