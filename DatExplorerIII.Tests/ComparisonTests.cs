namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The comparison of two archives (DAT files or folders) and the copy or move of files between them.</summary>
	[Collection("ScrollBarSizes")] // Opening the settings applies the sizes of the scroll bars, which the other tests of that collection measure.
	public sealed class ComparisonTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
		private readonly List<DatArchive> _archives = new();
		private readonly List<MainViewModel> _viewModels = new();

		public ComparisonTests()
		{
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			foreach (DatArchive archive in _archives.Concat(_viewModels.SelectMany(viewModel => viewModel.Roots).Select(root => root.Document.Archive)))
			{
				archive.Dispose();
			}

			Directory.Delete(_root, true);
		}

		private String Folder(String name, params (String Path, String Content)[] files)
		{
			String folder = Path.Combine(_root, name);
			Directory.CreateDirectory(folder);
			foreach ((String path, String content) in files)
			{
				String full = Path.Combine(folder, path);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllText(full, content);
			}

			return folder;
		}

		private DatArchive OpenFolder(String folder)
		{
			DatArchive archive = DatArchive.OpenFolder(folder);
			_archives.Add(archive);
			return archive;
		}

		/// <summary>Two folders: one file the same, one different (same size), one different (other size), one on each side only.</summary>
		private (String Left, String Right) TwoFolders()
		{
			String left = Folder("left", ("same.txt", "the same"), ("data\\changed.txt", "left 1"), ("longer.txt", "short"), ("only-left.txt", "left only"));
			String right = Folder("right", ("SAME.TXT", "the same"), ("data\\changed.txt", "right2"), ("longer.txt", "a longer text"), ("only-right.txt", "right only"));
			return (left, right);
		}

		private static DatCompareState StateOf(IReadOnlyList<DatCompareItem> items, String path) => items.Single(item => String.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)).State;

		#region Core

		[Fact]
		public void Compare_FindsWhatIsTheSameWhatDiffersAndWhatOnlyOneSideHas()
		{
			(String left, String right) = TwoFolders();
			IReadOnlyList<DatCompareItem> items = DatComparer.Compare(OpenFolder(left), OpenFolder(right));

			Assert.Equal(5, items.Count);
			Assert.Equal(DatCompareState.Identical, StateOf(items, "same.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(items, "data\\changed.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(items, "longer.txt"));
			Assert.Equal(DatCompareState.LeftOnly, StateOf(items, "only-left.txt"));
			Assert.Equal(DatCompareState.RightOnly, StateOf(items, "only-right.txt"));

			// The paths are compared without regard to case, and ordered.
			Assert.Equal(items.Select(item => item.Path.ToLowerInvariant()).OrderBy(path => path, StringComparer.Ordinal), items.Select(item => item.Path.ToLowerInvariant()));
			DatCompareItem same = items.Single(item => item.State == DatCompareState.Identical);
			Assert.NotNull(same.Left);
			Assert.NotNull(same.Right);

			DatCompareItem onlyLeft = items.Single(item => item.State == DatCompareState.LeftOnly);
			Assert.NotNull(onlyLeft.Left);
			Assert.Null(onlyLeft.Right);
		}

		[Fact]
		public void Compare_AFileAndAFolderAreComparedLikeTwoArchives()
		{
			(String left, String right) = TwoFolders();
			String datPath = Path.Combine(_root, "left.dat");
			using (DatArchive built = DatArchive.OpenFolder(left))
			{
				built.SaveAs(datPath);
			}

			DatArchive file = DatArchive.Open(datPath);
			_archives.Add(file);

			IReadOnlyList<DatCompareItem> items = DatComparer.Compare(file, OpenFolder(right));

			Assert.Equal(DatCompareState.Identical, StateOf(items, "same.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(items, "data\\changed.txt"));
			Assert.Equal(DatCompareState.LeftOnly, StateOf(items, "only-left.txt"));
		}

		[Fact]
		public void Compare_AnArchiveWithItself_IsAllIdentical_AndAFileThatCannotBeReadIsDifferent()
		{
			String folder = Folder("one", ("a.txt", "aaa"), ("b.txt", "bbb"));
			DatArchive first = OpenFolder(folder);
			DatArchive second = OpenFolder(folder);

			Assert.All(DatComparer.Compare(first, second), item => Assert.Equal(DatCompareState.Identical, item.State));

			// The file disappears from the disk after the archive was opened: it cannot be read, it is not the same.
			File.Delete(Path.Combine(folder, "b.txt"));
			IReadOnlyList<DatCompareItem> items = DatComparer.Compare(first, second);
			Assert.Equal(DatCompareState.Identical, StateOf(items, "a.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(items, "b.txt"));
		}

		[Fact]
		public void Compare_CanBeCancelled_AndReportsItsProgress()
		{
			String left = Folder("many-left");
			String right = Folder("many-right");
			for (Int32 index = 0; index < 60; index++)
			{
				File.WriteAllText(Path.Combine(left, $"f{index}.txt"), "content " + index);
				File.WriteAllText(Path.Combine(right, $"f{index}.txt"), "content " + index);
			}

			DatArchive leftArchive = OpenFolder(left);
			DatArchive rightArchive = OpenFolder(right);

			using CancellationTokenSource cancelled = new();
			cancelled.Cancel();
			Assert.ThrowsAny<OperationCanceledException>(() => DatComparer.Compare(leftArchive, rightArchive, null, cancelled.Token));

			List<DatProgress> reports = new();
			DatComparer.Compare(leftArchive, rightArchive, new SynchronousProgress(reports.Add));
			Assert.NotEmpty(reports);
			Assert.Equal(60, reports[^1].Total);
			Assert.Equal(60, reports[^1].Current);
		}

		private sealed class SynchronousProgress : IProgress<DatProgress>
		{
			private readonly Action<DatProgress> _report;

			public SynchronousProgress(Action<DatProgress> report)
			{
				_report = report;
			}

			public void Report(DatProgress value)
			{
				lock (_report)
				{
					_report(value);
				}
			}
		}

		#endregion

		#region Settings

		[Fact]
		public async Task Settings_Accepted_TakeTheValues_AndCompareAgainWhenTheOptionsChanged()
		{
			String left = Folder("opt-left", ("a.txt", "Hello World"));
			String right = Folder("opt-right", ("a.txt", "hello world"));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel before = viewModel.Comparison!;
			Assert.Equal(1, before.Count(DatCompareState.Different));

			dialogs.SettingsScript = model =>
			{
				model.IgnoreCase = true;
				model.SingleInstance = false;
				model.SaveWindowSize = false;
				model.CompareSyncScroll = false;
				model.ScrollBarWidth = 22.4;
				model.ScrollBarMinimumThumb = 500;
				return true;
			};
			await viewModel.OpenSettingsAsync();

			Assert.NotSame(before, viewModel.Comparison);
			Assert.Equal(1, viewModel.Comparison!.Count(DatCompareState.Identical));
			Assert.True(viewModel.ComparisonOptions.IgnoreCase);
			Assert.False(viewModel.IsSingleInstance);
			Assert.False(viewModel.SaveWindowSize);
			Assert.True(viewModel.SaveWindowPosition);
			Assert.False(viewModel.IsCompareSyncScroll);

			// Kept for the next launch.
			SettingsData saved = new SettingsService(Path.Combine(_root, "settings"), null).Data;
			Assert.True(saved.ComparisonOptions.IgnoreCase);
			Assert.False(saved.SingleInstance);
			Assert.False(saved.SaveWindowSize);

			// The sizes of the scroll bars are rounded and kept between their limits.
			Assert.Equal(22, saved.ScrollBarWidth);
			Assert.Equal(160, saved.ScrollBarMinimumThumb);
		}

		[Fact]
		public async Task Settings_Cancelled_ChangeNothing()
		{
			String left = Folder("cancel-left", ("a.txt", "Hello World"));
			String right = Folder("cancel-right", ("a.txt", "hello world"));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel before = viewModel.Comparison!;

			dialogs.SettingsScript = model =>
			{
				model.IgnoreCase = true;
				model.SingleInstance = false;
				return false;
			};
			await viewModel.OpenSettingsAsync();

			Assert.Same(before, viewModel.Comparison);
			Assert.False(viewModel.ComparisonOptions.IgnoreCase);
			Assert.True(viewModel.IsSingleInstance);
		}

		[Fact]
		public async Task Settings_TheOptionsOtherThanTheComparisonOnes_DoNotCompareAgain()
		{
			String left = Folder("same-left", ("a.txt", "one"));
			String right = Folder("same-right", ("a.txt", "two"));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel before = viewModel.Comparison!;

			dialogs.SettingsScript = model =>
			{
				model.WindowFrameVisible = true;
				model.BlockSeparatorsVisible = false;
				return true;
			};
			await viewModel.OpenSettingsAsync();

			Assert.Same(before, viewModel.Comparison);
			Assert.True(viewModel.IsWindowFrameVisible);
			Assert.False(viewModel.IsBlockSeparatorsVisible);
		}

		[Fact]
		public async Task TheDifferences_TellWhatElseDiffers_WhenTheLinesAreTheSame()
		{
			String left = Folder("eol-left", ("a.txt", "one" + Convert.ToChar(13) + Convert.ToChar(10) + "two" + Convert.ToChar(13) + Convert.ToChar(10)));
			String right = Folder("eol-right", ("a.txt", "one" + Convert.ToChar(10) + "two" + Convert.ToChar(10)));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel comparison = viewModel.Comparison!;
			Assert.Equal(1, comparison.Count(DatCompareState.Different));

			Select(viewModel, "a.txt");
			comparison.ShowDifferences = true;
			await comparison.DiffTask;

			Assert.Equal(0, comparison.Diff.DifferenceCount);
			Assert.Contains("Str.CompareDiffEndings", comparison.Diff.Summary, StringComparison.Ordinal);

			// With the line endings ignored the two files are the same.
			dialogs.SettingsScript = model =>
			{
				model.IgnoreLineEndings = true;
				return true;
			};
			await viewModel.OpenSettingsAsync();
			Assert.Equal(1, viewModel.Comparison!.Count(DatCompareState.Identical));
		}

		#endregion

		#region View model

		private sealed class Dialogs : IDialogService
		{
			public ComparisonSources? Sources { get; set; }

			public Boolean Answer { get; set; } = true;

			public List<String> Questions { get; } = new();

			public List<String> Messages { get; } = new();

			public String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons) => null;

			public Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question)
			{
				Questions.Add(message);
				return Answer;
			}

			public void ShowMessage(String title, String message, DialogIcon icon) => Messages.Add(message);

			public void ShowError(String message) => Messages.Add(message);

			public String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate) => null;

			public IReadOnlyList<String> PickDatFiles(String? initialFolder) => [];

			public String? PickNewDatFile(DatFormat format, String? initialFolder) => null;

			public SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat) => null;

			public String? OfferedLeft { get; private set; }

			public String? OfferedRight { get; private set; }

			public IReadOnlyList<String> Suggestions { get; private set; } = [];

			public ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions)
			{
				OfferedLeft = left;
				OfferedRight = right;
				Suggestions = suggestions;
				return Sources;
			}

			public IReadOnlyList<String> PickFiles(String? initialFolder) => [];

			public String? PickFolder(String title, String? initialFolder) => null;

			public void ShowSearchResults(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
			{
			}

			public void ShowViewer(ViewerViewModel viewer)
			{
			}

			public Func<SettingsDialogViewModel, Boolean>? SettingsScript { get; set; }

			public Boolean ShowSettings(SettingsDialogViewModel settings) => SettingsScript?.Invoke(settings) ?? false;

			public void ShowAbout()
			{
			}
		}

		private MainViewModel CreateViewModel(Dialogs dialogs)
		{
			MainViewModel viewModel = new(dialogs, new SettingsService(Path.Combine(_root, "settings"), null), new TempFolder(), new ManualUiThread());
			_viewModels.Add(viewModel);
			return viewModel;
		}

		private static void Select(MainViewModel viewModel, params String[] paths)
		{
			viewModel.Comparison!.SetSelection(viewModel.Comparison.AllRows.Where(row => paths.Contains(row.Path, StringComparer.OrdinalIgnoreCase)).ToList());
		}

		[Fact]
		public async Task ViewModel_ComparesTwoFolders_AndShowsTheStatesWithTheirFilters()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);

			await viewModel.CompareAsync();

			// Both archives are open in the window, and the window is in the comparison mode.
			Assert.Equal(2, viewModel.Roots.Count);
			Assert.True(viewModel.IsComparing);
			ComparisonViewModel comparison = viewModel.Comparison!;
			Assert.Equal(1, comparison.Count(DatCompareState.Identical));
			Assert.Equal(2, comparison.Count(DatCompareState.Different));
			Assert.Equal(1, comparison.Count(DatCompareState.LeftOnly));
			Assert.Equal(1, comparison.Count(DatCompareState.RightOnly));
			Assert.False(viewModel.OpenCommand.CanExecute(null));
			Assert.True(viewModel.CloseComparisonCommand.CanExecute(null));

			// Without the identical files the list is the one of what differs.
			Assert.Equal(4, comparison.Rows.Count);
			Assert.DoesNotContain(comparison.Rows, row => row.State == DatCompareState.Identical);
			comparison.ShowIdentical = true;
			Assert.Equal(5, comparison.Rows.Count);
			comparison.ShowDifferent = false;
			Assert.Equal(3, comparison.Rows.Count);
			comparison.ShowLeftOnly = false;
			comparison.ShowRightOnly = false;
			Assert.Single(comparison.Rows);

			viewModel.CloseComparisonCommand.Execute(null);
			Assert.False(viewModel.IsComparing);
			Assert.Null(viewModel.Comparison);
			Assert.Equal(2, viewModel.Roots.Count);
		}

		[Fact]
		public async Task ViewModel_TheListFollowsTheFolderThatIsChosen_FromTheBoxOrFromTheTree()
		{
			String left = Folder("tree-left", ("root.txt", "1"), ("art\\cuts\\a.txt", "left"), ("art\\heads\\b.txt", "same"), ("text\\c.txt", "same"));
			String right = Folder("tree-right", ("root.txt", "2"), ("art\\cuts\\a.txt", "right"), ("art\\heads\\b.txt", "same"), ("text\\c.txt", "same"));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel comparison = viewModel.Comparison!;
			comparison.ShowIdentical = true;

			// The folders of both archives, each followed by its sub folders; the whole is the first.
			Assert.Equal(["", "art\\", "art\\cuts\\", "art\\heads\\", "text\\"], comparison.FlatFolders.Select(folder => folder.Path));
			Assert.Same(comparison.Root, comparison.SelectedFolder);
			Assert.Equal(4, comparison.Rows.Count);

			// The number of files that are not the same, in the folder and in its sub folders.
			Assert.Equal(2, comparison.Root.Differences);
			Assert.Equal(1, comparison.FlatFolders.Single(folder => folder.Path == "art\\").Differences);
			Assert.Equal(0, comparison.FlatFolders.Single(folder => folder.Path == "text\\").Differences);

			// A folder chosen in the box: its files and those of its sub folders.
			comparison.SelectedFolder = comparison.FlatFolders.Single(folder => folder.Path == "art\\");
			Assert.Equal(["art\\cuts\\a.txt", "art\\heads\\b.txt"], comparison.Rows.Select(row => row.Path));
			Assert.True(comparison.Root.IsExpanded);
			Assert.True(comparison.SelectedFolder.IsSelected);
			Assert.False(comparison.Root.IsSelected);

			// Without the sub folders, the files of the folder only.
			comparison.IncludeSubfolders = false;
			Assert.Empty(comparison.Rows);
			comparison.SelectedFolder = comparison.FlatFolders.Single(folder => folder.Path == "art\\cuts\\");
			Assert.Equal(["art\\cuts\\a.txt"], comparison.Rows.Select(row => row.Path));

			// A folder chosen in the tree (the tree sets IsSelected of the node) is the same choice.
			comparison.IncludeSubfolders = true;
			comparison.FlatFolders.Single(folder => folder.Path == "text\\").IsSelected = true;
			Assert.Equal("text\\", comparison.SelectedFolder.Path);
			Assert.Equal(["text\\c.txt"], comparison.Rows.Select(row => row.Path));
			Assert.False(comparison.FlatFolders.Single(folder => folder.Path == "art\\cuts\\").IsSelected);

			// The box does not lose the choice when it empties its selection while its list is replaced.
			comparison.SelectedFolder = null!;
			Assert.Equal("text\\", comparison.SelectedFolder.Path);
		}

		[Fact]
		public async Task ViewModel_TheNumbersOfDifferencesFollowTheCopies()
		{
			String left = Folder("count-left", ("art\\a.txt", "left"));
			String right = Folder("count-right", ("art\\a.txt", "right"));
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel comparison = viewModel.Comparison!;
			Assert.Equal(1, comparison.Root.Differences);

			Select(viewModel, "art\\a.txt");
			await viewModel.TransferAsync(true, false);

			Assert.Equal(0, comparison.Root.Differences);
			Assert.Equal(0, comparison.FlatFolders.Single(folder => folder.Path == "art\\").Differences);
		}

		[Fact]
		public void Layout_TheChoicesAreKeptInTheSettings_AndSideAndStackedAreTheTwoFacesOfOneChoice()
		{
			SettingsService settings = new(Path.Combine(_root, "layout-settings"), null);
			MainViewModel viewModel = new(new Dialogs(), settings, new TempFolder(), new ManualUiThread());
			_viewModels.Add(viewModel);

			// By default: the folders above the files, the two archives side by side, the two groups side by side, the tree shown.
			Assert.True(viewModel.IsCompareListStacked);
			Assert.False(viewModel.IsCompareViewersStacked);
			Assert.False(viewModel.IsCompareGroupsStacked);
			Assert.True(viewModel.IsCompareGroupsSide);
			Assert.True(viewModel.IsCompareTreeVisible);

			List<String> changed = new();
			viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName!);

			viewModel.IsCompareListStacked = false;
			Assert.True(viewModel.IsCompareListSide);
			Assert.Contains("IsCompareListSide", changed);

			viewModel.IsCompareViewersSide = false;
			Assert.True(viewModel.IsCompareViewersStacked);

			viewModel.IsCompareGroupsSide = false;
			Assert.True(viewModel.IsCompareGroupsStacked);

			viewModel.IsCompareTreeVisible = false;

			SettingsData back = new SettingsService(Path.Combine(_root, "layout-settings"), null).Data;
			Assert.False(back.CompareListStacked);
			Assert.True(back.CompareViewersStacked);
			Assert.True(back.CompareGroupsStacked);
			Assert.False(back.CompareTreeVisible);
		}

		[Fact]
		public async Task ViewModel_CopyingToTheRight_ReplacesWhatDiffers_AndAddsWhatIsMissing()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel comparison = viewModel.Comparison!;

			Select(viewModel, "data\\changed.txt", "only-left.txt", "same.txt");
			Assert.True(comparison.CanSendRight);
			await viewModel.TransferAsync(true, false);

			// The question mentions the file that is replaced.
			Assert.Single(dialogs.Questions);

			DatArchive rightArchive = comparison.Right.Archive;
			Assert.Equal("left 1", Encoding.ASCII.GetString(rightArchive.ReadData(rightArchive.Find("data\\changed.txt")!)));
			Assert.Equal("left only", Encoding.ASCII.GetString(rightArchive.ReadData(rightArchive.Find("only-left.txt")!)));
			Assert.True(comparison.Right.ShouldSave);
			Assert.False(comparison.Left.ShouldSave);

			// The three files are the same now; the others did not move.
			Assert.Equal(DatCompareState.Identical, comparison.AllRows.Single(row => row.Path == "only-left.txt").State);
			Assert.Equal(DatCompareState.Identical, comparison.AllRows.Single(row => row.Path == "data\\changed.txt").State);
			Assert.Equal(DatCompareState.Different, comparison.AllRows.Single(row => row.Path == "longer.txt").State);
			Assert.Equal(DatCompareState.RightOnly, comparison.AllRows.Single(row => row.Path == "only-right.txt").State);
			Assert.Equal(3, comparison.Count(DatCompareState.Identical));

			// The files of the disk were not touched: the folder is only changed by saving.
			Assert.Equal("right2", File.ReadAllText(Path.Combine(right, "data", "changed.txt")));
			Assert.False(File.Exists(Path.Combine(right, "only-left.txt")));
		}

		[Fact]
		public async Task ViewModel_MovingToTheLeft_TakesTheFileAwayFromTheRight()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();
			ComparisonViewModel comparison = viewModel.Comparison!;

			Select(viewModel, "only-right.txt");
			Assert.True(comparison.CanSendLeft);
			Assert.False(comparison.CanSendRight);
			await viewModel.TransferAsync(false, true);

			Assert.NotNull(comparison.Left.Archive.Find("only-right.txt"));
			Assert.Null(comparison.Right.Archive.Find("only-right.txt"));
			ComparisonRow row = comparison.AllRows.Single(candidate => candidate.Path == "only-right.txt");
			Assert.Equal(DatCompareState.LeftOnly, row.State);
			Assert.NotNull(row.Left);
			Assert.Null(row.Right);

			// A move asks first.
			Assert.Single(dialogs.Questions);
		}

		[Fact]
		public async Task ViewModel_AnswerNoToTheQuestion_ChangesNothing()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right), Answer = false };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();

			Select(viewModel, "data\\changed.txt");
			await viewModel.TransferAsync(true, false);

			Assert.False(viewModel.Comparison!.Right.ShouldSave);
			Assert.Equal(DatCompareState.Different, viewModel.Comparison.AllRows.Single(row => row.Path == "data\\changed.txt").State);
		}

		[Fact]
		public async Task ViewModel_NothingToTransfer_SaysSo()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.CompareAsync();

			// A file that only the right archive has cannot be copied to the right.
			Select(viewModel, "only-right.txt");
			await viewModel.TransferAsync(true, false);

			Assert.Single(dialogs.Messages);
			Assert.Empty(dialogs.Questions);
		}

		[Fact]
		public async Task ViewModel_ProposesTheOpenArchivesThenTheLastOnesUsed_AndRemembersTheChoice()
		{
			(String left, String right) = TwoFolders();
			String missing = Path.Combine(_root, "does-not-exist");
			Dialogs dialogs = new() { Sources = new ComparisonSources(left, right) };
			MainViewModel viewModel = CreateViewModel(dialogs);

			// Nothing is open and nothing was compared yet: nothing is proposed.
			await viewModel.CompareAsync();
			Assert.Null(dialogs.OfferedLeft);
			Assert.Null(dialogs.OfferedRight);
			Assert.Empty(dialogs.Suggestions);

			// The two archives are open now (and are the natural choice); the choice was kept.
			Assert.Equal(2, viewModel.Roots.Count);
			Assert.Equal(left, new SettingsService(Path.Combine(_root, "settings"), null).Data.CompareLastLeft);
			Assert.Equal(right, new SettingsService(Path.Combine(_root, "settings"), null).Data.CompareLastRight);

			viewModel.CloseComparisonCommand.Execute(null);
			await viewModel.CompareAsync();
			Assert.Equal(viewModel.Roots[0].Document.FilePath, dialogs.OfferedLeft);
			Assert.Equal(viewModel.Roots[1].Document.FilePath, dialogs.OfferedRight);
			Assert.Equal(2, dialogs.Suggestions.Count);

			// An archive that is gone is not proposed, nothing is proposed twice, the recent files come last.
			viewModel.CloseComparisonCommand.Execute(null);
			SettingsService settings = new(Path.Combine(_root, "other-settings"), null);
			settings.Data.CompareLastLeft = missing;
			settings.Data.CompareLastRight = right;
			settings.Data.RecentFiles.AddRange([left, right.ToUpperInvariant(), missing]);
			MainViewModel fresh = new(dialogs, settings, new TempFolder(), new ManualUiThread());
			_viewModels.Add(fresh);

			IReadOnlyList<String> suggestions = fresh.ComparisonSuggestions();
			Assert.Equal([right, left], suggestions);
		}

		[Fact]
		public void TheSetupDialog_TellsWhatASourceIs()
		{
			String folder = Folder("kind-folder", ("a.txt", "a"));
			String file = Path.Combine(_root, "kind.dat");
			using (DatArchive built = DatArchive.OpenFolder(folder))
			{
				built.SaveAs(file);
			}

			Assert.Equal("Str.CompareKindFolder", Views.CompareSetupDialog.Describe(folder));
			Assert.Equal("Str.CompareKindFile", Views.CompareSetupDialog.Describe(file));
			Assert.Equal(String.Empty, Views.CompareSetupDialog.Describe(String.Empty));
			Assert.Equal(String.Empty, Views.CompareSetupDialog.Describe(Path.Combine(_root, "nothing here")));
		}

		[Fact]
		public async Task ViewModel_AComparisonCanBeRunAgain_AndTheSourcesCanBeRefused()
		{
			(String left, String right) = TwoFolders();
			Dialogs dialogs = new() { Sources = null };
			MainViewModel viewModel = CreateViewModel(dialogs);

			await viewModel.CompareAsync();
			Assert.False(viewModel.IsComparing);

			dialogs.Sources = new ComparisonSources(left, right);
			await viewModel.CompareAsync();
			ComparisonViewModel first = viewModel.Comparison!;

			File.WriteAllText(Path.Combine(left, "only-right.txt"), "right only");
			await viewModel.CompareAgainAsync();

			// The folders were read when they were opened: the list is made again from the archives that are open.
			Assert.NotSame(first, viewModel.Comparison);
			Assert.Equal(first.AllRows.Count, viewModel.Comparison!.AllRows.Count);
		}

		#endregion
	}
}