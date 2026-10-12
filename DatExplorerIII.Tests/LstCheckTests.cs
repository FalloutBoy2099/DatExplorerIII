namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The lists of files (.lst) name files of their folder: the check says which are not there, and which the list does not name.</summary>
	public sealed class LstCheckTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
		private readonly List<DatArchive> _archives = new();

		public void Dispose()
		{
			foreach (DatArchive archive in _archives)
			{
				archive.Dispose();
			}

			if (Directory.Exists(_root))
			{
				Directory.Delete(_root, true);
			}
		}

		/// <summary>A folder opened as an archive, with the files given by their path in it.</summary>
		private DatArchive Archive(String name, params (String Path, String Content)[] files)
		{
			String folder = Path.Combine(_root, name);
			Directory.CreateDirectory(folder);
			foreach ((String path, String content) in files)
			{
				String full = Path.Combine(folder, path);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllText(full, content, Encoding.ASCII);
			}

			DatArchive archive = DatArchive.OpenFolder(folder);
			_archives.Add(archive);
			return archive;
		}

		[Fact]
		public void AListOfAFolderOfTheArt_SaysWhichFilesAreMissing_AndWhichFilesItDoesNotName()
		{
			DatArchive archive = Archive(
				"master",
				("art\\items\\items.lst", "A.FRM\r\nb.frm ; comment\r\nC.FRM\r\n"),
				("art\\items\\a.frm", "x"),
				("art\\items\\c.frm", "x"),
				("art\\items\\d.frm", "x"),
				("art\\items\\other.pal", "x"));

			LstReport report = LstChecker.Check("art\\items\\", "items.lst", File.ReadAllText(Path.Combine(_root, "master", "art", "items", "items.lst")), [archive])!;

			Assert.Equal(new LstMissingFile(2, "b.frm"), Assert.Single(report.Missing));
			Assert.Equal(["d.frm"], report.Unlisted);
			Assert.True(report.HasProblems);
			Assert.Equal([2], report.MissingLines);
		}

		[Fact]
		public void TheOnlyDataOfALine_IsItsFirstWord()
		{
			Assert.Equal([(1, "a.frm"), (3, "b.frm"), (5, "c.frm")], LstChecker.ReadNames("a.frm,1,0\r\n\r\nb.frm\tdata\n;comment only\nc.frm;x"));
			Assert.Equal([(1, "a.frm")], LstChecker.ReadNames("﻿a.frm"));
		}

		[Fact]
		public void TheCrittersAndTheHeads_AreNamedByTheBeginningOfTheirFiles()
		{
			DatArchive archive = Archive(
				"master",
				("art\\critters\\hmjmpsaa.frm", "x"),
				("art\\critters\\hmjmpsab.fr0", "x"),
				("art\\critters\\lonely.frm", "x"),
				("art\\critters\\readme.txt", "x"));

			LstReport report = LstChecker.Check("art\\critters\\", "critters.lst", "hmjmps,1,0\r\nnobody\r\n", [archive])!;

			Assert.Equal(new LstMissingFile(2, "nobody"), Assert.Single(report.Missing));
			Assert.Equal(["lonely.frm"], report.Unlisted);
		}

		[Fact]
		public void AnotherListOfTheFolder_IsOnlyCheckedForMissingFiles_AndSoIsAListOutsideTheArtAndThePrototypes()
		{
			DatArchive archive = Archive(
				"master",
				("art\\items\\a.frm", "x"),
				("art\\items\\b.frm", "x"),
				("scripts\\one.int", "x"),
				("scripts\\two.int", "x"));

			// new.lst is not the list of its folder: the files that it does not name are not its business.
			LstReport extra = LstChecker.Check("art\\items\\", "new.lst", "a.frm\r\nmissing.frm\r\n", [archive])!;
			Assert.Equal("missing.frm", Assert.Single(extra.Missing).Name);
			Assert.Empty(extra.Unlisted);

			// The scripts do not have to all be in the list.
			LstReport scripts = LstChecker.Check("scripts\\", "scripts.lst", "one.int ; first #local_vars=1\r\n", [archive])!;
			Assert.Empty(scripts.Missing);
			Assert.Empty(scripts.Unlisted);
			Assert.False(scripts.HasProblems);
		}

		[Fact]
		public void TheFilesOfTheOtherArchives_Count()
		{
			DatArchive master = Archive("master", ("art\\items\\items.lst", "a.frm\r\nb.frm\r\n"), ("art\\items\\a.frm", "x"));
			DatArchive patch = Archive("patch", ("art\\items\\b.frm", "x"));

			Assert.Single(LstChecker.Check("art\\items\\", "items.lst", "a.frm\r\nb.frm\r\n", [master])!.Missing);
			Assert.False(LstChecker.Check("art\\items\\", "items.lst", "a.frm\r\nb.frm\r\n", [master, patch])!.HasProblems);
		}

		[Fact]
		public void AListThatDoesNotNameFiles_IsNotChecked()
		{
			DatArchive archive = Archive("master", ("sound\\sfx\\sndlist.lst", "x"), ("proto\\tiles\\patterns\\patterns.lst", "x"));

			Assert.Null(LstChecker.Check("sound\\sfx\\", "sndlist.lst", "3\r\nfoo.acm\r\n", [archive]));
			Assert.Null(LstChecker.Check("proto\\tiles\\patterns\\", "patterns.lst", "pattern01\r\npattern02\r\n", [archive]));
		}

		[Fact]
		public void TheLinesThatNameAMissingFile_AreMarkedInTheColors()
		{
			const String text = "a.frm\r\nb.frm,1,0 ; note\r\nc.frm\r\n";

			SyntaxSpan missing = Assert.Single(LstSyntax.Tokenize(text, new HashSet<Int32> { 2 }), span => span.Kind == SyntaxKind.Missing);

			Assert.Equal("b.frm", text.Substring(missing.Start, missing.Length));
			Assert.DoesNotContain(LstSyntax.Tokenize(text), span => span.Kind == SyntaxKind.Missing);
			Int32 position = 0;
			foreach (SyntaxSpan span in LstSyntax.Tokenize(text, new HashSet<Int32> { 1, 2, 3 }))
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
		}

		[Fact]
		public async Task TheViewer_SaysWhatIsWrongWithAList_AndMarksTheLines()
		{
			DatArchive archive = Archive(
				"master",
				("art\\items\\items.lst", "a.frm\r\nb.frm\r\n"),
				("art\\items\\a.frm", "x"),
				("art\\items\\c.frm", "x"),
				("art\\scenery\\scenery.lst", "s.frm\r\n"),
				("art\\scenery\\s.frm", "x"));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\items\\items.lst")!);
			Assert.Contains("Str.PluralLstMissing", viewer.Text.Notice, StringComparison.Ordinal);
			Assert.Contains("Str.PluralLstUnlisted", viewer.Text.Notice, StringComparison.Ordinal);
			Assert.NotNull(viewer.Text.NoticeTip);
			Assert.Contains("b.frm", viewer.Text.NoticeTip, StringComparison.Ordinal);
			Assert.Contains("c.frm", viewer.Text.NoticeTip, StringComparison.Ordinal);
			Assert.Contains(viewer.Text.SyntaxSpans!, span => span.Kind == SyntaxKind.Missing);

			// A list that is right says nothing.
			await viewer.LoadAsync(archive, archive.Find("art\\scenery\\scenery.lst")!);
			Assert.Equal(String.Empty, viewer.Text.Notice);
			Assert.Null(viewer.Text.NoticeTip);
			Assert.DoesNotContain(viewer.Text.SyntaxSpans!, span => span.Kind == SyntaxKind.Missing);
		}
	}
}