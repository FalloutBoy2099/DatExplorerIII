namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Xml.Linq;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>
	/// The logic around the windows: file policy, import planning, conflicts, window placement, temporary files,
	/// and the completeness of the translated texts.
	/// </summary>
	public sealed class UiLogicTests : IDisposable
	{
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

		private readonly String _folder;

		public UiLogicTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		private sealed class ScriptedDialogs : IDialogService
		{
			private readonly Queue<String?> _answers;

			public ScriptedDialogs(params String?[] answers)
			{
				_answers = new Queue<String?>(answers);
			}

			public Int32 Asked { get; private set; }

			public String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons)
			{
				Asked++;
				return _answers.Dequeue();
			}

			public Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question) => throw new NotSupportedException();

			public void ShowMessage(String title, String message, DialogIcon icon) => throw new NotSupportedException();

			public void ShowError(String message) => throw new NotSupportedException();

			public String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate) => throw new NotSupportedException();

			public IReadOnlyList<String> PickDatFiles(String? initialFolder) => throw new NotSupportedException();

			public String? PickNewDatFile(DatFormat format, String? initialFolder) => throw new NotSupportedException();

			public SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat) => throw new NotSupportedException();

			public ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions) => throw new NotSupportedException();

			public IReadOnlyList<String> PickFiles(String? initialFolder) => throw new NotSupportedException();

			public String? PickFolder(String title, String? initialFolder) => throw new NotSupportedException();

			public void ShowSearchResults(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo) => throw new NotSupportedException();

			public void ShowViewer(ViewerViewModel viewer) => throw new NotSupportedException();

			public Boolean ShowSettings(SettingsDialogViewModel settings) => throw new NotSupportedException();

			public void ShowMessageIssues(MsgIssuesViewModel issues) => throw new NotSupportedException();

			public void ShowAbout() => throw new NotSupportedException();
		}

		#region File policy

		[Theory]
		[InlineData("setup.exe", true)]
		[InlineData("RUN.BAT", true)]
		[InlineData("script.ps1", true)]
		[InlineData("link.url", true)]
		[InlineData("macro.docm", true)]
		[InlineData("hero.frm", false)]
		[InlineData("text.msg", false)]
		[InlineData("sound.acm", false)]
		[InlineData("noextension", false)]
		public void FilePolicy_BlocksProgramsAndScripts(String name, Boolean dangerous)
		{
			Assert.Equal(dangerous, FilePolicy.IsDangerous(name));
		}

		#endregion

		#region Conflicts

		[Fact]
		public void ConflictResolver_RemembersTheAnswerGivenToAll()
		{
			ScriptedDialogs dialogs = new("yes", "yesAll");
			ConflictResolver resolver = new(dialogs, "test");

			Assert.Equal(ConflictDecision.Replace, resolver.Resolve("a"));
			Assert.Equal(ConflictDecision.Replace, resolver.Resolve("b"));
			Assert.Equal(ConflictDecision.Replace, resolver.Resolve("c"));
			Assert.Equal(ConflictDecision.Replace, resolver.Resolve("d"));
			Assert.Equal(2, dialogs.Asked);
		}

		[Fact]
		public void ConflictResolver_MapsEveryAnswer()
		{
			ScriptedDialogs dialogs = new("no", "noAll");
			ConflictResolver resolver = new(dialogs, "test");
			Assert.Equal(ConflictDecision.Skip, resolver.Resolve("a"));
			Assert.Equal(ConflictDecision.Skip, resolver.Resolve("b"));
			Assert.Equal(ConflictDecision.Skip, resolver.Resolve("c"));

			Assert.Equal(ConflictDecision.Cancel, new ConflictResolver(new ScriptedDialogs("cancel"), "test").Resolve("a"));
			Assert.Equal(ConflictDecision.Cancel, new ConflictResolver(new ScriptedDialogs((String?)null), "test").Resolve("a"));
		}

		#endregion

		#region Import

		private String MakeFiles(params String[] relativePaths)
		{
			String root = Path.Combine(_folder, "import");
			foreach (String relative in relativePaths)
			{
				String path = Path.Combine(root, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllText(path, relative);
			}

			return root;
		}

		[Fact]
		public void Import_OfAFolder_KeepsItsNameUnlessItsContentIsMerged()
		{
			String root = MakeFiles(@"mods\art\a.frm", @"mods\b.txt");
			String mods = Path.Combine(root, "mods");

			List<ImportItem> kept = ImportPlanner.Collect([mods], false, CancellationToken.None);
			Assert.Contains(kept, item => item.RelativeDirectory == "mods" && item.SourceFile.EndsWith("b.txt"));
			Assert.Contains(kept, item => item.RelativeDirectory == @"mods\art" && item.SourceFile.EndsWith("a.frm"));

			List<ImportItem> merged = ImportPlanner.Collect([mods], true, CancellationToken.None);
			Assert.Contains(merged, item => item.RelativeDirectory == String.Empty && item.SourceFile.EndsWith("b.txt"));
			Assert.Contains(merged, item => item.RelativeDirectory == "art" && item.SourceFile.EndsWith("a.frm"));
		}

		[Fact]
		public void Import_OfLooseFiles_GoesToTheTargetFolder()
		{
			String root = MakeFiles("one.txt");
			List<ImportItem> items = ImportPlanner.Collect([Path.Combine(root, "one.txt"), Path.Combine(root, "missing.txt")], false, CancellationToken.None);
			ImportItem single = Assert.Single(items);
			Assert.Equal(String.Empty, single.RelativeDirectory);
		}

		[Fact]
		public void Import_Plan_AsksOnlyForTheFilesThatExist()
		{
			String root = MakeFiles("new.txt", "old.txt", "older.txt");
			using DatArchive archive = DatArchive.Create(Path.Combine(_folder, "target.dat"), DatFormat.Fallout2);
			archive.AddFile(Path.Combine(root, "old.txt"), "data\\", "old.txt");
			archive.AddFile(Path.Combine(root, "older.txt"), "data\\", "older.txt");

			List<ImportItem> items = ImportPlanner.Collect([root], true, CancellationToken.None);
			ScriptedDialogs dialogs = new("yes", "no");

			List<ImportStep>? plan = ImportPlanner.Plan(archive, "data\\", items.Select(item => item with { RelativeDirectory = String.Empty }).ToList(), new ConflictResolver(dialogs, "test"), out List<DatEntry> replaced);

			Assert.NotNull(plan);
			Assert.Equal(2, dialogs.Asked);
			Assert.Equal(2, plan!.Count);
			Assert.Single(replaced);
			Assert.Contains(plan, step => step.Name == "new.txt" && step.Directory == "data\\");
		}

		[Fact]
		public void Import_Plan_CanBeCancelled()
		{
			String root = MakeFiles("old.txt");
			using DatArchive archive = DatArchive.Create(Path.Combine(_folder, "target.dat"), DatFormat.Fallout2);
			archive.AddFile(Path.Combine(root, "old.txt"), String.Empty, "old.txt");

			List<ImportItem> items = ImportPlanner.Collect([root], true, CancellationToken.None);
			Assert.Null(ImportPlanner.Plan(archive, String.Empty, items, new ConflictResolver(new ScriptedDialogs("cancel"), "test"), out _));
		}

		#endregion

		#region Window placement, temporary files

		[Fact]
		public void WindowPlacement_IsBroughtBackOnScreen()
		{
			WindowPlacement offScreen = new(5000, -300, 1200, 900, false);
			WindowPlacement fitted = offScreen.FitInto(0, 0, 1920, 1080);

			Assert.Equal(1200, fitted.Width);
			Assert.Equal(720, fitted.Left);
			Assert.Equal(0, fitted.Top);

			WindowPlacement tooBig = new WindowPlacement(10, 10, 4000, 3000, true).FitInto(0, 0, 1920, 1080);
			Assert.Equal(1920, tooBig.Width);
			Assert.Equal(1080, tooBig.Height);
			Assert.True(tooBig.IsMaximized);

			WindowPlacement inside = new(100, 50, 800, 600, false);
			Assert.Equal(inside, inside.FitInto(0, 0, 1920, 1080));
		}

		[Fact]
		public void TempFolder_KeepsASessionThatIsRunning_AndRemovesOneThatIsOver()
		{
			TempFolder running = new();
			String running1 = running.Create("test");
			TempFolder over = new();
			String over1 = over.Create("test");
			String overSession = Path.GetDirectoryName(Path.GetDirectoryName(over1))!;

			// Simulates a crash: the lock of the second session is released but its folder stays.
			System.Reflection.FieldInfo lockField = typeof(TempFolder).GetField("_lock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
			((IDisposable)lockField.GetValue(over)!).Dispose();

			TempFolder.CleanStaleSessions();

			Assert.True(Directory.Exists(running1));
			Assert.False(Directory.Exists(overSession));

			running.Cleanup();
			Assert.False(Directory.Exists(running1));
		}

		#endregion

		#region Translations

		private static String? FindSolutionFolder()
		{
			for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
			{
				if (File.Exists(Path.Combine(directory.FullName, "DatExplorerIII.sln")))
				{
					return directory.FullName;
				}
			}

			return null;
		}

		private static HashSet<String> EnglishKeys()
		{
			XDocument document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Strings", "Strings.en.xaml"));
			return document.Root!.Elements().Select(element => (String)element.Attribute(XamlNamespace + "Key")!).ToHashSet();
		}

		[Fact]
		public void EveryErrorAndIssueHasATranslatedText()
		{
			HashSet<String> keys = EnglishKeys();

			foreach (DatErrorKind kind in Enum.GetValues<DatErrorKind>().Where(kind => kind != DatErrorKind.Unknown))
			{
				Assert.Contains("Str.Err." + kind, keys);
			}

			foreach (DatIssueKind kind in Enum.GetValues<DatIssueKind>())
			{
				Assert.Contains("Str.Issue." + kind, keys);
			}

			foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
			{
				Assert.Contains("Str.Theme" + preference, keys);
			}
		}

		[Fact]
		public void RecentFiles_KeepTheLastTenOpened_MostRecentFirst()
		{
			List<String> list = new();
			for (Int32 i = 1; i <= 12; i++)
			{
				RecentFiles.Add(list, @"C:\dat\file" + i + ".dat");
			}

			Assert.Equal(10, list.Count);
			Assert.Equal(@"C:\dat\file12.dat", list[0]);
			Assert.Equal(@"C:\dat\file3.dat", list[9]);

			// A file opened again goes back to the top, even when it is written with other upper and lower case letters.
			RecentFiles.Add(list, @"c:\DAT\FILE5.dat");
			Assert.Equal(10, list.Count);
			Assert.Equal(@"c:\DAT\FILE5.dat", list[0]);
			Assert.Single(list, path => path.Contains("5.dat", StringComparison.OrdinalIgnoreCase) && path.Contains("file5", StringComparison.OrdinalIgnoreCase));

			RecentFiles.Remove(list, @"C:\dat\FILE12.dat");
			Assert.Equal(9, list.Count);
			Assert.DoesNotContain(list, path => path.EndsWith("file12.dat", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void RecentFiles_LongPathsAreShortenedForTheMenu()
		{
			Assert.Equal(@"C:\Games\master.dat", RecentFiles.Shorten(@"C:\Games\master.dat"));

			String longPath = @"D:\FalloutProjects\FalloutSFall\Fallout2\Backups\Before the patch\master.dat";
			String shortened = RecentFiles.Shorten(longPath, 40);
			Assert.Equal("D:\\\u2026\\Before the patch\\master.dat", shortened);

			Assert.Equal("D:\\\u2026\\master.dat", RecentFiles.Shorten(longPath, 20));
			Assert.True(RecentFiles.Shorten(@"C:\" + new String('x', 100) + ".dat", 30).Length <= 30);
		}

		[Fact]
		public void RecentFiles_AreKeptInTheSettings()
		{
			SettingsData data = new();
			RecentFiles.Add(data.RecentFiles, @"C:\dat\a.dat");
			RecentFiles.Add(data.RecentFiles, @"C:\dat\b.dat");

			SettingsData back = System.Text.Json.JsonSerializer.Deserialize<SettingsData>(System.Text.Json.JsonSerializer.Serialize(data))!;
			Assert.Equal(new[] { @"C:\dat\b.dat", @"C:\dat\a.dat" }, back.RecentFiles);

			// Settings written before the list existed have none.
			Assert.Empty(System.Text.Json.JsonSerializer.Deserialize<SettingsData>("{}")!.RecentFiles);
		}

		[Theory]
		[InlineData(0L, "0 B")]
		[InlineData(532L, "532 B")]
		[InlineData(1024L, "1 KB")]
		[InlineData(1536L, "1.5 KB")]
		[InlineData(10L * 1024, "10 KB")]
		[InlineData(12800L, "12.5 KB")]
		[InlineData(200L * 1024, "200 KB")]
		[InlineData(1000L * 1024, "0.98 MB")]
		[InlineData(1024L * 1024, "1 MB")]
		[InlineData(52_391_374L, "50 MB")]
		[InlineData(43_342_010L, "41.3 MB")]
		[InlineData(11L * 1024 * 1024 * 1024 / 2, "5.5 GB")]
		[InlineData(3L * 1024 * 1024 * 1024 * 1024, "3 TB")]
		public void Sizes_AreWrittenForPeople(Int64 bytes, String expected)
		{
			String[] units = ["B", "KB", "MB", "GB", "TB"];
			Assert.Equal(expected, SizeFormatter.Format(bytes, units, System.Globalization.CultureInfo.GetCultureInfo("en-US")));
		}

		[Fact]
		public void Sizes_FollowTheUnitsAndTheCultureOfTheLanguage()
		{
			String[] french = ["o", "Ko", "Mo", "Go", "To"];
			System.Globalization.CultureInfo fr = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

			Assert.Equal("532 o", SizeFormatter.Format(532, french, fr));
			Assert.Equal("1,5 Ko", SizeFormatter.Format(1536, french, fr));
			Assert.Equal("41,3 Mo", SizeFormatter.Format(43_342_010, french, fr));
			Assert.Equal("2 Go", SizeFormatter.Format(2L * 1024 * 1024 * 1024, french, fr));
		}

		[Fact]
		public void EveryLanguageNamesTheUnitsOfSizes()
		{
			HashSet<String> keys = EnglishKeys();
			Assert.All(["Str.UnitByte", "Str.UnitKilo", "Str.UnitMega", "Str.UnitGiga", "Str.UnitTera"], key => Assert.Contains(key, keys));
		}

		[Theory]
		[InlineData("master.dat", true)]
		[InlineData("ARCANUM.DAT", true)]
		[InlineData("Arcanum.PATCH0", true)]
		[InlineData("Arcanum.patch9", true)]
		[InlineData(@"C:\Games\UAP\Modules\Time.PATCH9", true)]
		[InlineData("Arcanum.patch10", false)]
		[InlineData("Arcanum.patcha", false)]
		[InlineData("Arcanum.patch", false)]
		[InlineData("readme.txt", false)]
		[InlineData("dat", false)]
		public void DatFiles_AreRecognizedByTheirExtension(String path, Boolean expected)
		{
			Assert.Equal(expected, DatFiles.IsDatFile(path));
		}

		[Fact]
		public void TheAssociationOfThePatches_CoversPatch0ToPatch9_AndOnlyThoseOfAnArchive()
		{
			Assert.Equal(10, FileAssociationService.PatchExtensions.Count);
			Assert.Equal(".patch0", FileAssociationService.PatchExtensions[0]);
			Assert.Equal(".patch9", FileAssociationService.PatchExtensions[9]);
			Assert.All(FileAssociationService.PatchExtensions, extension => Assert.True(DatFiles.IsDatFile("module" + extension)));
		}

		[Fact]
		public void TheFilterOfTheDialogs_ListsThePatchesOfArcanum()
		{
			String filter = XDocument.Load(Path.Combine(FindSolutionFolder() ?? String.Empty, "DatExplorerIII", "Resources", "Strings.en.xaml"))
				.Descendants().First(element => element.Attribute(XamlNamespace + "Key")?.Value == "Str.DatFilter").Value;

			Assert.Contains("*.dat;*.patch0;", filter);
			Assert.Contains("*.patch9|", filter);
		}

		[Fact]
		public void TheNamesOfTheCharacterSheetExist()
		{
			HashSet<String> keys = EnglishKeys();
			IEnumerable<String> needed = Enumerable.Range(0, 17).Concat([31, 32]).Select(index => "Str.Stat" + index)
				.Concat(Enumerable.Range(0, 7).Select(index => "Str.Damage" + index))
				.Concat(Enumerable.Range(0, 18).Select(index => "Str.Skill" + index))
				.Concat(Enumerable.Range(0, 16).Select(index => "Str.Trait" + index))
				.Concat(Enumerable.Range(0, 6).Select(index => "Str.ProType" + index))
				.Concat(Enumerable.Range(0, 7).Select(index => "Str.ProItem" + index))
				.Concat(Enumerable.Range(0, 6).Select(index => "Str.ProScenery" + index))
				.Concat(Enumerable.Range(0, 8).Select(index => "Str.ProMaterial" + index))
				.Concat(Enumerable.Range(0, 3).Select(index => "Str.ProBody" + index))
				.Concat(Enumerable.Range(0, 18).Select(index => "Str.ArcProType" + index))
				.Concat(["Str.ArcProNumber", "Str.ArcProVersion", "Str.ArcProFields", "Str.ArcProIncomplete", "Str.ClearRecent"])
				.Concat(Enumerable.Range(0, 5).Select(index => "Str.MapScriptType" + index))
				.Concat(Enumerable.Range(0, 3).Select(index => "Str.MapExitKind" + index))
				.Concat(Enumerable.Range(0, 4).Select(index => "Str.CompareState" + index));

			Assert.All(needed, key => Assert.Contains(key, keys));
		}

		[Fact]
		public void EveryTextUsedByTheProgramExists()
		{
			String? solution = FindSolutionFolder();
			if (solution == null)
			{
				return;
			}

			HashSet<String> keys = EnglishKeys();
			HashSet<String> prefixes = new() { "Str.Err", "Str.Issue", "Str.Theme", "Str.Stat", "Str.Damage", "Str.Skill", "Str.Trait", "Str.ProItem", "Str.ProScenery", "Str.ProBody", "Str.ArcProType", "Str.MapScriptType", "Str.MapExitKind", "Str.CompareState", "Str.MsgIssue", "Str.MsgSeverity", "Str.MsgCheck" };
			Regex usage = new(@"Str\.[A-Za-z0-9]+(\.[A-Za-z0-9]+)?", RegexOptions.Compiled);
			List<String> missing = new();

			String project = Path.Combine(solution, "DatExplorerIII");
			IEnumerable<String> sources = Directory.EnumerateFiles(project, "*.*", SearchOption.AllDirectories)
				.Where(path => (path.EndsWith(".cs") || path.EndsWith(".xaml")) && !path.Contains(@"\obj\") && !path.Contains(@"\bin\") && !Path.GetFileName(path).StartsWith("Strings."));

			foreach (String path in sources)
			{
				foreach (Match match in usage.Matches(File.ReadAllText(path)))
				{
					String key = match.Value;
					if (!keys.Contains(key) && !prefixes.Contains(key) && !prefixes.Any(prefix => key.StartsWith(prefix + ".")))
					{
						missing.Add($"{key} ({Path.GetFileName(path)})");
					}
				}
			}

			Assert.Empty(missing.Distinct());
		}

		[Fact]
		public void ErrorText_FallsBackToTheEnglishMessage_WhenNoTranslationIsLoaded()
		{
			DatException error = new(DatErrorKind.NotADat, "english text");
			Assert.Equal("english text", ErrorText.Describe(error));
			Assert.Equal("plain", ErrorText.Describe(new InvalidOperationException("plain")));
		}

		#endregion
	}
}