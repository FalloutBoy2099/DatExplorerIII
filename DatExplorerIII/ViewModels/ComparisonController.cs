namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Core;
	using Infrastructure;
	using Models.Archives;
	using Models.Comparison;
	using Models.Interface;
	using Services.Interface;
	using Services.Platform;
	using Services.Texts;

	/// <summary>
	/// The comparison mode: two archives (files or folders) side by side in the main window, the files that are identical,
	/// different or only on one side, and the copy or move of files from one to the other. The main view model owns one and
	/// gives it, through <see cref="IComparisonHost"/>, what it needs of the archives that are open.
	/// </summary>
	public sealed class ComparisonController : ObservableObject
	{
		private const Int32 MaxListedItems = 12;

		private readonly IComparisonHost _host;
		private readonly IDialogService _dialogs;
		private readonly SettingsService _settings;
		private readonly TempFolder _temp;
		private readonly IUiThread _uiThread;

		private ComparisonViewModel? _comparison;

		internal ComparisonController(IComparisonHost host, IDialogService dialogs, SettingsService settings, TempFolder temp, IUiThread uiThread)
		{
			_host = host;
			_dialogs = dialogs;
			_settings = settings;
			_temp = temp;
			_uiThread = uiThread;
			CreateCommands();
		}

		public ICommand CompareCommand { get; private set; } = null!;

		public ICommand CloseComparisonCommand { get; private set; } = null!;

		public ICommand CompareAgainCommand { get; private set; } = null!;

		public ICommand CopyToRightCommand { get; private set; } = null!;

		public ICommand CopyToLeftCommand { get; private set; } = null!;

		public ICommand MoveToRightCommand { get; private set; } = null!;

		public ICommand MoveToLeftCommand { get; private set; } = null!;

		public ICommand SaveLeftCommand { get; private set; } = null!;

		public ICommand SaveRightCommand { get; private set; } = null!;

		/// <summary>The comparison that is shown; null when the window shows the archives.</summary>
		public ComparisonViewModel? Current
		{
			get => _comparison;
			private set
			{
				if (SetProperty(ref _comparison, value))
				{
					OnPropertyChanged(nameof(IsComparing));
					CommandRequery.Invalidate();
				}
			}
		}

		public Boolean IsComparing => _comparison != null;

		/// <summary>Comparison: the block of the folders and files and the block of the two archives are one above the other.</summary>
		public Boolean IsCompareGroupsStacked
		{
			get => _settings.Data.CompareGroupsStacked;
			set
			{
				if (_settings.Data.CompareGroupsStacked != value)
				{
					_settings.Data.CompareGroupsStacked = value;
					_settings.Save();
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCompareGroupsSide));
				}
			}
		}

		/// <summary>Comparison: the block of the folders and files and the block of the two archives are side by side.</summary>
		public Boolean IsCompareGroupsSide
		{
			get => !_settings.Data.CompareGroupsStacked;
			set => IsCompareGroupsStacked = !value;
		}

		/// <summary>Comparison: the tree of the folders and the list of the files are one above the other.</summary>
		public Boolean IsCompareListStacked
		{
			get => _settings.Data.CompareListStacked;
			set
			{
				if (_settings.Data.CompareListStacked != value)
				{
					_settings.Data.CompareListStacked = value;
					_settings.Save();
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCompareListSide));
				}
			}
		}

		/// <summary>Comparison: the tree of the folders and the list of the files are side by side.</summary>
		public Boolean IsCompareListSide
		{
			get => !_settings.Data.CompareListStacked;
			set => IsCompareListStacked = !value;
		}

		/// <summary>Comparison: the left archive and the right archive are one above the other.</summary>
		public Boolean IsCompareViewersStacked
		{
			get => _settings.Data.CompareViewersStacked;
			set
			{
				if (_settings.Data.CompareViewersStacked != value)
				{
					_settings.Data.CompareViewersStacked = value;
					_settings.Save();
					OnPropertyChanged();
					OnPropertyChanged(nameof(IsCompareViewersSide));
				}
			}
		}

		/// <summary>Comparison: the left archive and the right archive are side by side.</summary>
		public Boolean IsCompareViewersSide
		{
			get => !_settings.Data.CompareViewersStacked;
			set => IsCompareViewersStacked = !value;
		}

		/// <summary>Comparison: whether the tree of the folders is shown.</summary>
		public Boolean IsCompareTreeVisible
		{
			get => _settings.Data.CompareTreeVisible;
			set
			{
				if (_settings.Data.CompareTreeVisible != value)
				{
					_settings.Data.CompareTreeVisible = value;
					_settings.Save();
					OnPropertyChanged();
				}
			}
		}

		private void CreateCommands()
		{
			CompareCommand = new AsyncRelayCommand(CompareAsync, _host.CanBrowse);
			CloseComparisonCommand = new RelayCommand(CloseComparison, () => IsComparing && !_host.Busy.IsBusy);
			CompareAgainCommand = new AsyncRelayCommand(CompareAgainAsync, () => IsComparing && !_host.Busy.IsBusy);
			CopyToRightCommand = new AsyncRelayCommand(() => TransferAsync(true, false), () => CanTransfer(true));
			CopyToLeftCommand = new AsyncRelayCommand(() => TransferAsync(false, false), () => CanTransfer(false));
			MoveToRightCommand = new AsyncRelayCommand(() => TransferAsync(true, true), () => CanTransfer(true));
			MoveToLeftCommand = new AsyncRelayCommand(() => TransferAsync(false, true), () => CanTransfer(false));
			SaveLeftCommand = new AsyncRelayCommand(() => SaveComparedAsync(true), () => IsComparing && !_host.Busy.IsBusy);
			SaveRightCommand = new AsyncRelayCommand(() => SaveComparedAsync(false), () => IsComparing && !_host.Busy.IsBusy);
		}

		private Boolean CanTransfer(Boolean toRight)
		{
			return _comparison is { } comparison && !_host.Busy.IsBusy && (toRight ? comparison.CanSendRight : comparison.CanSendLeft);
		}

		/// <summary>Asks for the two archives, opens them (when they are not open yet) and compares them.</summary>
		internal async Task CompareAsync()
		{
			// Two archives that are open are the natural choice; otherwise the two that were compared last.
			String? defaultLeft = _host.Roots.Count > 1 ? _host.Roots[0].Document.FilePath : _settings.Data.CompareLastLeft;
			String? defaultRight = _host.Roots.Count > 1 ? _host.Roots[1].Document.FilePath : _settings.Data.CompareLastRight;

			ComparisonSources? sources = _dialogs.PickComparisonSources(defaultLeft, defaultRight, ComparisonSuggestions());
			if (sources == null)
				return;

			_settings.Data.CompareLastLeft = sources.Left;
			_settings.Data.CompareLastRight = sources.Right;
			_settings.Save();

			DatDocument? left = await OpenForComparisonAsync(sources.Left);
			DatDocument? right = left == null ? null : await OpenForComparisonAsync(sources.Right);
			if (left == null || right == null)
				return;

			await RunComparisonAsync(left, right);
		}

		/// <summary>The archives to propose for a side of the comparison: the ones that are open, then the last ones used, then the recent files.</summary>
		internal IReadOnlyList<String> ComparisonSuggestions()
		{
			return _host.Roots.Select(root => root.Document.FilePath)
				.Concat(new[] { _settings.Data.CompareLastLeft, _settings.Data.CompareLastRight }.OfType<String>())
				.Concat(_settings.Data.RecentFiles)
				.Where(path => File.Exists(path) || Directory.Exists(path))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Take(20)
				.ToList();
		}

		internal async Task CompareAgainAsync()
		{
			if (_comparison is { } comparison)
				await RunComparisonAsync(comparison.Left, comparison.Right);
		}

		/// <summary>The document of an archive that is open; the archive is opened first when it is not.</summary>
		private async Task<DatDocument?> OpenForComparisonAsync(String path)
		{
			String full = Path.GetFullPath(path);
			FolderNode? FindRoot() => _host.Roots.FirstOrDefault(root => String.Equals(root.Document.FilePath, full, StringComparison.OrdinalIgnoreCase));

			if (FindRoot() == null)
				await _host.OpenArchiveAsync(full);

			return FindRoot()?.Document;
		}

		private async Task RunComparisonAsync(DatDocument left, DatDocument right)
		{
			IReadOnlyList<DatCompareItem>? items = null;
			ComparisonOptions options = _settings.Data.ComparisonOptions;
			TextEquality? text = TextContentComparer.CreateEquality(options);
			Boolean done = await _host.RunBusyAsync(Loc.Get("Str.Comparing"), true, async (progress, token) =>
			{
				items = await Task.Run(() => DatComparer.Compare(left.Archive, right.Archive, progress, token, text), token);
			});

			if (!done || items == null)
				return;

			_comparison?.Dispose();
			ComparisonViewModel comparison = new(left, right, items, _host.OpenArchives, _uiThread, options);
			comparison.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(ComparisonViewModel.ShowDifferences))
				{
					_settings.Data.CompareShowDifferences = comparison.ShowDifferences;
					_settings.Save();
				}
			};

			comparison.ShowDifferences = _settings.Data.CompareShowDifferences;
			Current = comparison;
			ApplySubtitles();
			ApplyMessageChecks();
			_host.StatusText = _comparison!.SummaryText;
		}

		private void CloseComparison()
		{
			ComparisonViewModel? comparison = _comparison;
			Current = null;
			comparison?.Dispose();
			_host.RefreshItems();
		}

		/// <summary>
		/// Copies (or moves) the selected files from one archive to the other. A file that the other archive already has is
		/// replaced. The files are read first, in the background; the archives are changed afterwards.
		/// </summary>
		internal async Task TransferAsync(Boolean toRight, Boolean move)
		{
			ComparisonViewModel? comparison = _comparison;
			if (comparison == null)
				return;

			DatDocument source = toRight ? comparison.Left : comparison.Right;
			DatDocument target = toRight ? comparison.Right : comparison.Left;
			DatEntry? SourceOf(ComparisonRow row) => toRight ? row.Left : row.Right;
			DatEntry? TargetOf(ComparisonRow row) => toRight ? row.Right : row.Left;

			List<ComparisonRow> rows = comparison.Selected.Where(row => SourceOf(row) != null).ToList();
			if (rows.Count == 0)
			{
				_dialogs.ShowMessage(_host.Title, Loc.Get("Str.CompareNothingToDo"), DialogIcon.Information);
				return;
			}

			Int32 replaced = rows.Count(row => TargetOf(row) != null && row.State != DatCompareState.Identical);
			if (move || replaced > 0)
			{
				String question = move ? Loc.Format("Str.CompareConfirmMove", rows.Count) : String.Empty;
				if (replaced > 0)
					question += (question.Length > 0 ? Environment.NewLine + Environment.NewLine : String.Empty) + Loc.Format("Str.CompareConfirmReplace", replaced);

				if (!_dialogs.Confirm(_host.Title, question + Environment.NewLine + Environment.NewLine + Loc.Get("Str.CompareContinue"), DialogIcon.Warning))
					return;
			}

			// A file that is the same on both sides is not copied again; a move of it only takes it away from the source.
			String folder = _temp.Create("compare");
			Dictionary<ComparisonRow, String?> prepared = [];
			List<String> failures = [];

			Boolean read = await _host.RunBusyAsync(Loc.Get(move ? "Str.CompareMoving" : "Str.CompareCopying"), true, (progress, token) => Task.Run(() =>
			{
				DatProgressThrottle throttle = new(progress, rows.Count);
				for (Int32 index = 0; index < rows.Count; index++)
				{
					token.ThrowIfCancellationRequested();
					ComparisonRow row = rows[index];
					throttle.Report(row.Path, index + 1);

					if (row.State == DatCompareState.Identical)
					{
						prepared[row] = null;
						continue;
					}

					try
					{
						String path = Path.Combine(folder, index.ToString("D6", CultureInfo.InvariantCulture) + ".tmp");
						File.WriteAllBytes(path, source.Archive.ReadData(SourceOf(row)!));
						prepared[row] = path;
					}
					catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
					{
						failures.Add($"{row.Path}: {ErrorText.Describe(exception)}");
					}
				}
			}, token));

			if (!read)
				return;

			Int32 done = 0;
			foreach ((ComparisonRow row, String? temporary) in prepared)
			{
				try
				{
					DatEntry sourceEntry = SourceOf(row)!;
					if (temporary != null)
					{
						if (TargetOf(row) is { } existing)
							target.Archive.Remove([existing]);

						target.Archive.AddFile(temporary, sourceEntry.Directory, sourceEntry.Name);
					}

					if (move)
						source.Archive.Remove([sourceEntry]);

					done++;
				}
				catch (DatException exception)
				{
					failures.Add($"{row.Path}: {ErrorText.Describe(exception)}");
					continue;
				}

				// What each archive holds now: a file that both have was just copied, or was the same already.
				DatEntry? left = comparison.Left.Archive.Find(row.Path);
				DatEntry? right = comparison.Right.Archive.Find(row.Path);
				row.Update(left, right, left != null && right != null ? DatCompareState.Identical : left != null ? DatCompareState.LeftOnly : DatCompareState.RightOnly);
			}

			comparison.Left.Refresh();
			comparison.Right.Refresh();
			_host.RefreshItems();
			comparison.Refresh();
			_host.StatusText = Loc.Format("Str.CompareTransferDone", done);

			if (failures.Count > 0)
			{
				List<String> lines = failures.Take(MaxListedItems).ToList();
				if (failures.Count > MaxListedItems)
					lines.Add(Loc.Format("Str.AndMore", failures.Count - MaxListedItems));

				_dialogs.ShowMessage(_host.Title, Loc.Get("Str.CompareTransferFailures") + Environment.NewLine + Environment.NewLine + String.Join(Environment.NewLine, lines), DialogIcon.Warning);
			}
		}

		/// <summary>Saves one of the two archives (the one that was changed by the copies).</summary>
		private async Task SaveComparedAsync(Boolean left)
		{
			ComparisonViewModel? comparison = _comparison;
			if (comparison == null)
				return;

			DatDocument document = left ? comparison.Left : comparison.Right;
			await _host.SaveDocumentAsync(document, false);
			comparison.RefreshTitles();
		}

		/// <summary>Tells the viewers of the comparison that is shown which kinds of problems of the message files are not looked for.</summary>
		public void ApplyMessageChecks()
		{
			if (_comparison != null)
			{
				_comparison.LeftViewer.DisabledMessageChecks = _settings.Data.DisabledMessageChecks;
				_comparison.RightViewer.DisabledMessageChecks = _settings.Data.DisabledMessageChecks;
			}
		}

		/// <summary>Gives the option of the subtitles to the viewers of the comparison that is shown.</summary>
		public void ApplySubtitles()
		{
			if (_comparison != null)
			{
				_comparison.LeftViewer.ShowSubtitles = _settings.Data.ShowSubtitles;
				_comparison.RightViewer.ShowSubtitles = _settings.Data.ShowSubtitles;
			}
		}
	}
}