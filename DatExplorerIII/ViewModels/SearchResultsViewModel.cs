namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>The column of the list of the files found that it is sorted by.</summary>
	public enum SearchSortColumn
	{
		Name,
		Folder,
		Size,
	}

	/// <summary>
	/// The files found by a search: a list that is sorted by a column (a second click on the same column reverses the order) and
	/// where a file can be opened, which shows it in its folder.
	/// </summary>
	public sealed class SearchResultsViewModel : ObservableObject
	{
		private const String Ascending = "";
		private const String Descending = "";

		private readonly IReadOnlyList<ListEntry> _found;
		private readonly Action<DatEntry> _goTo;
		private SearchSortColumn _sortColumn = SearchSortColumn.Name;
		private Boolean _sortDescending;
		private IReadOnlyList<ListEntry> _items;
		private ListEntry? _selectedItem;

		public SearchResultsViewModel(IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
		{
			_goTo = goTo;
			_found = results.Select(entry => new ListEntry(entry)).ToList();
			_items = Sorted();
			Caption = Loc.Format("Str.SearchResultCount", results.Count);
			SortCommand = new RelayCommand(parameter => SortBy(Enum.Parse<SearchSortColumn>((String)parameter!)), parameter => parameter is String name && Enum.TryParse<SearchSortColumn>(name, out _));
			OpenCommand = new RelayCommand(Open, parameter => (parameter as ListEntry ?? SelectedItem)?.Entry != null);
		}

		/// <summary>What the list says: how many files were found.</summary>
		public String Caption { get; }

		/// <summary>The files, in the order of the column.</summary>
		public IReadOnlyList<ListEntry> Items
		{
			get => _items;
			private set => SetProperty(ref _items, value);
		}

		public ListEntry? SelectedItem
		{
			get => _selectedItem;
			set => SetProperty(ref _selectedItem, value);
		}

		public SearchSortColumn SortColumn => _sortColumn;

		public Boolean SortDescending => _sortDescending;

		/// <summary>Sorts by a column; the parameter is the name of a <see cref="SearchSortColumn"/>.</summary>
		public ICommand SortCommand { get; }

		/// <summary>Shows a file in its folder: the file of the parameter (a double click), else the selected one (the Enter key).</summary>
		public ICommand OpenCommand { get; }

		/// <summary>The arrow in the header of the name when the list is sorted by it (nothing otherwise).</summary>
		public String NameSortGlyph => GlyphOf(SearchSortColumn.Name);

		public String FolderSortGlyph => GlyphOf(SearchSortColumn.Folder);

		public String SizeSortGlyph => GlyphOf(SearchSortColumn.Size);

		public void SortBy(SearchSortColumn column)
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

			Items = Sorted();
			OnPropertyChanged(nameof(SortColumn));
			OnPropertyChanged(nameof(SortDescending));
			OnPropertyChanged(nameof(NameSortGlyph));
			OnPropertyChanged(nameof(FolderSortGlyph));
			OnPropertyChanged(nameof(SizeSortGlyph));
		}

		private String GlyphOf(SearchSortColumn column)
		{
			return _sortColumn != column ? String.Empty : _sortDescending ? Descending : Ascending;
		}

		private void Open(Object? parameter)
		{
			if ((parameter as ListEntry ?? SelectedItem)?.Entry is { } entry)
			{
				_goTo(entry);
			}
		}

		private IReadOnlyList<ListEntry> Sorted()
		{
			List<ListEntry> list = _found.ToList();
			list.Sort(Compare);
			return list;
		}

		private Int32 Compare(ListEntry a, ListEntry b)
		{
			Int32 byName = String.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
			Int32 byFolder = String.Compare(a.Entry?.Directory, b.Entry?.Directory, StringComparison.CurrentCultureIgnoreCase);
			Int32 primary = _sortColumn switch
			{
				SearchSortColumn.Folder => byFolder,
				SearchSortColumn.Size => (a.Entry?.UnpackedSize ?? 0).CompareTo(b.Entry?.UnpackedSize ?? 0),
				_ => byName,
			};

			if (primary != 0)
			{
				return _sortDescending ? -primary : primary;
			}

			// What ties is in the order of the names, then of the folders, whatever the direction.
			return byName != 0 ? byName : byFolder;
		}
	}
}