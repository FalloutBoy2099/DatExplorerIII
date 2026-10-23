namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Models.Archives;

	/// <summary>What the main view model gives to the parts it owns (the comparison), without making it public.</summary>
	public sealed partial class MainViewModel
	{
		ObservableCollection<FolderNode> IComparisonHost.Roots => Roots;

		BusyState IComparisonHost.Busy => Busy;

		String IComparisonHost.Title => Title;

		String IComparisonHost.StatusText
		{
			get => StatusText;
			set => StatusText = value;
		}

		Boolean IComparisonHost.CanBrowse() => CanBrowse();

		Task IComparisonHost.OpenArchiveAsync(String path) => OpenArchiveAsync(path);

		Task<Boolean> IComparisonHost.RunBusyAsync(String title, Boolean canCancel, Func<IProgress<DatProgress>, CancellationToken, Task> work) => RunBusyAsync(title, canCancel, work);

		Task IComparisonHost.SaveDocumentAsync(DatDocument? document, Boolean quick) => SaveDocumentAsync(document, quick);

		void IComparisonHost.RefreshItems() => RefreshItems();

		IEnumerable<DatArchive> IComparisonHost.OpenArchives() => OpenArchives();
	}
}