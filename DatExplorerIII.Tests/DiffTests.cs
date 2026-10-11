namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>Showing the differences between two versions of a file made of text.</summary>
	public sealed class DiffTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
		private readonly List<MainViewModel> _viewModels = new();

		public DiffTests()
		{
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			foreach (DatArchive archive in _viewModels.SelectMany(viewModel => viewModel.Roots).Select(root => root.Document.Archive))
			{
				archive.Dispose();
			}

			Directory.Delete(_root, true);
		}

		#region The comparison of lines

		private static String[] Lines(String text) => text.Length == 0 ? [] : text.Split(' ');

		private static (List<String> Left, List<String> Right) Sides(IReadOnlyList<DiffRow> rows)
		{
			return (rows.Where(row => row.LeftNumber > 0).Select(row => row.LeftText).ToList(), rows.Where(row => row.RightNumber > 0).Select(row => row.RightText).ToList());
		}

		[Fact]
		public void TheSameTexts_AreAllTheSame_AndHaveNoDifference()
		{
			IReadOnlyList<DiffRow> rows = TextDiff.Compare(Lines("a b c"), Lines("a b c"), out Boolean exact);

			Assert.True(exact);
			Assert.Equal(3, rows.Count);
			Assert.All(rows, row => Assert.Equal(DiffKind.Same, row.Kind));
			Assert.Empty(TextDiff.GroupStarts(rows));
			Assert.Equal(["1", "2", "3"], rows.Select(row => row.LeftNumberText));
		}

		[Fact]
		public void AChangedLine_AnAddedLine_AndARemovedLine_AreToldApart()
		{
			IReadOnlyList<DiffRow> changed = TextDiff.Compare(Lines("a b c"), Lines("a X c"), out _);
			Assert.Equal([DiffKind.Same, DiffKind.Changed, DiffKind.Same], changed.Select(row => row.Kind));
			Assert.Equal("b", changed[1].LeftText);
			Assert.Equal("X", changed[1].RightText);

			IReadOnlyList<DiffRow> added = TextDiff.Compare(Lines("a c"), Lines("a b c"), out _);
			Assert.Equal([DiffKind.Same, DiffKind.RightOnly, DiffKind.Same], added.Select(row => row.Kind));
			Assert.Equal(0, added[1].LeftNumber);
			Assert.Equal(2, added[1].RightNumber);
			Assert.Equal(String.Empty, added[1].LeftText);

			IReadOnlyList<DiffRow> removed = TextDiff.Compare(Lines("a b c"), Lines("a c"), out _);
			Assert.Equal([DiffKind.Same, DiffKind.LeftOnly, DiffKind.Same], removed.Select(row => row.Kind));
			Assert.Equal(2, removed[1].LeftNumber);
			Assert.Equal(0, removed[1].RightNumber);

			// The groups of lines that differ: two separate ones are two differences, a group of lines is one.
			Assert.Equal([1], TextDiff.GroupStarts(changed));
			Assert.Equal(2, TextDiff.GroupStarts(TextDiff.Compare(Lines("a b c d e"), Lines("a X c Y e"), out _)).Count);
			Assert.Single(TextDiff.GroupStarts(TextDiff.Compare(Lines("a b c d e"), Lines("a X Y Z e"), out _)));
		}

		[Fact]
		public void ALineThatBecameSeveral_IsOneChangedLineAndAddedLines()
		{
			IReadOnlyList<DiffRow> rows = TextDiff.Compare(Lines("a b d"), Lines("a X Y Z d"), out _);

			Assert.Equal([DiffKind.Same, DiffKind.Changed, DiffKind.RightOnly, DiffKind.RightOnly, DiffKind.Same], rows.Select(row => row.Kind));
		}

		[Fact]
		public void EmptyTexts_AreHandled()
		{
			Assert.Empty(TextDiff.Compare([], [], out _));
			Assert.All(TextDiff.Compare([], Lines("a b"), out _), row => Assert.Equal(DiffKind.RightOnly, row.Kind));
			Assert.All(TextDiff.Compare(Lines("a b"), [], out _), row => Assert.Equal(DiffKind.LeftOnly, row.Kind));
		}

		[Fact]
		public void TheRows_AlwaysGiveBothTextsBack_AndMatchAsMuchAsPossible()
		{
			Random random = new(2026);
			String[] alphabet = ["a", "b", "c", "d", "e"];

			for (Int32 round = 0; round < 400; round++)
			{
				String[] left = Enumerable.Range(0, random.Next(0, 13)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
				String[] right = Enumerable.Range(0, random.Next(0, 13)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();

				IReadOnlyList<DiffRow> rows = TextDiff.Compare(left, right, out Boolean exact);
				(List<String> backLeft, List<String> backRight) = Sides(rows);

				Assert.True(exact);
				Assert.Equal(left, backLeft);
				Assert.Equal(right, backRight);
				Assert.All(rows.Where(row => row.Kind == DiffKind.Same), row => Assert.Equal(row.LeftText, row.RightText));
				Assert.All(rows.Where(row => row.Kind == DiffKind.Changed), row => Assert.NotEqual(row.LeftText, row.RightText));

				// The lines that stay side by side are a longest common subsequence: no match is missed.
				Assert.Equal(CommonLength(left, right), rows.Count(row => row.Kind == DiffKind.Same));
			}
		}

		private static Int32 CommonLength(String[] left, String[] right)
		{
			Int32[,] table = new Int32[left.Length + 1, right.Length + 1];
			for (Int32 i = 1; i <= left.Length; i++)
			{
				for (Int32 j = 1; j <= right.Length; j++)
				{
					table[i, j] = left[i - 1] == right[j - 1] ? table[i - 1, j - 1] + 1 : Math.Max(table[i - 1, j], table[i, j - 1]);
				}
			}

			return table[left.Length, right.Length];
		}

		[Fact]
		public void TextsThatAreTooDifferent_ArePairedLineByLine_AndSaySo()
		{
			String[] left = Enumerable.Range(0, TextDiff.MaxDifferences + 500).Select(index => "left " + index).ToArray();
			String[] right = Enumerable.Range(0, TextDiff.MaxDifferences + 500).Select(index => "right " + index).ToArray();

			IReadOnlyList<DiffRow> rows = TextDiff.Compare(left, right, out Boolean exact);

			Assert.False(exact);
			Assert.Equal(left.Length, rows.Count);
			Assert.All(rows, row => Assert.Equal(DiffKind.Changed, row.Kind));
			(List<String> backLeft, List<String> backRight) = Sides(rows);
			Assert.Equal(left, backLeft);
			Assert.Equal(right, backRight);
		}

		[Fact]
		public void ALargeTextWithAFewChanges_IsComparedQuickly()
		{
			String[] left = Enumerable.Range(0, 150_000).Select(index => "line " + index).ToArray();
			String[] right = (String[])left.Clone();
			right[10] = "changed";
			right[75_000] = "changed";
			right[149_990] = "changed";

			IReadOnlyList<DiffRow> rows = TextDiff.Compare(left, right, out Boolean exact);

			Assert.True(exact);
			Assert.Equal(150_000, rows.Count);
			Assert.Equal(3, TextDiff.GroupStarts(rows).Count);
		}

		#endregion

		#region Files as lines

		[Fact]
		public void FileTextView_WritesTextFilesAsLines_WhateverTheEndOfTheirLines()
		{
			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("a.txt", Encoding.ASCII.GetBytes("one\r\ntwo\nthree\rfour\r\n"), null, out IReadOnlyList<String> lines));
			Assert.Equal(["one", "two", "three", "four"], lines);

			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("b.msg", Encoding.ASCII.GetBytes("{1}{}{Hello}"), null, out lines));
			Assert.Equal(["{1}{}{Hello}"], lines);
		}

		[Fact]
		public void FileTextView_RefusesWhatIsNotText()
		{
			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("picture.frm", new Byte[100], null, out _));
			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("sound.acm", new Byte[100], null, out _));
			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("unknown.bin", new Byte[100], null, out _));
			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("broken.pro", new Byte[10], null, out _));

			// A text that looks binary is not shown as a text.
			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("fake.txt", Enumerable.Range(0, 400).Select(index => (Byte)(index % 5)).ToArray(), null, out _));

			Assert.Equal(FileTextResult.TooLarge, FileTextView.TryGetLines("big.txt", new Byte[FileTextView.MaxBytes + 1], null, out _));
		}

		[Fact]
		public void FileTextView_WritesMessagesAndDialogsAsOneLinePerEntry()
		{
			Byte[] mes = Encoding.ASCII.GetBytes("{1}{one}\r\n{2}{two\r\nlines}\r\n");
			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("game.mes", mes, null, out IReadOnlyList<String> lines));
			Assert.Equal(["1: one", "2: two ¶ lines"], lines);

			Byte[] dlg = Encoding.ASCII.GetBytes("{1}{Hello}{}{}{}{5}{}\r\n");
			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("talk.dlg", dlg, null, out lines));
			Assert.Single(lines);
			Assert.StartsWith("1 | Hello", lines[0], StringComparison.Ordinal);
		}

		#endregion

		[Fact]
		public void RealGame_PrototypesCharactersMapsAndMessagesAreWrittenAsLines()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_FALLOUT_DIR");
			if (folder == null || !File.Exists(Path.Combine(folder, "master.dat")))
			{
				return;
			}

			using DatArchive master = DatArchive.Open(Path.Combine(folder, "master.dat"));
			MapResources resources = new(path =>
			{
				DatEntry? entry = master.Find(path);
				return entry == null ? null : master.ReadData(entry);
			});

			foreach (String extension in new[] { ".pro", ".gcd", ".map", ".msg", ".bio" })
			{
				DatEntry entry = master.Entries.First(candidate => candidate.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
				Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines(entry.Name, master.ReadData(entry), resources, out IReadOnlyList<String> lines));
				Assert.NotEmpty(lines);
			}

			// The two versions of a prototype whose damage was changed differ by one line only.
			DatEntry weapon = master.Entries.First(candidate => candidate.FullPath.StartsWith(@"proto\items\", StringComparison.OrdinalIgnoreCase) && candidate.Name.EndsWith(".pro", StringComparison.OrdinalIgnoreCase));
			Byte[] original = master.ReadData(weapon);
			FileTextView.TryGetLines(weapon.Name, original, resources, out IReadOnlyList<String> before);
			Assert.StartsWith("[", before[0], StringComparison.Ordinal);
			Assert.Empty(TextDiff.GroupStarts(TextDiff.Compare(before, before, out _)));
		}

		#region In the comparison

		private sealed class Dialogs : IDialogService
		{
			public ComparisonSources? Sources { get; set; }

			public String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons) => null;

			public Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question) => true;

			public void ShowMessage(String title, String message, DialogIcon icon)
			{
			}

			public void ShowError(String message)
			{
			}

			public String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate) => null;

			public IReadOnlyList<String> PickDatFiles(String? initialFolder) => [];

			public String? PickNewDatFile(DatFormat format, String? initialFolder) => null;

			public SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat) => null;

			public ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions) => Sources;

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

		private String Folder(String name, params (String Path, Byte[] Content)[] files)
		{
			String folder = Path.Combine(_root, name);
			Directory.CreateDirectory(folder);
			foreach ((String path, Byte[] content) in files)
			{
				File.WriteAllBytes(Path.Combine(folder, path), content);
			}

			return folder;
		}

		private static Byte[] Text(String text) => Encoding.ASCII.GetBytes(text);

		private async Task<ComparisonViewModel> CompareAsync((String Path, Byte[] Content)[] left, (String Path, Byte[] Content)[] right)
		{
			Dialogs dialogs = new() { Sources = new ComparisonSources(Folder("left", left), Folder("right", right)) };
			MainViewModel viewModel = new(dialogs, new SettingsService(Path.Combine(_root, "settings"), null), new TempFolder());
			_viewModels.Add(viewModel);
			await viewModel.CompareAsync();
			return viewModel.Comparison!;
		}

		private static async Task SelectAsync(ComparisonViewModel comparison, String path)
		{
			comparison.SetSelection(comparison.AllRows.Where(row => row.Path == path).ToList());
			await comparison.DiffTask;
		}

		[Fact]
		public async Task Comparison_ShowsTheDifferencesOfALineByLine_WhenTheOptionIsOn()
		{
			ComparisonViewModel comparison = await CompareAsync(
				[("a.txt", Text("one\r\ntwo\r\nthree\r\nfour\r\nfive\r\n")), ("same.txt", Text("same"))],
				[("a.txt", Text("one\ntwo changed\nthree\nfour\nfive\nsix\n")), ("same.txt", Text("same"))]);

			// Without the option the two versions are in the viewers, and no difference is computed.
			await SelectAsync(comparison, "a.txt");
			Assert.False(comparison.ShowDifferences);
			Assert.Empty(comparison.Diff.Rows);

			comparison.ShowDifferences = true;
			await comparison.DiffTask;

			Assert.False(comparison.Diff.HasMessage);
			Assert.True(comparison.Diff.HasRows);
			Assert.Equal(2, comparison.Diff.DifferenceCount);
			Assert.Equal([DiffKind.Same, DiffKind.Changed, DiffKind.Same, DiffKind.Same, DiffKind.Same, DiffKind.RightOnly], comparison.Diff.Rows.Select(row => row.Kind));
			Assert.Equal("two", comparison.Diff.Rows[1].LeftText);
			Assert.Equal("two changed", comparison.Diff.Rows[1].RightText);

			// The first difference is where the list stands; "next" goes to the other, then comes back to the first.
			Assert.Same(comparison.Diff.Rows[1], comparison.Diff.SelectedRow);
			comparison.Diff.NextCommand.Execute(null);
			Assert.Same(comparison.Diff.Rows[5], comparison.Diff.SelectedRow);
			comparison.Diff.NextCommand.Execute(null);
			Assert.Same(comparison.Diff.Rows[1], comparison.Diff.SelectedRow);
			comparison.Diff.PreviousCommand.Execute(null);
			Assert.Same(comparison.Diff.Rows[5], comparison.Diff.SelectedRow);
		}

		[Fact]
		public async Task Comparison_SaysWhyThereIsNothingToShow()
		{
			Byte[] picture = new Byte[200];
			ComparisonViewModel comparison = await CompareAsync(
				[("same.txt", Text("same")), ("left-only.txt", Text("x")), ("picture.frm", picture), ("big.txt", new Byte[FileTextView.MaxBytes + 10])],
				[("same.txt", Text("same")), ("right-only.txt", Text("y")), ("picture.frm", new Byte[201]), ("big.txt", new Byte[FileTextView.MaxBytes + 11])]);
			comparison.ShowDifferences = true;

			await SelectAsync(comparison, "same.txt");
			Assert.Equal("Str.CompareDiffIdentical", comparison.Diff.Message);

			await SelectAsync(comparison, "left-only.txt");
			Assert.Equal("Str.CompareDiffOnlyOne", comparison.Diff.Message);

			await SelectAsync(comparison, "picture.frm");
			Assert.Equal("Str.CompareDiffNotText", comparison.Diff.Message);

			await SelectAsync(comparison, "big.txt");
			Assert.Equal("Str.CompareDiffTooLarge", comparison.Diff.Message);

			comparison.SetSelection([]);
			await comparison.DiffTask;
			Assert.Equal("Str.CompareDiffSelect", comparison.Diff.Message);

			// Turning the option off brings the viewers back: no more difference.
			comparison.ShowDifferences = false;
			await comparison.DiffTask;
		}

		[Fact]
		public async Task Comparison_TheOptionIsKept_AndSavedFilesOfTheGameAreComparedByTheirFields()
		{
			// A file of messages is compared by entry: only the entry that changed is a difference.
			ComparisonViewModel comparison = await CompareAsync(
				[("game.mes", Text("{1}{one}\r\n{2}{two}\r\n{3}{three}\r\n"))],
				[("game.mes", Text("{1}{one}\r\n{2}{deux}\r\n{3}{three}\r\n"))]);
			comparison.ShowDifferences = true;
			await SelectAsync(comparison, "game.mes");

			Assert.Equal(1, comparison.Diff.DifferenceCount);
			Assert.Equal("2: two", comparison.Diff.Rows.Single(row => row.Kind == DiffKind.Changed).LeftText);
			Assert.Equal("2: deux", comparison.Diff.Rows.Single(row => row.Kind == DiffKind.Changed).RightText);

			Assert.True(new SettingsService(Path.Combine(_root, "settings"), null).Data.CompareShowDifferences);
		}

		#endregion
	}
}