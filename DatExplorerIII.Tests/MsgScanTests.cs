namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.Views;

	/// <summary>The check of the message files: of a folder, an archive, and a command line; and the window that lists the problems.</summary>
	public sealed class MsgScanTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		/// <summary>Says a problem with its name, so that the tests do not depend on the language.</summary>
		private static readonly MsgReportText Plain = new(issue => issue.Kind + " " + issue.Value, severity => severity.ToString());

		public MsgScanTests()
		{
			Directory.CreateDirectory(_root);
		}

		public void Dispose()
		{
			Directory.Delete(_root, true);
		}

		private String Write(String relative, String text)
		{
			String path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, text, new UTF8Encoding(false));
			return path;
		}

		/// <summary>A folder with a clean file, a broken one (critical and high) and one with a medium problem in a sub folder.</summary>
		private String MakeFolder()
		{
			Write("text/clean.msg", "{1}{}{Fine}\n");
			Write("text/broken.msg", "{x}{}{a}\n{2}{}{b}\n{2}{}{c}\n");
			Write("text/sub/medium.msg", "{1}{}{No final line}");
			Write("text/readme.txt", "{x}{}{not a message file}");
			return Path.Combine(_root, "text");
		}

		// ---- the scanner

		[Fact]
		public void TheScanner_FindsTheMessageFilesOfAFolder_InAllItsSubFolders()
		{
			IReadOnlyList<MsgFileReport> reports = MsgScanner.ScanPath(MakeFolder());

			Assert.Equal(["broken.msg", "clean.msg", "medium.msg"], reports.Select(report => Path.GetFileName(report.Path)));
			Assert.Equal(2, reports.First().Issues.Count);
			Assert.Empty(reports.Single(report => report.Path.EndsWith("clean.msg", StringComparison.Ordinal)).Issues);
			Assert.Equal(MsgIssueSeverity.Medium, reports.Last().Worst);
		}

		[Fact]
		public void TheScanner_ReadsTheTextWhateverItsEncoding_AndSkipsWhatIsNotText()
		{
			Byte[] windows = Encoding.GetEncoding(1252).GetBytes("{1}{}{café à la carte}\r\n{1}{}{double}\r\n");
			Byte[] binary = new Byte[400];
			new Random(3).NextBytes(binary);
			binary[10] = 0;

			Assert.Equal(MsgIssueKind.DuplicateId, MsgScanner.ScanData("w.msg", windows).Issues.Single().Kind);
			Assert.Empty(MsgScanner.ScanData("b.msg", binary).Issues);
		}

		[Fact]
		public void TheScanner_ReportsAPathThatIsNotThere_AndAFileOfTheDiskThatHasAnyName()
		{
			MsgFileReport missing = MsgScanner.ScanPath(Path.Combine(_root, "nothing.msg")).Single();
			Assert.NotNull(missing.ReadError);

			String odd = Write("odd.txt", "{9}{}{x}");
			MsgFileReport report = MsgScanner.ScanPath(odd).Single();
			Assert.Equal(MsgIssueKind.NoFinalEmptyLine, report.Issues.Single().Kind);
		}

		[Fact]
		public void TheScanner_ChecksTheMessageFilesOfAnArchive_WithTheirPathInIt()
		{
			MakeFolder();
			String dat = Path.Combine(_root, "master.dat");
			using (DatArchive folder = DatArchive.OpenFolder(Path.Combine(_root, "text")))
			{
				folder.SaveAs(dat, DatFormat.Fallout2);
			}

			IReadOnlyList<MsgFileReport> reports = MsgScanner.ScanPath(dat);

			Assert.Equal(3, reports.Count);
			Assert.All(reports, report => Assert.StartsWith(dat + ":", report.Path, StringComparison.Ordinal));
			Assert.Contains(reports, report => report.Path.EndsWith("broken.msg", StringComparison.Ordinal) && report.Count(MsgIssueSeverity.Critical) == 1 && report.Count(MsgIssueSeverity.High) == 1);

			// An archive that is not one is reported, not thrown.
			String notDat = Write("fake.dat", "this is not an archive");
			Assert.NotNull(MsgScanner.ScanPath(notDat).Single().ReadError);
		}

		[Fact]
		public void TheScanner_StopsWhenItIsCancelled()
		{
			MakeFolder();
			using CancellationTokenSource cancelled = new();
			cancelled.Cancel();

			Assert.Throws<OperationCanceledException>(() => MsgScanner.ScanPath(Path.Combine(_root, "text"), null, cancelled.Token));
		}

		// ---- the command line

		[Fact]
		public void TheCommandLine_IsRecognizedByItsSwitch()
		{
			Assert.True(MsgCommandLine.IsCommand(["--scan-msg", "x"]));
			Assert.True(MsgCommandLine.IsCommand(["--SCAN-MSG"]));
			Assert.False(MsgCommandLine.IsCommand(["file.dat"]));
			Assert.False(MsgCommandLine.IsCommand([]));
		}

		[Fact]
		public void TheCommandLine_WritesTheReportToTheConsoleAndToTheLog_AndEndsWithTheCodeOfTheProblems()
		{
			String folder = MakeFolder();
			String log = Path.Combine(_root, "report.log");
			using StringWriter console = new();

			Int32 code = MsgCommandLine.Run(["--scan-msg", folder, "--log", log], console, Plain, _root);

			String report = console.ToString();
			Assert.Equal(MsgCommandLine.ExitProblems, code);
			Assert.Contains("broken.msg", report, StringComparison.Ordinal);
			Assert.Contains("[Critical] 1:1  InvalidId", report, StringComparison.Ordinal);
			Assert.Contains("[High] 3:1  DuplicateId 2", report, StringComparison.Ordinal);
			Assert.Contains("[Medium]", report, StringComparison.Ordinal);
			Assert.Contains("{x}{}{a}", report, StringComparison.Ordinal);
			Assert.DoesNotContain("clean.msg", report, StringComparison.Ordinal);
			Assert.DoesNotContain("readme.txt", report, StringComparison.Ordinal);
			Assert.Contains("3 problems in 2 of 3 files: 1 Critical, 1 High, 1 Medium.", report, StringComparison.Ordinal);
			Assert.Equal(report, File.ReadAllText(log));
		}

		[Fact]
		public void TheCommandLine_FiltersBySeverity_AndCanForgetTheExitCode()
		{
			String folder = MakeFolder();
			using StringWriter console = new();

			Int32 code = MsgCommandLine.Run(["--scan-msg", "--min-severity", "high", "--no-exitcode", folder], console, Plain, _root);

			Assert.Equal(MsgCommandLine.ExitClean, code);
			Assert.DoesNotContain("[Medium]", console.ToString(), StringComparison.Ordinal);
			Assert.Contains("[High]", console.ToString(), StringComparison.Ordinal);
			Assert.Contains("2 problems in 1 of 3 files", console.ToString(), StringComparison.Ordinal);

			using StringWriter critical = new();
			Assert.Equal(MsgCommandLine.ExitProblems, MsgCommandLine.Run(["--scan-msg", "--min-severity", "critical", folder], critical, Plain, _root));
			Assert.Contains("1 problem in 1 of 3 files", critical.ToString(), StringComparison.Ordinal);
		}

		[Fact]
		public void TheCommandLine_OfFilesThatAreRight_EndsWithZero()
		{
			Write("good/a.msg", "{1}{}{Fine}\n");
			Write("good/b.msg", "# comment\n{2}{}{Fine too}\n");
			using StringWriter console = new();

			Int32 code = MsgCommandLine.Run(["--scan-msg", Path.Combine(_root, "good")], console, Plain, _root);

			Assert.Equal(MsgCommandLine.ExitClean, code);
			Assert.Contains("0 problems in 0 of 2 files", console.ToString(), StringComparison.Ordinal);
		}

		[Fact]
		public void TheCommandLine_ChecksTheFilesAndFoldersItIsGiven_AndTheCurrentFolderWhenItIsGivenNone()
		{
			String folder = MakeFolder();
			String single = Path.Combine(folder, "broken.msg");
			using StringWriter two = new();

			MsgCommandLine.Run(["--scan-msg", single, Path.Combine(folder, "sub")], two, Plain, _root);

			Assert.Contains("problems in 2 of 2 files", two.ToString(), StringComparison.Ordinal);
			Assert.Contains("broken.msg", two.ToString(), StringComparison.Ordinal);
			Assert.Contains("medium.msg", two.ToString(), StringComparison.Ordinal);

			String previous = Environment.CurrentDirectory;
			try
			{
				Environment.CurrentDirectory = Path.Combine(folder, "sub");
				using StringWriter current = new();
				MsgCommandLine.Run(["--scan-msg"], current, Plain, _root);
				Assert.Contains("medium.msg", current.ToString(), StringComparison.Ordinal);
			}
			finally
			{
				Environment.CurrentDirectory = previous;
			}
		}

		[Fact]
		public void TheCommandLine_WithAWrongPathOrOption_EndsWithTheUsageCode()
		{
			using StringWriter missing = new();
			Assert.Equal(MsgCommandLine.ExitUsage, MsgCommandLine.Run(["--scan-msg", Path.Combine(_root, "nowhere")], missing, Plain, _root));
			Assert.Contains("Cannot be checked", missing.ToString(), StringComparison.Ordinal);

			using StringWriter unknown = new();
			Assert.Equal(MsgCommandLine.ExitUsage, MsgCommandLine.Run(["--scan-msg", "--nope"], unknown, Plain, _root));
			Assert.Contains("Usage:", unknown.ToString(), StringComparison.Ordinal);

			using StringWriter help = new();
			Assert.Equal(MsgCommandLine.ExitClean, MsgCommandLine.Run(["--scan-msg", "--help"], help, Plain, _root));
			Assert.Contains("--log <file>", help.ToString(), StringComparison.Ordinal);

			// A severity that does not exist is an unknown option, not a silent default.
			using StringWriter severity = new();
			Assert.Equal(MsgCommandLine.ExitUsage, MsgCommandLine.Run(["--scan-msg", "--min-severity", "urgent"], severity, Plain, _root));
		}

		[Fact]
		public void TheCommandLine_WithoutAConsole_WritesTheLogInTheWorkingFolder()
		{
			String folder = MakeFolder();

			Int32 code = MsgCommandLine.Run(["--scan-msg", folder], null, Plain, _root);

			Assert.Equal(MsgCommandLine.ExitProblems, code);
			Assert.Contains("broken.msg", File.ReadAllText(Path.Combine(_root, MsgCommandLine.DefaultLogName)), StringComparison.Ordinal);
		}

		[Fact]
		public void TheCommandLine_TellsWhenTheLogCannotBeWritten()
		{
			String folder = MakeFolder();
			using StringWriter console = new();

			Int32 code = MsgCommandLine.Run(["--scan-msg", folder, "--log", Path.Combine(_root, "no-such-folder", "x.log")], console, Plain, _root);

			Assert.Equal(MsgCommandLine.ExitUsage, code);
			Assert.Contains("Cannot write the log", console.ToString(), StringComparison.Ordinal);
		}

		// ---- the list of the problems

		private static MsgIssuesViewModel Issues(ManualUiThread ui)
		{
			MsgFileReport second = new("b.msg", [new MsgIssue(MsgIssueSeverity.Medium, MsgIssueKind.NoFinalEmptyLine, 9, 4, 0, 0, "{9}{}{x}")]);
			MsgFileReport first = new("a.msg", [
				new MsgIssue(MsgIssueSeverity.High, MsgIssueKind.DuplicateId, 7, 1, 5, 2, "{5}{}{y}"),
				new MsgIssue(MsgIssueSeverity.Critical, MsgIssueKind.InvalidId, 3, 1, 0, 0, "{x}{}{z}"),
			]);
			return new MsgIssuesViewModel([second, first, new MsgFileReport("c.msg", Array.Empty<MsgIssue>())], Plain, ui);
		}

		[Fact]
		public void TheFiles_AreListedByTheirWorstProblem_AndTheFirstOneIsSelected()
		{
			MsgIssuesViewModel issues = Issues(new ManualUiThread());

			Assert.Equal(["a.msg", "b.msg"], issues.Files.Select(file => file.Name));
			Assert.Equal([2, 1], issues.Files.Select(file => file.Count));
			Assert.Equal(MsgIssueSeverity.Critical, issues.Files[0].Worst);
			Assert.Same(issues.Files[0], issues.SelectedFile);
			Assert.Equal("Critical (1)", issues.CriticalText);
			Assert.Equal("Medium (1)", issues.MediumText);

			// The problems of a file are in the order of the file.
			Assert.Equal([3, 7], issues.SelectedFile!.Issues.Select(row => row.Line));
			Assert.Equal("1 / 2", issues.PositionText);
			Assert.Equal("Critical", issues.CurrentSeverityText);
			Assert.Equal(MsgIssueSeverity.Critical, issues.CurrentSeverity);
			Assert.Equal("InvalidId 0", issues.CurrentMessage);
			Assert.Equal("{x}{}{z}", issues.CurrentValue);
			Assert.NotEqual(String.Empty, issues.CurrentLocation);
		}

		[Fact]
		public void TheStepper_GoesFromTheFirstProblemOfTheFileToTheLastOne()
		{
			MsgIssuesViewModel issues = Issues(new ManualUiThread());

			Assert.False(issues.FirstCommand.CanExecute(null));
			Assert.False(issues.PreviousCommand.CanExecute(null));
			Assert.True(issues.NextCommand.CanExecute(null));
			Assert.True(issues.LastCommand.CanExecute(null));

			issues.NextCommand.Execute(null);
			Assert.Equal(1, issues.Position);
			Assert.Equal("2 / 2", issues.PositionText);
			Assert.Equal("High", issues.CurrentSeverityText);
			Assert.Equal("DuplicateId 5", issues.CurrentMessage);
			Assert.False(issues.NextCommand.CanExecute(null));
			Assert.False(issues.LastCommand.CanExecute(null));
			Assert.True(issues.FirstCommand.CanExecute(null));
			Assert.True(issues.PreviousCommand.CanExecute(null));

			issues.FirstCommand.Execute(null);
			Assert.Equal(0, issues.Position);
			issues.LastCommand.Execute(null);
			Assert.Equal(1, issues.Position);
			issues.PreviousCommand.Execute(null);
			Assert.Equal(0, issues.Position);
		}

		[Fact]
		public void ThePosition_StaysInTheFile_AndStartsAgainWithAnotherFile()
		{
			MsgIssuesViewModel issues = Issues(new ManualUiThread());

			issues.Position = 40;
			Assert.Equal(1, issues.Position);
			issues.Position = -3;
			Assert.Equal(0, issues.Position);

			issues.LastCommand.Execute(null);
			issues.SelectedFile = issues.Files[1];
			Assert.Equal(0, issues.Position);
			Assert.Equal("1 / 1", issues.PositionText);
			Assert.Equal("Medium", issues.CurrentSeverityText);
			Assert.False(issues.NextCommand.CanExecute(null));
			Assert.False(issues.LastCommand.CanExecute(null));
		}

		[Fact]
		public void TheFilters_HideTheSeverities_AndTheSelectedFileStaysWhenItIsStillThere()
		{
			MsgIssuesViewModel issues = Issues(new ManualUiThread());
			List<String?> changes = new();
			issues.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
			issues.SelectedFile = issues.Files[1];

			issues.ShowCritical = false;
			Assert.Contains(nameof(MsgIssuesViewModel.Files), changes);
			Assert.Equal(["a.msg", "b.msg"], issues.Files.Select(file => file.Name));
			Assert.Equal([1, 1], issues.Files.Select(file => file.Count));
			Assert.Equal("b.msg", issues.SelectedFile!.Name);

			issues.ShowHigh = false;
			Assert.Equal(["b.msg"], issues.Files.Select(file => file.Name));
			Assert.Equal(MsgIssueSeverity.Medium, issues.SelectedFile!.Worst);

			issues.ShowMedium = false;
			Assert.Empty(issues.Files);
			Assert.False(issues.HasFiles);
			Assert.Null(issues.SelectedFile);
			Assert.Null(issues.CurrentIssue);
			Assert.Equal(String.Empty, issues.PositionText);
			Assert.Equal(String.Empty, issues.CurrentMessage);
			Assert.False(issues.CopyMessageCommand.CanExecute(null));
			Assert.False(issues.NextCommand.CanExecute(null));

			issues.ShowHigh = true;
			Assert.Equal("a.msg", Assert.Single(issues.Files).Name);
			Assert.Equal("High", issues.CurrentSeverityText);
		}

		[Fact]
		public void TheSummary_CountsAllTheProblems_WhateverTheFilters()
		{
			MsgIssuesViewModel issues = Issues(new ManualUiThread());
			String before = issues.Summary;

			issues.ShowCritical = false;

			Assert.Equal(before, issues.Summary);
			Assert.Equal(MsgIssueSummary.Describe(1, 1, 1), issues.Summary);
			Assert.Equal(String.Empty, MsgIssueSummary.Describe(0, 0, 0));
		}

		[Fact]
		public void TheErrorAndTheValue_CanBeCopiedEachOnItsOwn()
		{
			ManualUiThread ui = new();
			MsgIssuesViewModel issues = Issues(ui);
			issues.NextCommand.Execute(null);

			issues.CopyMessageCommand.Execute(null);
			issues.CopyValueCommand.Execute(null);

			Assert.Equal(["DuplicateId 5", "{5}{}{y}"], ui.Clipboard);
		}

		[Theory]
		[InlineData("en", 1, 0)]
		[InlineData("en", 0, 1)]
		[InlineData("en", 2, 1)]
		[InlineData("fr", 0, 0)]
		[InlineData("fr", 1, 0)]
		[InlineData("fr", 2, 1)]
		[InlineData("de", 1, 0)]
		[InlineData("de", 5, 1)]
		[InlineData("zh", 1, 0)]
		[InlineData("zh", 25, 0)]
		[InlineData("ru", 1, 0)]
		[InlineData("ru", 21, 0)]
		[InlineData("ru", 2, 1)]
		[InlineData("ru", 24, 1)]
		[InlineData("ru", 5, 2)]
		[InlineData("ru", 11, 2)]
		[InlineData("ru", 12, 2)]
		[InlineData("ru", 111, 2)]
		[InlineData("ru", 0, 2)]
		[InlineData("pl", 1, 0)]
		[InlineData("pl", 21, 2)]
		[InlineData("pl", 22, 1)]
		[InlineData("pl", 12, 2)]
		[InlineData("pl", 5, 2)]
		[InlineData("cs", 1, 0)]
		[InlineData("cs", 4, 1)]
		[InlineData("cs", 5, 2)]
		[InlineData("cs", 0, 2)]
		public void ThePluralForm_FollowsTheRuleOfTheLanguage(String language, Int32 count, Int32 expected)
		{
			Assert.Equal(expected, Loc.PluralIndex(language, count));
		}

		[Fact]
		public void TheWindow_Loads_AndShowsTheFilesTheStepperAndTheTexts()
		{
			Exception? failure = null;
			Thread thread = new(() =>
			{
				try
				{
					MsgIssuesViewModel issues = Issues(new ManualUiThread());
					MsgIssuesWindow window = new(issues);
					window.Show();
					try
					{
						System.Windows.Controls.ListBox files = (System.Windows.Controls.ListBox)window.FindName("FilesList");
						Assert.Equal(2, files.Items.Count);
						Assert.Same(issues.Files[0], files.SelectedItem);
						Assert.True(((System.Windows.Controls.Primitives.ToggleButton)window.FindName("MediumBox")).IsChecked);
						Assert.Equal("1 / 2", ((System.Windows.Controls.TextBlock)window.FindName("PositionLabel")).Text);
						Assert.Equal("InvalidId 0", ((System.Windows.Controls.TextBox)window.FindName("MessageBox")).Text);
						Assert.Equal("{x}{}{z}", ((System.Windows.Controls.TextBox)window.FindName("ValueBox")).Text);
						Assert.True(((System.Windows.Controls.TextBox)window.FindName("ValueBox")).IsReadOnly);
						Assert.False(((System.Windows.Controls.Button)window.FindName("PreviousButton")).IsEnabled);
						Assert.True(((System.Windows.Controls.Button)window.FindName("NextButton")).IsEnabled);

						issues.NextCommand.Execute(null);
						Assert.Equal("2 / 2", ((System.Windows.Controls.TextBlock)window.FindName("PositionLabel")).Text);
						Assert.Equal("DuplicateId 5", ((System.Windows.Controls.TextBox)window.FindName("MessageBox")).Text);

						issues.ShowMedium = false;
						Assert.Single(files.Items);
					}
					finally
					{
						window.Close();
					}
				}
				catch (Exception exception)
				{
					failure = exception;
				}
			});

			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (failure != null)
			{
				throw new InvalidOperationException("The window failed.", failure);
			}
		}

		[Fact]
		public void AReport_KeepsTheProblemsOfTheKindsThatAreNotTurnedOff()
		{
			MsgFileReport report = new("a.msg", [
				new MsgIssue(MsgIssueSeverity.Medium, MsgIssueKind.NoFinalEmptyLine, 9, 4, 0, 0, "x"),
				new MsgIssue(MsgIssueSeverity.High, MsgIssueKind.DuplicateId, 7, 1, 5, 2, "y"),
				new MsgIssue(MsgIssueSeverity.Critical, MsgIssueKind.InvalidId, 3, 1, 0, 0, "z"),
			]);

			Assert.Same(report, report.Without([]));
			Assert.Same(report, report.Without([MsgIssueKind.WordTooLong]));
			Assert.Equal([MsgIssueKind.NoFinalEmptyLine, MsgIssueKind.InvalidId], report.Without([MsgIssueKind.DuplicateId]).Issues.Select(issue => issue.Kind));
			Assert.False(report.Without(Enum.GetValues<MsgIssueKind>()).HasIssues);
		}

		[Fact]
		public void TheViewer_OnlyMentionsTheProblemsThatAreLookedFor()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] minor = Encoding.ASCII.GetBytes("{1}{}{a}");

			viewer.Show(minor, minor.Length, ".msg");
			Assert.NotEqual(String.Empty, viewer.Notice);

			viewer.DisabledMessageChecks = [MsgIssueKind.NoFinalEmptyLine];
			viewer.Show(minor, minor.Length, ".msg");
			Assert.Equal(String.Empty, viewer.Notice);
		}

		[Fact]
		public void TheButtonOfTheProblems_IsThereForAMessageFileOnly_AndGivesItsReport()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			List<MsgFileReport> asked = new();
			viewer.ProblemsRequested = asked.Add;
			viewer.SourcePath = "text\\game\\a.msg";
			Byte[] broken = Encoding.ASCII.GetBytes("{x}{}{a}\n");
			List<String?> changes = new();
			viewer.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

			Assert.False(viewer.CanShowProblems);
			Assert.False(viewer.ShowProblemsCommand.CanExecute(null));

			viewer.Show(broken, broken.Length, ".msg");
			Assert.Contains(nameof(TextViewerViewModel.CanShowProblems), changes);
			Assert.True(viewer.CanShowProblems);
			viewer.ShowProblemsCommand.Execute(null);
			MsgFileReport report = Assert.Single(asked);
			Assert.Equal("text\\game\\a.msg", report.Path);
			Assert.Equal(MsgIssueKind.InvalidId, Assert.Single(report.Issues).Kind);

			viewer.Show(broken, broken.Length, ".txt");
			Assert.False(viewer.CanShowProblems);

			// Nobody to open the window: no button.
			viewer.ProblemsRequested = null;
			viewer.Show(broken, broken.Length, ".msg");
			Assert.False(viewer.CanShowProblems);
		}

		[Fact]
		public void TheSettingsDialog_HasABoxForEveryKindOfProblem_ByTheSeverity()
		{
			SettingsData data = new() { DisabledMessageChecks = [MsgIssueKind.WordTooLong] };
			SettingsDialogViewModel model = new(data);

			Assert.Equal(3, model.MessageChecks.Count);
			Assert.Equal(Enum.GetValues<MsgIssueKind>().Length, model.MessageChecks.Sum(group => group.Items.Count));
			Assert.All(model.MessageChecks.SelectMany(group => group.Items), option => Assert.Equal(option.Kind != MsgIssueKind.WordTooLong, option.IsEnabled));
			Assert.All(model.MessageChecks[0].Items, option => Assert.Equal(MsgIssueSeverity.Critical, MsgIssue.SeverityOf(option.Kind)));
			Assert.Equal([MsgIssueKind.WordTooLong], model.DisabledMessageChecks());

			model.MessageChecks.SelectMany(group => group.Items).Single(option => option.Kind == MsgIssueKind.WordTooLong).IsEnabled = true;
			Assert.Empty(model.DisabledMessageChecks());
		}

		[Fact]
		public void TheViewer_SaysThatAMessageFileHasProblems_ButNotWhenItIsRight()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] broken = Encoding.ASCII.GetBytes("{x}{}{a}\n");
			Byte[] fine = Encoding.ASCII.GetBytes("{1}{}{a}\n");

			viewer.Show(broken, broken.Length, ".msg");
			Assert.Equal("Str.MsgCountsFormat", viewer.Notice);

			viewer.Show(fine, fine.Length, ".msg");
			Assert.Equal(String.Empty, viewer.Notice);

			// Another kind of text is not checked.
			viewer.Show(broken, broken.Length, ".txt");
			Assert.Equal(String.Empty, viewer.Notice);
		}

		[Fact]
		public void EveryKindOfProblem_HasATextInEnglish()
		{
			String english = SourceFiles.Read("Resources", "Strings.en.xaml");

			foreach (MsgIssueKind kind in Enum.GetValues<MsgIssueKind>())
			{
				Assert.Contains("Str.MsgIssue" + kind + "\"", english, StringComparison.Ordinal);
			}

			foreach (MsgIssueSeverity severity in Enum.GetValues<MsgIssueSeverity>())
			{
				Assert.Contains("Str.MsgSeverity" + severity + "\"", english, StringComparison.Ordinal);
			}

		}
	}
}