namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Reflection;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>
	/// State and behavior of the main window. The behavior is split by feature in the other files of this class.
	/// </summary>
	public sealed partial class MainViewModel : ObservableObject, IDisposable
	{
		private const Int64 MaxDragBytes = 512L * 1024 * 1024;
		private const Int32 LargeFileBytes = 1024 * 1024;
		private const Int32 MaxListedItems = 12;

		private readonly IDialogService _dialogs;
		private readonly SettingsService _settings;
		private readonly TempFolder _temp;
		private readonly IUiThread _uiThread;

		private FolderNode? _selectedFolder;
		private IReadOnlyList<ListEntry> _items = Array.Empty<ListEntry>();
		private IReadOnlyList<ListEntry> _selectedItems = Array.Empty<ListEntry>();
		private DatViewMode _viewMode;
		private DatSortColumn _sortColumn = DatSortColumn.Name;
		private Boolean _sortDescending;
		private String _searchText = String.Empty;
		private Boolean _searchNotFound;
		private String _statusText;
		private String _folderSummary = String.Empty;
		private String _totalText = String.Empty;
		private String _pathText = String.Empty;

		public MainViewModel(IDialogService dialogs, SettingsService settings, TempFolder temp, IUiThread uiThread)
		{
			_dialogs = dialogs;
			_uiThread = uiThread;
			_settings = settings;
			_temp = temp;
			Busy = new BusyState(dialogs);
			Preview = CreateViewer();
			_statusText = Loc.Get("Str.Ready");
			_viewMode = Enum.TryParse(settings.Data.ViewMode, out DatViewMode mode) ? mode : DatViewMode.Details;

			OpenCommand = new AsyncRelayCommand(PickAndOpenAsync, CanBrowse);
			OpenRecentCommand = new AsyncRelayCommand(OpenRecentAsync, _ => CanBrowse());
			ClearRecentCommand = new RelayCommand(ClearRecent);
			OpenFolderCommand = new AsyncRelayCommand(PickAndOpenFolderAsync, CanBrowse);
			SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => CanBrowse() && CurrentDocument != null);
			NewFallout1Command = new RelayCommand(() => CreateArchive(DatFormat.Fallout1), CanBrowse);
			NewFallout2Command = new RelayCommand(() => CreateArchive(DatFormat.Fallout2), CanBrowse);
			NewArcanumCommand = new RelayCommand(() => CreateArchive(DatFormat.Arcanum), CanBrowse);
			SaveCommand = new AsyncRelayCommand(() => SaveAsync(false), () => CanBrowse() && CurrentDocument?.ShouldSave == true);
			QuickSaveCommand = new AsyncRelayCommand(() => SaveAsync(true), () => CanBrowse() && CurrentDocument?.CanQuickSave == true);
			CloseDatCommand = new RelayCommand(CloseCurrentDocument, HasDocument);
			UpCommand = new RelayCommand(GoUp, () => CanBrowse() && SelectedFolder?.Parent != null);
			RefreshCommand = new RelayCommand(RefreshItems, HasDocument);

			OpenItemCommand = new AsyncRelayCommand(_ => OpenItemAsync(SelectedItems.FirstOrDefault()), _ => CanBrowse() && SelectedItems.Count > 0);
			ExtractItemsCommand = new AsyncRelayCommand(ExtractSelectedItemsAsync, _ => CanBrowse() && SelectedItems.Count > 0);
			ExtractFolderCommand = new AsyncRelayCommand(ExtractSelectedFolderAsync, _ => CanBrowse() && SelectedFolder is { IsRoot: false });
			ExtractAllCommand = new AsyncRelayCommand(ExtractAllAsync, _ => HasDocument());
			ImportFilesCommand = new AsyncRelayCommand(ImportFilesAsync, _ => HasDocument());
			ImportFolderCommand = new AsyncRelayCommand(ImportFolderAsync, _ => HasDocument());
			NewFolderCommand = new RelayCommand(CreateFolder, HasDocument);
			RenameItemCommand = new RelayCommand(RenameSelectedItem, () => CanBrowse() && SelectedItems.Count == 1);
			DeleteItemsCommand = new RelayCommand(DeleteSelectedItems, () => CanBrowse() && SelectedItems.Count > 0);
			LowercaseCommand = new RelayCommand(LowercaseSelectedFolder, () => CanBrowse() && SelectedFolder != null);
			RenameFolderCommand = new RelayCommand(RenameSelectedFolder, () => CanBrowse() && SelectedFolder is { IsRoot: false });
			DeleteFolderCommand = new RelayCommand(DeleteSelectedFolder, () => CanBrowse() && SelectedFolder is { IsRoot: false });

			GlobalSearchCommand = new RelayCommand(SearchEverywhere, HasDocument);
			VerifyCommand = new AsyncRelayCommand(VerifyAsync, _ => HasDocument());
			ShowProblemsCommand = new RelayCommand(ShowProblems, () => CanBrowse() && CurrentDocument != null);
			RemoveDamagedCommand = new RelayCommand(RemoveDamaged, () => CanBrowse() && CurrentDocument?.Archive.DamagedCount > 0);

			SetViewModeCommand = new RelayCommand(parameter => ViewMode = Enum.Parse<DatViewMode>((String)parameter!));
			AssociateCommand = new RelayCommand(Associate);
			AssociatePatchesCommand = new RelayCommand(AssociatePatches);
			ChangeLanguageCommand = new RelayCommand(parameter => ChangeLanguage((String)parameter!));
			ViewItemCommand = new AsyncRelayCommand(ViewSelectedItemAsync, _ => CanBrowse() && SelectedViewableEntry != null);
			OpenExternalCommand = new AsyncRelayCommand(_ => OpenExternalAsync(SelectedItems.FirstOrDefault()), _ => CanBrowse() && SelectedItems.Count > 0);
			SetDoubleClickCommand = new RelayCommand(parameter => SetDoubleClick((String)parameter!));
			AboutCommand = new RelayCommand(_dialogs.ShowAbout);
			SettingsCommand = new AsyncRelayCommand(OpenSettingsAsync, () => !Busy.IsBusy);
			CreateComparisonCommands();
			CreateInputCommands();
		}

		/// <summary>Raised when a list entry should be selected and scrolled into view.</summary>
		public event Action<ListEntry>? RevealRequested;

		public ICommand OpenCommand { get; }

		/// <summary>Opens one of the recent files; the parameter is its path.</summary>
		public ICommand OpenRecentCommand { get; }

		public ICommand ClearRecentCommand { get; }

		public ICommand OpenFolderCommand { get; }

		public ICommand SaveAsCommand { get; }

		/// <summary>The archives opened last (full paths), the most recent first.</summary>
		public IReadOnlyList<String> RecentFiles => _settings.Data.RecentFiles;

		public ICommand NewFallout1Command { get; }

		public ICommand NewFallout2Command { get; }

		public ICommand NewArcanumCommand { get; }

		public ICommand SaveCommand { get; }

		public ICommand QuickSaveCommand { get; }

		public ICommand CloseDatCommand { get; }

		public ICommand UpCommand { get; }

		public ICommand RefreshCommand { get; }

		public ICommand OpenItemCommand { get; }

		public ICommand ExtractItemsCommand { get; }

		public ICommand ExtractFolderCommand { get; }

		public ICommand ExtractAllCommand { get; }

		public ICommand ImportFilesCommand { get; }

		public ICommand ImportFolderCommand { get; }

		public ICommand NewFolderCommand { get; }

		public ICommand RenameItemCommand { get; }

		public ICommand DeleteItemsCommand { get; }

		public ICommand RenameFolderCommand { get; }

		public ICommand LowercaseCommand { get; }

		public ICommand DeleteFolderCommand { get; }

		public ICommand GlobalSearchCommand { get; }

		/// <summary>Reads every file of the archive to find the ones that are damaged.</summary>
		public ICommand VerifyCommand { get; }

		/// <summary>Lists what is wrong with the archive.</summary>
		public ICommand ShowProblemsCommand { get; }

		/// <summary>Removes the files that are known to be damaged.</summary>
		public ICommand RemoveDamagedCommand { get; }

		/// <summary>Shows the selected file in a window of its own.</summary>
		public ICommand ViewItemCommand { get; }

		/// <summary>Opens the selected file with the program Windows associates with it.</summary>
		public ICommand OpenExternalCommand { get; }

		public ICommand SetDoubleClickCommand { get; }

		public ICommand SetViewModeCommand { get; }

		public ICommand AssociateCommand { get; }

		public ICommand AssociatePatchesCommand { get; }

		/// <summary>Switches the language of the interface. The parameter is the code of the language.</summary>
		public ICommand ChangeLanguageCommand { get; }

		public ICommand AboutCommand { get; }

		public ICommand SettingsCommand { get; }

		/// <summary>The viewer of the preview pane, which follows the selection of the file list.</summary>
		public ViewerViewModel Preview { get; }

		/// <summary>The progress of the long operation that is running, if any.</summary>
		public BusyState Busy { get; }

		public ObservableCollection<FolderNode> Roots { get; } = new();

		public FolderNode? SelectedFolder
		{
			get => _selectedFolder;
			private set
			{
				if (SetProperty(ref _selectedFolder, value))
				{
					OnPropertyChanged(nameof(CurrentDocument));
					RefreshItems();
				}
			}
		}

		public DatDocument? CurrentDocument => _selectedFolder?.Document;

		public IReadOnlyList<ListEntry> Items
		{
			get => _items;
			private set => SetProperty(ref _items, value);
		}

		public IReadOnlyList<ListEntry> SelectedItems => _selectedItems;

		public DatViewMode ViewMode
		{
			get => _viewMode;
			set
			{
				_viewMode = value;
				_settings.Data.ViewMode = value.ToString();
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsDetailsView));
				OnPropertyChanged(nameof(IsListView));
				OnPropertyChanged(nameof(IsIconsView));
			}
		}

		public Boolean IsDetailsView => _viewMode == DatViewMode.Details;

		public Boolean IsListView => _viewMode == DatViewMode.List;

		public Boolean IsIconsView => _viewMode == DatViewMode.Icons;

		public DatSortColumn SortColumn => _sortColumn;

		public Boolean SortDescending => _sortDescending;

		public String SearchText
		{
			get => _searchText;
			set
			{
				if (SetProperty(ref _searchText, value))
				{
					SearchNotFound = false;
				}
			}
		}

		public Boolean SearchNotFound
		{
			get => _searchNotFound;
			private set => SetProperty(ref _searchNotFound, value);
		}

		public String Title { get; } = $"DatExplorerIII v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)} - by Kain";

		public String StatusText
		{
			get => _statusText;
			private set => SetProperty(ref _statusText, value);
		}

		public String FolderSummary
		{
			get => _folderSummary;
			private set => SetProperty(ref _folderSummary, value);
		}

		public String TotalText
		{
			get => _totalText;
			private set => SetProperty(ref _totalText, value);
		}

		public String PathText
		{
			get => _pathText;
			private set => SetProperty(ref _pathText, value);
		}

		/// <summary>The archives can be browsed and changed when no operation runs and the window is not in the comparison mode.</summary>
		private Boolean CanBrowse()
		{
			return !Busy.IsBusy && !IsComparing;
		}

		private Boolean HasDocument()
		{
			return CanBrowse() && CurrentDocument != null;
		}

		/// <summary>Opens the files given on the command line, or offers to associate .dat files on the first start.</summary>
		public async Task StartAsync(IReadOnlyList<String> arguments)
		{
			if (arguments.Count > 0)
			{
				await OpenFilesAsync(arguments);
			}
			else if (!_settings.Data.AssociationAsked)
			{
				_settings.Data.AssociationAsked = true;
				_settings.Save();

				if (!FileAssociationService.IsAssociated)
				{
					Associate();
				}
			}
		}

		/// <summary>
		/// Whether the window may close: not while an operation runs (an archive would be left half written),
		/// and only after confirmation when archives have unsaved changes.
		/// </summary>
		public Boolean ConfirmExit()
		{
			if (Busy.IsBusy)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.BusyCannotClose"), DialogIcon.Information);
				return false;
			}

			Boolean unsaved = Roots.Any(root => root.Document.ShouldSave);
			return !unsaved || _dialogs.Confirm(Title, Loc.Get("Str.QuitUnsaved"), DialogIcon.Warning);
		}

		/// <summary>The place the window had when the program ended last time, if it was ever saved.</summary>
		public WindowPlacement? LoadPlacement()
		{
			return _settings.Data.Window;
		}

		public void SavePlacement(WindowPlacement placement)
		{
			_settings.Data.Window = WindowPlacement.ForSaving(_settings.Data.Window, placement, SaveWindowSize, SaveWindowPosition);
		}

		/// <summary>Frees what the program holds for its whole life (called when it exits, after <see cref="Shutdown"/>).</summary>
		public void Dispose()
		{
			_forwardedOpens.Dispose();
		}

		public void Shutdown()
		{
			Preview.Dispose();
			foreach (FolderNode root in Roots)
			{
				root.Document.Archive.Dispose();
			}

			_settings.Save();
			_temp.Cleanup();
		}
	}
}