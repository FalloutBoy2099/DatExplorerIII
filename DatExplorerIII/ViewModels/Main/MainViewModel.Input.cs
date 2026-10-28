namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Models.Interface;
	using Services.Platform;

	/// <summary>What the keyboard and the mouse ask for, as commands: the shortcuts and the drag and drop of files.</summary>
	public sealed partial class MainViewModel
	{
		private Int32 _searchFocusRequests;

		/// <summary>Opens the DAT archives dropped on the tree; the parameter is the list of paths.</summary>
		public ICommand OpenDroppedCommand { get; private set; } = null!;

		/// <summary>Imports the files and folders dropped on the list (or opens them when no archive is open); the parameter is the list of paths.</summary>
		public ICommand ImportDroppedCommand { get; private set; } = null!;

		/// <summary>Makes real files of a folder of the tree dragged to the file explorer.</summary>
		public IDragOutSource FolderDragSource { get; private set; } = null!;

		/// <summary>Makes real files of the selected entries of the list dragged to the file explorer.</summary>
		public IDragOutSource ListDragSource { get; private set; } = null!;

		/// <summary>The tree has a new selected folder; the parameter is its node (null when nothing is selected).</summary>
		public ICommand TreeSelectionChangedCommand { get; private set; } = null!;

		/// <summary>The selection of the list changed; the parameter is the list of the selected entries.</summary>
		public ICommand ListSelectionChangedCommand { get; private set; } = null!;

		/// <summary>Sorts the list by a column; the parameter is the name of a <see cref="DatSortColumn"/>.</summary>
		public ICommand SortCommand { get; private set; } = null!;

		/// <summary>The widths of the columns of the list of the files, kept for the next run (empty: the widths of the design).</summary>
		public IReadOnlyList<Double> ColumnWidths
		{
			get => _settings.Data.ColumnWidths;
			set
			{
				if (value is { Count: SettingsData.ColumnCount } && !value.SequenceEqual(_settings.Data.ColumnWidths))
				{
					_settings.Data.ColumnWidths = value.ToList();
					OnPropertyChanged();
				}
			}
		}

		public ICommand TogglePreviewCommand { get; private set; } = null!;

		public ICommand ToggleTreeCommand { get; private set; } = null!;

		public ICommand ToggleFilesCommand { get; private set; } = null!;

		/// <summary>Asks for the search box to take the focus.</summary>
		public ICommand FocusSearchCommand { get; private set; } = null!;

		/// <summary>Goes to the next entry of the folder whose name contains the search text.</summary>
		public ICommand FindNextCommand { get; private set; } = null!;

		public ICommand FindPreviousCommand { get; private set; } = null!;

		/// <summary>Changes each time <see cref="FocusSearchCommand"/> runs; the search box takes the focus when it changes.</summary>
		public Int32 SearchFocusRequests
		{
			get => _searchFocusRequests;
			private set => SetProperty(ref _searchFocusRequests, value);
		}

		private void CreateInputCommands()
		{
			OpenDroppedCommand = new AsyncRelayCommand(
				parameter => OpenDroppedAsync(PathsOf(parameter).Where(DatFiles.IsArchiveFile).ToList()),
				parameter => PathsOf(parameter).Any(DatFiles.IsArchiveFile));
			ImportDroppedCommand = new AsyncRelayCommand(
				parameter => ImportDroppedAsync(PathsOf(parameter)),
				parameter => PathsOf(parameter).Count > 0 && CurrentDocument?.Archive.IsReadOnly != true);
			FolderDragSource = new DelegateDragOutSource(item => item is FolderNode node ? PrepareDragOut(node) : null);
			ListDragSource = new DelegateDragOutSource(_ => SelectedItems.Count > 0 ? PrepareDragOut(SelectedItems.ToList()) : null);

			TreeSelectionChangedCommand = new RelayCommand(parameter => OnTreeSelectionChanged(parameter as FolderNode));
			ListSelectionChangedCommand = new RelayCommand(parameter => SetSelectedItems(((IEnumerable<Object>)parameter!).OfType<ListEntry>().ToList()), parameter => parameter is IEnumerable<Object>);
			SortCommand = new RelayCommand(parameter => SortBy(Enum.Parse<DatSortColumn>((String)parameter!)), parameter => parameter is String name && Enum.TryParse<DatSortColumn>(name, out _));
			TogglePreviewCommand = new RelayCommand(() => IsPreviewVisible = !IsPreviewVisible);
			ToggleTreeCommand = new RelayCommand(() => IsTreeVisible = !IsTreeVisible);
			ToggleFilesCommand = new RelayCommand(() => IsFilesVisible = !IsFilesVisible);
			FocusSearchCommand = new RelayCommand(() => SearchFocusRequests++);
			FindNextCommand = new RelayCommand(() => Find(true));
			FindPreviousCommand = new RelayCommand(() => Find(false));
		}

		/// <summary>Selects the next (or previous) file of the current folder, the last one being followed by the first one.</summary>
		public void StepFile(Boolean forward)
		{
			Int32 count = Items.Count;
			if (count == 0)
				return;

			ListEntry? current = forward ? SelectedItems.LastOrDefault() : SelectedItems.FirstOrDefault();
			Int32 start = current == null ? (forward ? -1 : count) : ListIndexOf(current);
			for (Int32 step = 1; step <= count; step++)
			{
				Int32 index = forward ? (start + step) % count : (((start - step) % count) + count) % count;
				if (!Items[index].IsFolder)
				{
					RevealRequested?.Invoke(Items[index]);
					return;
				}
			}
		}

		private static IReadOnlyList<String> PathsOf(Object? parameter)
		{
			return parameter as IReadOnlyList<String> ?? Array.Empty<String>();
		}

		private void Find(Boolean forward)
		{
			ListEntry? found = FindInFolder(forward);
			if (found != null)
				RevealRequested?.Invoke(found);
		}
	}
}