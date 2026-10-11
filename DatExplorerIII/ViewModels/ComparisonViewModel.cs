namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats;
	using Infrastructure;
	using Models;
	using Scripts;
	using Services;

	/// <summary>A file of the comparison of two archives, as a line of the list.</summary>
	public sealed class ComparisonRow : ObservableObject
	{
		private DatEntry? _left;
		private DatEntry? _right;
		private DatCompareState _state;

		public ComparisonRow(DatCompareItem item)
		{
			Path = item.Path;
			Directory = item.Path[..(item.Path.LastIndexOf('\\') + 1)];
			_state = item.State;
			_left = item.Left;
			_right = item.Right;
		}

		public String Path { get; }

		/// <summary>The folder of the file: "" for the root, otherwise the path with a backslash at the end.</summary>
		public String Directory { get; }

		/// <summary>The file in the left archive; null when only the right one has it.</summary>
		public DatEntry? Left => _left;

		/// <summary>The file in the right archive; null when only the left one has it.</summary>
		public DatEntry? Right => _right;

		public DatCompareState State => _state;

		public String StateText => Loc.Get("Str.CompareState" + (Int32)_state);

		/// <summary>The icon of the state: a tick, a warning, an arrow towards the side that has the file.</summary>
		public String Glyph => _state switch
		{
			DatCompareState.Identical => "",
			DatCompareState.Different => "",
			DatCompareState.LeftOnly => "",
			_ => "",
		};

		public String LeftSizeText => _left == null ? String.Empty : SizeFormatter.Format(_left.UnpackedSize);

		public String RightSizeText => _right == null ? String.Empty : SizeFormatter.Format(_right.UnpackedSize);

		/// <summary>The files and the state after a copy or a move.</summary>
		public void Update(DatEntry? left, DatEntry? right, DatCompareState state)
		{
			_left = left;
			_right = right;
			_state = state;
			OnPropertyChanged(nameof(Left));
			OnPropertyChanged(nameof(Right));
			OnPropertyChanged(nameof(State));
			OnPropertyChanged(nameof(StateText));
			OnPropertyChanged(nameof(Glyph));
			OnPropertyChanged(nameof(LeftSizeText));
			OnPropertyChanged(nameof(RightSizeText));
		}

		public void RefreshLanguage()
		{
			OnPropertyChanged(nameof(StateText));
			OnPropertyChanged(nameof(LeftSizeText));
			OnPropertyChanged(nameof(RightSizeText));
		}
	}

	/// <summary>A folder of the two archives together: a node of the tree, a line of the box that chooses the folder.</summary>
	public sealed class ComparisonFolder : ObservableObject
	{
		private Boolean _isExpanded;
		private Boolean _isSelected;
		private Int32 _differences;
		private String _name;

		public ComparisonFolder(String name, String path, ComparisonFolder? parent)
		{
			_name = name;
			Path = path;
			Parent = parent;
			Depth = parent == null ? 0 : parent.Depth + 1;
		}

		/// <summary>Called when the folder becomes the selected one (by the tree, which sets <see cref="IsSelected"/>).</summary>
		internal Action<ComparisonFolder>? Selected { get; set; }

		public String Name
		{
			get => _name;
			set
			{
				if (SetProperty(ref _name, value))
				{
					OnPropertyChanged(nameof(Display));
				}
			}
		}

		/// <summary>"" for the root, otherwise the path with a backslash at the end.</summary>
		public String Path { get; }

		public ComparisonFolder? Parent { get; }

		public Int32 Depth { get; }

		public List<ComparisonFolder> Children { get; } = new();

		/// <summary>How many files of the folder and of its sub folders are not identical in the two archives.</summary>
		public Int32 Differences
		{
			get => _differences;
			set
			{
				if (SetProperty(ref _differences, value))
				{
					OnPropertyChanged(nameof(Display));
					OnPropertyChanged(nameof(CountText));
					OnPropertyChanged(nameof(HasDifferences));
				}
			}
		}

		public Boolean HasDifferences => _differences > 0;

		public String CountText => _differences > 0 ? "(" + _differences.ToString(System.Globalization.CultureInfo.CurrentCulture) + ")" : String.Empty;

		/// <summary>The line of the box that chooses the folder: indented by the depth, with the number of differences.</summary>
		public String Display => new String('\u00A0', Depth * 3) + _name + (_differences > 0 ? "  " + CountText : String.Empty);

		public Boolean IsExpanded
		{
			get => _isExpanded;
			set => SetProperty(ref _isExpanded, value);
		}

		public Boolean IsSelected
		{
			get => _isSelected;
			set
			{
				if (SetProperty(ref _isSelected, value) && value)
				{
					Selected?.Invoke(this);
				}
			}
		}
	}

	/// <summary>
	/// The comparison of two open archives, shown in the main window: the files of both with their state (identical, different,
	/// only on one side), filters, the selection, and the two versions of the selected file side by side.
	/// </summary>
	public sealed class ComparisonViewModel : ObservableObject, IDisposable
	{
		private IReadOnlyList<ComparisonRow> _rows = [];
		private IReadOnlyList<ComparisonRow> _selected = [];
		private Boolean _showIdentical;
		private Boolean _showDifferent = true;
		private Boolean _showLeftOnly = true;
		private Boolean _showRightOnly = true;
		private Boolean _includeSubfolders = true;
		private Boolean _showDifferences;
		private CancellationTokenSource? _diffCancellation;
		private ComparisonFolder _selectedFolder;
		private readonly Dictionary<String, ComparisonFolder> _folderByPath = new(StringComparer.OrdinalIgnoreCase);

		private readonly Func<IEnumerable<DatArchive>> _archives;

		public ComparisonViewModel(DatDocument left, DatDocument right, IEnumerable<DatCompareItem> items, Func<IEnumerable<DatArchive>> archives, ComparisonOptions? options = null)
		{
			_archives = archives;
			Options = options ?? new ComparisonOptions();
			Left = left;
			Right = right;
			AllRows = items.Select(item => new ComparisonRow(item)).ToList();
			LeftViewer = new ViewerViewModel { OtherArchives = archives };
			RightViewer = new ViewerViewModel { OtherArchives = archives };

			Diff = new DiffViewModel();
			Root = BuildFolders();
			_selectedFolder = Root;
			Root.IsSelected = true;
			Root.IsExpanded = true;
			UpdateDifferences();
			ApplyFilter();
			ShowSelection();
		}

		/// <summary>What the comparison ignored: the differences are shown the same way.</summary>
		public ComparisonOptions Options { get; }

		/// <summary>The differences of the selected file, when the option to show them is on.</summary>
		public DiffViewModel Diff { get; }

		/// <summary>
		/// Whether the two versions of the selected file are shown as the differences between their lines (only for files made of
		/// text) instead of side by side in the viewers.
		/// </summary>
		public Boolean ShowDifferences
		{
			get => _showDifferences;
			set
			{
				if (SetProperty(ref _showDifferences, value))
				{
					ShowSelection();
				}
			}
		}

		/// <summary>The last computation of the differences (the tests wait for it).</summary>
		internal Task DiffTask { get; private set; } = Task.CompletedTask;

		/// <summary>The folder that holds everything (the one the comparison starts with).</summary>
		public ComparisonFolder Root { get; }

		/// <summary>The roots of the tree of the folders (only <see cref="Root"/>).</summary>
		public IReadOnlyList<ComparisonFolder> Tree => [Root];

		/// <summary>Every folder, each followed by its sub folders: the lines of the box that chooses the folder.</summary>
		public IReadOnlyList<ComparisonFolder> FlatFolders { get; private set; } = [];

		/// <summary>The folder whose files the list shows (and those of its sub folders, as asked).</summary>
		public ComparisonFolder SelectedFolder
		{
			get => _selectedFolder;
			set
			{
				if (value is not null)
				{
					SelectFolder(value);
				}
			}
		}

		/// <summary>Whether the list shows the files of the sub folders of the folder as well.</summary>
		public Boolean IncludeSubfolders
		{
			get => _includeSubfolders;
			set
			{
				if (SetProperty(ref _includeSubfolders, value))
				{
					ApplyFilter();
				}
			}
		}

		private void SelectFolder(ComparisonFolder folder)
		{
			if (ReferenceEquals(folder, _selectedFolder))
			{
				return;
			}

			ComparisonFolder previous = _selectedFolder;
			_selectedFolder = folder;
			previous.IsSelected = false;
			folder.IsSelected = true;

			for (ComparisonFolder? parent = folder.Parent; parent != null; parent = parent.Parent)
			{
				parent.IsExpanded = true;
			}

			OnPropertyChanged(nameof(SelectedFolder));
			ApplyFilter();
		}

		/// <summary>The tree of the folders of every file of the two archives.</summary>
		private ComparisonFolder BuildFolders()
		{
			ComparisonFolder root = new(Loc.Get("Str.CompareAllFolders"), String.Empty, null) { Selected = SelectFolder };
			_folderByPath[String.Empty] = root;

			foreach (ComparisonRow row in AllRows)
			{
				ComparisonFolder parent = root;
				Int32 end = row.Directory.IndexOf('\\');
				while (end >= 0)
				{
					String path = row.Directory[..(end + 1)];
					if (!_folderByPath.TryGetValue(path, out ComparisonFolder? folder))
					{
						String name = path.TrimEnd('\\');
						folder = new ComparisonFolder(name[(name.LastIndexOf('\\') + 1)..], path, parent) { Selected = SelectFolder };
						parent.Children.Add(folder);
						_folderByPath[path] = folder;
					}

					parent = folder;
					end = row.Directory.IndexOf('\\', end + 1);
				}
			}

			List<ComparisonFolder> flat = new();
			void Walk(ComparisonFolder folder)
			{
				flat.Add(folder);
				folder.Children.Sort((a, b) => String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
				foreach (ComparisonFolder child in folder.Children)
				{
					Walk(child);
				}
			}

			Walk(root);
			FlatFolders = flat;
			return root;
		}

		/// <summary>Counts, for every folder, the files that are not identical in it and in its sub folders.</summary>
		private void UpdateDifferences()
		{
			foreach (ComparisonFolder folder in FlatFolders)
			{
				folder.Differences = 0;
			}

			foreach (ComparisonRow row in AllRows.Where(row => row.State != DatCompareState.Identical))
			{
				for (ComparisonFolder? folder = _folderByPath[row.Directory]; folder != null; folder = folder.Parent)
				{
					folder.Differences++;
				}
			}
		}

		public DatDocument Left { get; }

		public DatDocument Right { get; }

		/// <summary>The name of the left archive with its kind ("[F2] master.dat").</summary>
		public String LeftTitle => Left.Root.DisplayName;

		public String RightTitle => Right.Root.DisplayName;

		public String LeftToolTip => Left.FilePath;

		public String RightToolTip => Right.FilePath;

		/// <summary>Every file of the two archives.</summary>
		public IReadOnlyList<ComparisonRow> AllRows { get; }

		/// <summary>The files the filters let through.</summary>
		public IReadOnlyList<ComparisonRow> Rows
		{
			get => _rows;
			private set => SetProperty(ref _rows, value);
		}

		/// <summary>The files that are selected in the list.</summary>
		public IReadOnlyList<ComparisonRow> Selected => _selected;

		public ViewerViewModel LeftViewer { get; }

		public ViewerViewModel RightViewer { get; }

		public Boolean ShowIdentical
		{
			get => _showIdentical;
			set
			{
				if (SetProperty(ref _showIdentical, value))
				{
					ApplyFilter();
				}
			}
		}

		public Boolean ShowDifferent
		{
			get => _showDifferent;
			set
			{
				if (SetProperty(ref _showDifferent, value))
				{
					ApplyFilter();
				}
			}
		}

		public Boolean ShowLeftOnly
		{
			get => _showLeftOnly;
			set
			{
				if (SetProperty(ref _showLeftOnly, value))
				{
					ApplyFilter();
				}
			}
		}

		public Boolean ShowRightOnly
		{
			get => _showRightOnly;
			set
			{
				if (SetProperty(ref _showRightOnly, value))
				{
					ApplyFilter();
				}
			}
		}

		public Int32 Count(DatCompareState state) => AllRows.Count(row => row.State == state);

		public String IdenticalFilterText => Loc.Format("Str.CompareFilterIdentical", Count(DatCompareState.Identical));

		public String DifferentFilterText => Loc.Format("Str.CompareFilterDifferent", Count(DatCompareState.Different));

		public String LeftOnlyFilterText => Loc.Format("Str.CompareFilterLeftOnly", Count(DatCompareState.LeftOnly));

		public String RightOnlyFilterText => Loc.Format("Str.CompareFilterRightOnly", Count(DatCompareState.RightOnly));

		public String SummaryText => Loc.Format(
			"Str.CompareSummary",
			AllRows.Count,
			Count(DatCompareState.Identical),
			Count(DatCompareState.Different),
			Count(DatCompareState.LeftOnly),
			Count(DatCompareState.RightOnly));

		/// <summary>True when something of the selection can go to the right (a file of the left archive is selected).</summary>
		public Boolean CanSendRight => _selected.Any(row => row.Left != null);

		public Boolean CanSendLeft => _selected.Any(row => row.Right != null);

		public void SetSelection(IReadOnlyList<ComparisonRow> rows)
		{
			_selected = rows;
			OnPropertyChanged(nameof(Selected));
			OnPropertyChanged(nameof(CanSendRight));
			OnPropertyChanged(nameof(CanSendLeft));
			ShowSelection();
		}

		/// <summary>After files were copied or moved: the states, the filters and the counts are made again.</summary>
		public void Refresh()
		{
			UpdateDifferences();
			ApplyFilter();
			OnPropertyChanged(nameof(CanSendRight));
			OnPropertyChanged(nameof(CanSendLeft));
			ShowSelection();
		}

		/// <summary>The names of the archives change when one is saved under another name.</summary>
		public void RefreshTitles()
		{
			OnPropertyChanged(nameof(LeftTitle));
			OnPropertyChanged(nameof(RightTitle));
			OnPropertyChanged(nameof(LeftToolTip));
			OnPropertyChanged(nameof(RightToolTip));
		}

		public void RefreshLanguage()
		{
			foreach (ComparisonRow row in AllRows)
			{
				row.RefreshLanguage();
			}

			Root.Name = Loc.Get("Str.CompareAllFolders");
			Diff.RefreshLanguage();
			NotifyCounts();
			LeftViewer.RefreshLanguage();
			RightViewer.RefreshLanguage();
		}

		public void Dispose()
		{
			_diffCancellation?.Cancel();
			_diffCancellation?.Dispose();
			_diffCancellation = null;
			_diffCancellation?.Cancel();
			LeftViewer.Dispose();
			RightViewer.Dispose();
		}

		private void ApplyFilter()
		{
			String folder = _selectedFolder.Path;
			Rows = AllRows.Where(row => (_includeSubfolders
				? row.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
				: String.Equals(row.Directory, folder, StringComparison.OrdinalIgnoreCase))
				&& row.State switch
				{
					DatCompareState.Identical => _showIdentical,
					DatCompareState.Different => _showDifferent,
					DatCompareState.LeftOnly => _showLeftOnly,
					_ => _showRightOnly,
				}).ToList();
			NotifyCounts();
		}

		private void NotifyCounts()
		{
			OnPropertyChanged(nameof(IdenticalFilterText));
			OnPropertyChanged(nameof(DifferentFilterText));
			OnPropertyChanged(nameof(LeftOnlyFilterText));
			OnPropertyChanged(nameof(RightOnlyFilterText));
			OnPropertyChanged(nameof(SummaryText));
		}

		/// <summary>The two versions of the file that is selected, side by side; nothing when none or several are selected.</summary>
		private void ShowSelection()
		{
			if (_showDifferences)
			{
				LeftViewer.Clear(Loc.Get("Str.ViewerNoPreview"));
				RightViewer.Clear(Loc.Get("Str.ViewerNoPreview"));
				DiffTask = ShowDifferencesAsync();
				return;
			}

			_diffCancellation?.Cancel();
			if (_selected.Count != 1)
			{
				LeftViewer.Clear(Loc.Get("Str.ViewerNoPreview"));
				RightViewer.Clear(Loc.Get("Str.ViewerNoPreview"));
				return;
			}

			ComparisonRow row = _selected[0];
			Show(LeftViewer, Left, row.Left, "Str.CompareLeftAbsent");
			Show(RightViewer, Right, row.Right, "Str.CompareRightAbsent");
		}

		/// <summary>
		/// Writes the two versions of the selected file as lines (in the background: a file can be large) and compares them.
		/// A choice of another file meanwhile drops the answer.
		/// </summary>
		private async Task ShowDifferencesAsync()
		{
			_diffCancellation?.Cancel();
			CancellationTokenSource cancellation = new();
			_diffCancellation = cancellation;

			if (_selected.Count != 1)
			{
				Diff.ShowMessage("Str.CompareDiffSelect");
				return;
			}

			ComparisonRow row = _selected[0];
			if (row.Left == null || row.Right == null)
			{
				Diff.ShowMessage("Str.CompareDiffOnlyOne");
				return;
			}

			if (row.State == DatCompareState.Identical)
			{
				Diff.ShowMessage("Str.CompareDiffIdentical");
				return;
			}

			DatEntry left = row.Left;
			DatEntry right = row.Right;
			String name = row.Path;
			Func<IEnumerable<DatArchive>> others = _archives;

			// What is ignored only applies to the files made of text (the others are shown field by field).
			Boolean isText = TextContentComparer.IsText(name);
			Boolean usesOptions = TextContentComparer.AppliesTo(name);
			ComparisonOptions options = Options.Effective;

			(FileTextResult Result, IReadOnlyList<DiffRow> Rows, Boolean Exact, IReadOnlyList<DiffNote> Notes) outcome;
			try
			{
				outcome = await Task.Run(
					() =>
					{
						Byte[] leftData = Left.Archive.ReadData(left);
						Byte[] rightData = Right.Archive.ReadData(right);
						FileTextResult leftResult = FileTextView.TryGetLines(name, leftData, ResourcesFor(Left.Archive, others), GameOf(Left.Archive), out IReadOnlyList<String> leftLines);
						FileTextResult rightResult = FileTextView.TryGetLines(name, rightData, ResourcesFor(Right.Archive, others), GameOf(Right.Archive), out IReadOnlyList<String> rightLines);

						if (leftResult != FileTextResult.Lines || rightResult != FileTextResult.Lines)
						{
							return (leftResult != FileTextResult.Lines ? leftResult : rightResult, (IReadOnlyList<DiffRow>)[], true, (IReadOnlyList<DiffNote>)[]);
						}

						IReadOnlyList<DiffRow> rows = TextDiff.Compare(leftLines, rightLines, usesOptions ? options : null, out Boolean exact);
						return (FileTextResult.Lines, rows, exact, isText ? TextContentComparer.Notes(leftData, rightData, options) : []);
					},
					cancellation.Token);
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException)
			{
				if (!cancellation.IsCancellationRequested)
				{
					Diff.ShowMessage("Str.CompareDiffNotText");
				}

				return;
			}

			if (cancellation.IsCancellationRequested)
			{
				return;
			}

			switch (outcome.Result)
			{
				case FileTextResult.Lines:
					Diff.Show(outcome.Rows, outcome.Exact, outcome.Notes);
					break;

				case FileTextResult.TooLarge:
					Diff.ShowMessage("Str.CompareDiffTooLarge");
					break;

				default:
					Diff.ShowMessage("Str.CompareDiffNotText");
					break;
			}
		}

		/// <summary>The game an archive is for, which a compiled script is decompiled for.</summary>
		private static ScriptGame GameOf(DatArchive archive) => TextContentComparer.ScriptGameOf(archive.Format);

		/// <summary>The files of the game that a map needs: found in the archive of the map first, then in the other open ones.</summary>
		private MapResources ResourcesFor(DatArchive archive, Func<IEnumerable<DatArchive>> others)
		{
			return new MapResources(path =>
			{
				foreach (DatArchive candidate in new[] { archive }.Concat(others()).Distinct())
				{
					try
					{
						DatEntry? entry = candidate.Find(path);
						if (entry != null && !entry.IsDamaged)
						{
							return candidate.ReadData(entry);
						}
					}
					catch (Exception exception) when (exception is DatException or IOException or InvalidOperationException)
					{
						// Another archive may hold a readable copy.
					}
				}

				return null;
			});
		}

		private static void Show(ViewerViewModel viewer, DatDocument document, DatEntry? entry, String absentKey)
		{
			if (entry == null)
			{
				viewer.Clear(Loc.Get(absentKey));
			}
			else
			{
				_ = viewer.LoadAsync(document.Archive, entry);
			}
		}
	}
}