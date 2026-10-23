namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Core;
	using Models.Archives;
	using Models.Interface;
	using Services.Interface;

	public sealed partial class MainViewModel
	{
		/// <summary>Called by the view when the user selects a node of the tree.</summary>
		public void OnTreeSelectionChanged(FolderNode? node)
		{
			if (node != null && node != _selectedFolder)
				SelectedFolder = node;
		}

		public void SetSelectedItems(IReadOnlyList<ListEntry> items)
		{
			_selectedItems = items;
			OnPropertyChanged(nameof(SelectedItems));
			OnPropertyChanged(nameof(ExtractItemsText));
			OnPropertyChanged(nameof(IsMultiSelection));
			UpdatePreview();
		}

		public void SelectFolder(FolderNode node)
		{
			if (_selectedFolder != null && _selectedFolder != node)
				_selectedFolder.IsSelected = false;

			node.ExpandParents();
			node.IsSelected = true;
			SelectedFolder = node;
		}

		private void GoUp()
		{
			if (SelectedFolder?.Parent is { } parent)
				SelectFolder(parent);
		}

		private void GoTo(DatDocument document, DatEntry entry)
		{
			if (!Roots.Contains(document.Root))
				return;

			SelectFolder(document.FindNode(entry.Directory) ?? document.Root);

			ListEntry? item = Items.FirstOrDefault(candidate => candidate.Entry == entry);
			if (item != null)
				RevealRequested?.Invoke(item);
		}

		public void SortBy(DatSortColumn column)
		{
			if (_sortColumn == column)
			{
				_sortDescending = !_sortDescending;
			}
			else
			{
				_sortColumn = column;
				_sortDescending = false;
			}

			_settings.Data.SortColumn = _sortColumn.ToString();
			_settings.Data.SortDescending = _sortDescending;
			OnPropertyChanged(nameof(SortColumn));
			OnPropertyChanged(nameof(SortDescending));
			RefreshItems();
		}

		private void RefreshItems()
		{
			FolderNode? folder = _selectedFolder;
			if (folder == null)
			{
				CancelMessageScan();
				Items = Array.Empty<ListEntry>();
				FolderSummary = String.Empty;
				TotalText = String.Empty;
				PathText = String.Empty;
				return;
			}

			DatArchive archive = folder.Document.Archive;
			List<ListEntry> list = folder.Children.Select(child => new ListEntry(child)).ToList();
			Int32 folderCount = list.Count;
			list.AddRange(archive.GetEntries(folder.Path).Select(entry => new ListEntry(entry, archive.IsFolderBacked)));
			list.Sort(Compare);

			Items = list;
			StartMessageScan(archive, list);
			FolderSummary = Loc.Format("Str.StatusFolders", folderCount, list.Count - folderCount);
			TotalText = Loc.Format("Str.StatusTotal", archive.Count);
			PathText = Loc.Format("Str.StatusPath", "\\" + folder.Path);
		}

		private Int32 Compare(ListEntry a, ListEntry b)
		{
			if (a.IsFolder != b.IsFolder)
				return a.IsFolder ? -1 : 1;

			Int32 byName = String.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			Int32 primary = _sortColumn switch
			{
				DatSortColumn.Type => String.Compare(a.TypeText, b.TypeText, StringComparison.CurrentCultureIgnoreCase),
				DatSortColumn.Size => (a.Entry?.UnpackedSize ?? 0).CompareTo(b.Entry?.UnpackedSize ?? 0),
				DatSortColumn.PackedSize => GetPackedSize(a).CompareTo(GetPackedSize(b)),
				_ => byName,
			};

			if (primary != 0)
				return _sortDescending ? -primary : primary;

			return byName;
		}

		private static Int32 GetPackedSize(ListEntry item)
		{
			return item.Entry is { IsCompressed: true } entry ? entry.PackedSize : 0;
		}

		/// <summary>Selects the next (or previous) entry of the current folder whose name contains the search text.</summary>
		public ListEntry? FindInFolder(Boolean forward)
		{
			String text = _searchText.Trim();
			if (text.Length == 0 || Items.Count == 0)
				return null;

			Int32 count = Items.Count;
			ListEntry? current = forward ? SelectedItems.LastOrDefault() : SelectedItems.FirstOrDefault();
			Int32 start = current == null ? (forward ? -1 : count) : ListIndexOf(current);

			for (Int32 step = 1; step <= count; step++)
			{
				Int32 index = forward ? (start + step) % count : (((start - step) % count) + count) % count;
				if (Items[index].Name.Contains(text, StringComparison.OrdinalIgnoreCase))
				{
					SearchNotFound = false;
					return Items[index];
				}
			}

			SearchNotFound = true;
			return null;
		}

		private Int32 ListIndexOf(ListEntry item)
		{
			for (Int32 index = 0; index < Items.Count; index++)
			{
				if (Items[index] == item)
					return index;
			}

			return -1;
		}

		private void SearchEverywhere()
		{
			DatDocument? document = CurrentDocument;
			String text = _searchText.Trim();
			if (document == null)
				return;

			if (text.Length < 2)
			{
				SearchNotFound = true;
				_dialogs.ShowMessage(Title, Loc.Get("Str.SearchTooShort"), DialogIcon.Information);
				return;
			}

			List<DatEntry> found = document.Archive.Entries
				.Where(entry => entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase))
				.OrderBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase)
				.ToList();

			SearchNotFound = found.Count == 0;
			if (found.Count == 0)
			{
				StatusText = Loc.Get("Str.NothingFound");
				return;
			}

			_dialogs.ShowSearchResults(Loc.Format("Str.SearchResultsFor", text), found, entry => GoTo(document, entry));
		}
	}
}