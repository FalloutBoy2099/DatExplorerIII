namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models.Comparison;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Texts;
	using DatExplorerIII.ViewModels;

	/// <summary>What the comparison of two archives ignores: the lines of a text, the way a text file is written, the settings.</summary>
	public sealed class ComparisonOptionsTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
		private readonly List<DatArchive> _archives = [];

		public ComparisonOptionsTests()
		{
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			foreach (DatArchive archive in _archives)
			{
				archive.Dispose();
			}

			Directory.Delete(_root, true);
		}

		private static IReadOnlyList<DiffRow> Diff(String[] left, String[] right, ComparisonOptions? options)
		{
			return TextDiff.Compare(left, right, options, out _);
		}

		private static Byte[] Utf8(String text) => new UTF8Encoding(false).GetBytes(text);

		#region Lines

		[Fact]
		public void WithoutOptions_EveryDifferenceOfALineCounts()
		{
			IReadOnlyList<DiffRow> rows = Diff(["a  b", "x"], ["a b", "x"], new ComparisonOptions());

			Assert.Equal([DiffKind.Changed, DiffKind.Same], rows.Select(row => row.Kind));
			Assert.Single(TextDiff.GroupStarts(rows));
		}

		[Fact]
		public void IgnoringWhitespace_TheLinesAreTheSame_AndEachSideShowsItsOwnText()
		{
			IReadOnlyList<DiffRow> rows = Diff(["a  b\t", "x"], ["a b", " x"], new ComparisonOptions { IgnoreWhitespace = true });

			Assert.All(rows, row => Assert.Equal(DiffKind.Same, row.Kind));
			Assert.Equal("a  b\t", rows[0].LeftText);
			Assert.Equal("a b", rows[0].RightText);
			Assert.Empty(TextDiff.GroupStarts(rows));
		}

		[Fact]
		public void IgnoringCase_AndNumbers()
		{
			Assert.Empty(TextDiff.GroupStarts(Diff(["Hello World"], ["hello WORLD"], new ComparisonOptions { IgnoreCase = true })));
			Assert.Single(TextDiff.GroupStarts(Diff(["Hello World"], ["hello WORLD"], new ComparisonOptions { IgnoreNumbers = true })));

			ComparisonOptions numbers = new() { IgnoreNumbers = true };
			Assert.Empty(TextDiff.GroupStarts(Diff(["damage 12, version 1.5"], ["damage 345, version 10.25"], numbers)));
			Assert.Single(TextDiff.GroupStarts(Diff(["damage 12"], ["armor 12"], numbers)));
		}

		[Fact]
		public void IgnoringBlankLines_TheyStayInPlace_AsIgnoredLinesThatAreNoDifference()
		{
			IReadOnlyList<DiffRow> rows = Diff(["a", "", "b"], ["a", "b", "", ""], new ComparisonOptions { IgnoreBlankLines = true });

			Assert.Empty(TextDiff.GroupStarts(rows));
			Assert.Equal([DiffKind.Same, DiffKind.Ignored, DiffKind.Same, DiffKind.Ignored, DiffKind.Ignored], rows.Select(row => row.Kind));

			// Every line of both texts is shown once.
			Assert.Equal([1, 2, 3], rows.Where(row => row.LeftNumber > 0).Select(row => row.LeftNumber));
			Assert.Equal([1, 2, 3, 4], rows.Where(row => row.RightNumber > 0).Select(row => row.RightNumber));

			// Lines that really differ are still found around blank lines.
			rows = Diff(["a", "", "b"], ["a", "c"], new ComparisonOptions { IgnoreBlankLines = true });
			Assert.Single(TextDiff.GroupStarts(rows));
			Assert.Contains(rows, row => row.Kind == DiffKind.Changed && row.LeftText == "b" && row.RightText == "c");
		}

		[Fact]
		public void IgnoringComments_LineBlockAndWholeLineComments()
		{
			ComparisonOptions options = new() { IgnoreComments = true };

			Assert.Empty(TextDiff.GroupStarts(Diff(["x = 1 // the first", "y"], ["x = 1", "y"], options)));
			Assert.Empty(TextDiff.GroupStarts(Diff(["a /* inline */ b"], ["a  b"], options)));
			Assert.Empty(TextDiff.GroupStarts(Diff(["a", "/* a long", "comment */", "b"], ["a", "b"], options)));
			Assert.Empty(TextDiff.GroupStarts(Diff(["# a comment of a message file", "{100}{}{Hello}"], ["{100}{}{Hello}", "; another one"], options)));

			// A real change next to a comment counts, and so does a directive.
			Assert.Single(TextDiff.GroupStarts(Diff(["x = 1 // same"], ["x = 2 // same"], options)));
			Assert.Single(TextDiff.GroupStarts(Diff(["#define A 1"], ["#define A 2"], options)));
		}

		[Fact]
		public void TheOptionsCombine()
		{
			ComparisonOptions options = new() { IgnoreCase = true, IgnoreWhitespace = true, IgnoreBlankLines = true, IgnoreNumbers = true };

			Assert.True(TextDiff.AreEquivalent(["Item 12", "", "  Armor  7"], ["ITEM   3", "armor 8"], options));
			Assert.False(TextDiff.AreEquivalent(["Item 12"], ["Weapon 12"], options));
		}

		#endregion

		#region Files made of text

		[Fact]
		public void LineEndings_AreADifference_UnlessTheyAreIgnored()
		{
			Byte[] windows = Utf8("one\r\ntwo\r\n");
			Byte[] unix = Utf8("one\ntwo\n");
			Byte[] mac = Utf8("one\rtwo\r");

			Assert.False(TextContentComparer.AreEqual(windows, unix, new ComparisonOptions()));
			Assert.False(TextContentComparer.AreEqual(mac, unix, new ComparisonOptions()));
			Assert.True(TextContentComparer.AreEqual(windows, unix, new ComparisonOptions { IgnoreLineEndings = true }));
			Assert.True(TextContentComparer.AreEqual(mac, unix, new ComparisonOptions { IgnoreLineEndings = true }));

			// The difference is told when the lines show none.
			IReadOnlyList<DiffNote> notes = TextContentComparer.Notes(windows, unix, new ComparisonOptions());
			DiffNote note = Assert.Single(notes);
			Assert.Equal("Str.CompareDiffEndings", note.Key);
			Assert.Equal(["Windows (CRLF)", "Unix (LF)"], note.Args);
			Assert.Empty(TextContentComparer.Notes(windows, unix, new ComparisonOptions { IgnoreLineEndings = true }));
		}

		[Fact]
		public void AMissingLastLineBreak_IsADifference_UnlessItIsIgnored()
		{
			Byte[] with = Utf8("one\ntwo\n");
			Byte[] without = Utf8("one\ntwo");

			Assert.False(TextContentComparer.AreEqual(with, without, new ComparisonOptions()));
			Assert.True(TextContentComparer.AreEqual(with, without, new ComparisonOptions { IgnoreMissingFinalEol = true }));
			Assert.Equal("Str.CompareDiffFinalEol", Assert.Single(TextContentComparer.Notes(with, without, new ComparisonOptions())).Key);
		}

		[Fact]
		public void TheCodePage_IsADifference_UnlessItIsIgnored()
		{
			String text = "café crème brûlée";
			Byte[] utf8 = Utf8(text);
			Byte[] windows1252 = Encoding.GetEncoding(1252).GetBytes(text);

			Assert.False(TextContentComparer.AreEqual(utf8, windows1252, new ComparisonOptions()));
			Assert.True(TextContentComparer.AreEqual(utf8, windows1252, new ComparisonOptions { IgnoreCodePage = true }));
			Assert.Equal("Str.CompareDiffCodePage", Assert.Single(TextContentComparer.Notes(utf8, windows1252, new ComparisonOptions())).Key);

			// A byte order mark is a difference of the way the file is written too.
			Byte[] withMark = [0xEF, 0xBB, 0xBF, .. utf8];
			Assert.False(TextContentComparer.AreEqual(utf8, withMark, new ComparisonOptions()));
			Assert.True(TextContentComparer.AreEqual(utf8, withMark, new ComparisonOptions { IgnoreCodePage = true }));
		}

		[Fact]
		public void TextThatDiffersInItsLines_IsNeverTheSame_AndABinaryIsNotRead()
		{
			ComparisonOptions everything = new() { IgnoreCase = true, IgnoreWhitespace = true, IgnoreBlankLines = true, IgnoreNumbers = true, IgnoreComments = true, IgnoreLineEndings = true, IgnoreCodePage = true, IgnoreMissingFinalEol = true };

			Assert.False(TextContentComparer.AreEqual(Utf8("alpha"), Utf8("beta"), everything));
			Assert.False(TextContentComparer.AreEqual([0, 1, 2, 3, 4, 5, 6, 7], [0, 1, 2, 3, 4, 5, 6, 8], everything));
			Assert.True(TextContentComparer.IsText("data\\readme.txt"));
			Assert.True(TextContentComparer.IsText("text\\english\\game\\item.msg"));
			Assert.False(TextContentComparer.IsText("proto\\items\\00000001.pro"));
			Assert.False(TextContentComparer.IsText("art\\pic.frm"));
		}

		#endregion

		#region Comparison of two archives

		private DatArchive Folder(String name, params (String Path, Byte[] Content)[] files)
		{
			String folder = Path.Combine(_root, name);
			Directory.CreateDirectory(folder);
			foreach ((String path, Byte[] content) in files)
			{
				String full = Path.Combine(folder, path);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllBytes(full, content);
			}

			DatArchive archive = DatArchive.OpenFolder(folder);
			_archives.Add(archive);
			return archive;
		}

		private static DatCompareState StateOf(IReadOnlyList<DatCompareItem> items, String path) => items.Single(item => String.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)).State;

		[Fact]
		public void TheComparison_CountsAsTheSame_WhatDiffersOnlyInWhatIsIgnored()
		{
			DatArchive left = Folder("left",
				("windows.txt", Utf8("one\r\ntwo\r\n")),
				("size.txt", Utf8("Hello   World\n")),
				("real.txt", Utf8("one\n")),
				("proto.pro", [1, 2, 3, 4]),
				("same.txt", Utf8("same")));
			DatArchive right = Folder("right",
				("windows.txt", Utf8("one\ntwo\n")),
				("size.txt", Utf8("hello world\n")),
				("real.txt", Utf8("two\n")),
				("proto.pro", [1, 2, 3, 5]),
				("same.txt", Utf8("same")));

			// Byte for byte: only what is the same byte for byte.
			IReadOnlyList<DatCompareItem> binary = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(new ComparisonOptions { Method = CompareMethod.Binary, IgnoreCase = true }));
			Assert.Equal(DatCompareState.Different, StateOf(binary, "windows.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(binary, "size.txt"));
			Assert.Equal(DatCompareState.Identical, StateOf(binary, "same.txt"));

			// As texts with nothing ignored: the same result for these files.
			IReadOnlyList<DatCompareItem> strict = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(new ComparisonOptions()));
			Assert.Equal(DatCompareState.Different, StateOf(strict, "windows.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(strict, "size.txt"));
			Assert.Equal(DatCompareState.Identical, StateOf(strict, "same.txt"));

			// As texts, ignoring the line endings, the white space and the case: the sizes differ, the files are the same.
			ComparisonOptions ignoring = new() { IgnoreLineEndings = true, IgnoreWhitespace = true, IgnoreCase = true };
			IReadOnlyList<DatCompareItem> tolerant = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(ignoring));
			Assert.Equal(DatCompareState.Identical, StateOf(tolerant, "windows.txt"));
			Assert.Equal(DatCompareState.Identical, StateOf(tolerant, "size.txt"));
			Assert.Equal(DatCompareState.Identical, StateOf(tolerant, "same.txt"));

			// What really differs still does, and so does a file that is not a text.
			Assert.Equal(DatCompareState.Different, StateOf(tolerant, "real.txt"));
			Assert.Equal(DatCompareState.Different, StateOf(tolerant, "proto.pro"));

			// Without the way to compare texts (the binary method) the sizes decide as before.
			Assert.Null(TextContentComparer.CreateEquality(new ComparisonOptions { Method = CompareMethod.Binary }));
		}

		#endregion

		#region Settings

		[Fact]
		public void TheSettings_KeepTheOptionsOfTheComparison_AsJson()
		{
			String folder = Path.Combine(_root, "settings");
			SettingsService first = new(folder, null);
			first.Data.ComparisonOptions = new ComparisonOptions { Method = CompareMethod.Binary, IgnoreCase = true, IgnoreComments = true };
			first.Data.CompareSyncScroll = false;
			first.Data.SaveWindowSize = false;
			first.Save();

			Assert.Contains("\"Binary\"", File.ReadAllText(Path.Combine(folder, "settings.json")));

			SettingsService second = new(folder, null);
			Assert.Equal(CompareMethod.Binary, second.Data.ComparisonOptions.Method);
			Assert.True(second.Data.ComparisonOptions.IgnoreCase);
			Assert.True(second.Data.ComparisonOptions.IgnoreComments);
			Assert.False(second.Data.ComparisonOptions.IgnoreNumbers);
			Assert.False(second.Data.CompareSyncScroll);
			Assert.False(second.Data.SaveWindowSize);
			Assert.True(second.Data.SaveWindowPosition);
		}

		[Fact]
		public void TheBinaryMethod_IgnoresNothing()
		{
			ComparisonOptions binary = new() { Method = CompareMethod.Binary, IgnoreCase = true, IgnoreWhitespace = true };

			Assert.Equal(ComparisonOptions.Strict, binary.Effective);
			Assert.True((binary with { Method = CompareMethod.Text }).Effective.IgnoreCase);
		}

		[Fact]
		public void TheDialog_StartsFromTheSettings_AndGivesTheOptionsBack()
		{
			SettingsData data = new()
			{
				SingleInstance = false,
				WindowFrameVisible = true,
				BlockSeparatorsVisible = false,
				SaveWindowPosition = false,
				ComparisonOptions = new ComparisonOptions { IgnoreNumbers = true, Method = CompareMethod.Binary },
			};

			SettingsDialogViewModel model = new(data);
			Assert.False(model.SingleInstance);
			Assert.True(model.WindowFrameVisible);
			Assert.False(model.BlockSeparatorsVisible);
			Assert.True(model.SaveWindowSize);
			Assert.False(model.SaveWindowPosition);
			Assert.True(model.IsBinaryMethod);
			Assert.False(model.IgnoresApply);

			model.IsTextMethod = true;
			model.IgnoreWhitespace = true;
			Assert.False(model.IsBinaryMethod);
			Assert.True(model.IgnoresApply);
			Assert.Equal(new ComparisonOptions { Method = CompareMethod.Text, IgnoreNumbers = true, IgnoreWhitespace = true }, model.ToComparisonOptions());
		}

		[Fact]
		public void ThePlaceOfTheWindow_KeepsOnlyWhatTheUserWants()
		{
			WindowPlacement before = new(10, 20, 800, 600, false);
			WindowPlacement now = new(100, 200, 1000, 700, true);

			Assert.Equal(now, WindowPlacement.ForSaving(before, now, true, true));
			Assert.Equal(new WindowPlacement(10, 20, 1000, 700, true), WindowPlacement.ForSaving(before, now, true, false));
			Assert.Equal(new WindowPlacement(100, 200, 800, 600, false), WindowPlacement.ForSaving(before, now, false, true));
			Assert.Equal(before, WindowPlacement.ForSaving(before, now, false, false));
			Assert.Equal(now, WindowPlacement.ForSaving(null, now, true, false));
			Assert.Null(WindowPlacement.ForSaving(null, now, false, false));
		}

		#endregion
	}
}