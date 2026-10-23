namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models.Archives;
	using DatExplorerIII.Models.Comparison;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Interface;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Main;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>
	/// A folder opened like a DAT archive (a folder named master.dat that holds the files of a DAT), and archives saved under
	/// another name.
	/// </summary>
	public sealed class FolderArchiveTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		public FolderArchiveTests()
		{
			Directory.CreateDirectory(_root);
		}

		private readonly List<MainViewModel> _viewModels = [];

		public void Dispose()
		{
			// The archives that are open keep their files.
			foreach (FolderNode root in _viewModels.SelectMany(viewModel => viewModel.Roots))
			{
				root.Document.Archive.Dispose();
			}

			Directory.Delete(_root, true);
		}

		private static readonly Byte[] ArtData = Enumerable.Range(0, 3000).Select(index => (Byte)(index % 7)).ToArray();

		/// <summary>A folder like the content of a master.dat: files in sub folders, an empty folder, a file at the root.</summary>
		private String MakeFolder(String name = "master.dat")
		{
			String folder = Path.Combine(_root, name);
			Directory.CreateDirectory(Path.Combine(folder, "art", "critters"));
			Directory.CreateDirectory(Path.Combine(folder, "text", "english", "game"));
			Directory.CreateDirectory(Path.Combine(folder, "empty"));
			File.WriteAllBytes(Path.Combine(folder, "art", "critters", "hero.frm"), ArtData);
			File.WriteAllText(Path.Combine(folder, "text", "english", "game", "combat.msg"), "{100}{}{Hello}\r\n");
			File.WriteAllText(Path.Combine(folder, "readme.txt"), "root file");
			return folder;
		}

		private static Byte[] Hash(String path) => System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));

		#region Core

		[Fact]
		public void OpenFolder_ListsTheFilesAndTheFoldersOfTheFolder()
		{
			String folder = MakeFolder();
			using DatArchive archive = DatArchive.OpenFolder(folder);

			Assert.True(archive.IsFolderBacked);
			Assert.Equal(DatFormat.Fallout2, archive.Format);
			Assert.Equal(folder, archive.FilePath);
			Assert.Equal(3, archive.Count);
			Assert.Empty(archive.SkippedFiles);
			Assert.False(archive.HasBackingFile);

			Assert.True(archive.FolderExists("art\\critters"));
			Assert.True(archive.FolderExists("empty"));
			Assert.Equal(ArtData, archive.ReadData(archive.Find("art\\critters\\hero.frm")!));
			Assert.Equal("root file", Encoding.ASCII.GetString(archive.ReadData(archive.Find("readme.txt")!)));
			Assert.Equal(["combat.msg"], archive.GetEntries("text\\english\\game").Select(entry => entry.Name));
		}

		[Fact]
		public void OpenFolder_IsNotAChangeToSave_UntilSomethingIsChanged()
		{
			using DatArchive archive = DatArchive.OpenFolder(MakeFolder());
			Assert.Equal(DatPendingChanges.None, archive.PendingChanges);

			archive.Rename(archive.Find("readme.txt")!, "notes.txt");
			Assert.NotEqual(DatPendingChanges.None, archive.PendingChanges);

			using DatArchive other = DatArchive.OpenFolder(MakeFolder("other"));
			other.Remove([other.Find("readme.txt")!]);
			Assert.NotEqual(DatPendingChanges.None, other.PendingChanges);

			using DatArchive third = DatArchive.OpenFolder(MakeFolder("third"));
			third.AddFolder("new");
			Assert.NotEqual(DatPendingChanges.None, third.PendingChanges);
		}

		[Fact]
		public void OpenFolder_LeavesOutWhatADatCannotStore_AndSaysSo()
		{
			String folder = MakeFolder();
			File.WriteAllText(Path.Combine(folder, "price€.txt"), "euro sign cannot be stored");
			Directory.CreateDirectory(Path.Combine(folder, "caf€"));
			File.WriteAllText(Path.Combine(folder, "caf€", "inside.txt"), "x");

			using DatArchive archive = DatArchive.OpenFolder(folder);

			Assert.Equal(3, archive.Count);
			Assert.Contains("price€.txt", archive.SkippedFiles);
			Assert.Contains("caf€\\", archive.SkippedFiles);
			Assert.Null(archive.Find("caf€\\inside.txt"));
		}

		[Fact]
		public void OpenFolder_RefusesAFolderThatDoesNotExist()
		{
			DatException error = Assert.Throws<DatException>(() => DatArchive.OpenFolder(Path.Combine(_root, "missing")));
			Assert.Equal(DatErrorKind.FileNotFound, error.Kind);
			Assert.Throws<DatException>(() => DatArchive.OpenFolder(String.Empty));
		}

		[Theory]
		[InlineData(DatFormat.Fallout2)]
		[InlineData(DatFormat.Fallout1)]
		[InlineData(DatFormat.Arcanum)]
		public void SaveAs_MakesADatFileFromAFolder_AndLeavesTheFolderAlone(DatFormat format)
		{
			String folder = MakeFolder();
			String target = Path.Combine(_root, "master_new.dat");

			using (DatArchive archive = DatArchive.OpenFolder(folder))
			{
				archive.SaveAs(target, format);

				Assert.False(archive.IsFolderBacked);
				Assert.True(archive.HasBackingFile);
				Assert.Equal(target, archive.FilePath);
				Assert.Equal(format, archive.Format);
				Assert.Equal(DatPendingChanges.None, archive.PendingChanges);

				// The archive reads from the new file now.
				Assert.Equal(ArtData, archive.ReadData(archive.Find("art\\critters\\hero.frm")!));
				Assert.All(archive.Entries, entry => Assert.False(entry.IsNew));
			}

			// The folder is as it was.
			Assert.True(Directory.Exists(folder));
			Assert.Equal(3, Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Count());

			using DatArchive reopened = DatArchive.Open(target);
			Assert.Equal(format, reopened.Format);
			Assert.Equal(3, reopened.Count);
			Assert.Equal(0, reopened.DamagedCount);
			Assert.Empty(reopened.Issues);
			Assert.Equal(ArtData, reopened.ReadData(reopened.Find("art\\critters\\hero.frm")!));
			Assert.Equal("{100}{}{Hello}\r\n", Encoding.ASCII.GetString(reopened.ReadData(reopened.Find("text\\english\\game\\combat.msg")!)));

			// Arcanum keeps the empty folders.
			Assert.Equal(format == DatFormat.Arcanum, reopened.FolderExists("empty"));
		}

		[Fact]
		public void SaveAs_AnArchiveOfAFile_LeavesTheOriginalFileAndContinuesInTheNewOne()
		{
			String first = Path.Combine(_root, "a.dat");
			using (DatArchive created = DatArchive.Create(first, DatFormat.Fallout2))
			{
				String source = Path.Combine(_root, "source.txt");
				File.WriteAllText(source, "some content that is repeated repeated repeated repeated");
				created.AddFile(source, "data");
				created.Save();
			}

			Byte[] before = Hash(first);
			String second = Path.Combine(_root, "b.dat");

			using (DatArchive archive = DatArchive.Open(first))
			{
				archive.SaveAs(second);
				Assert.Equal(second, archive.FilePath);
				Assert.Equal(DatFormat.Fallout2, archive.Format);
				Assert.Equal("some content that is repeated repeated repeated repeated", Encoding.ASCII.GetString(archive.ReadData(archive.Find("data\\source.txt")!)));

				// Later changes go to the new file.
				String extra = Path.Combine(_root, "extra.txt");
				File.WriteAllText(extra, "more");
				archive.AddFile(extra, "data");
				archive.Save();
			}

			Assert.Equal(before, Hash(first));
			using DatArchive original = DatArchive.Open(first);
			using DatArchive copy = DatArchive.Open(second);
			Assert.Equal(1, original.Count);
			Assert.Equal(2, copy.Count);
		}

		[Fact]
		public void SaveAs_RefusesAFolder_AndKeepsTheArchiveAsItWas()
		{
			String folder = MakeFolder();
			using DatArchive archive = DatArchive.OpenFolder(folder);

			DatException error = Assert.Throws<DatException>(() => archive.SaveAs(Path.Combine(_root, "master.dat")));
			Assert.Equal(DatErrorKind.Io, error.Kind);
			Assert.True(archive.IsFolderBacked);
			Assert.Equal(folder, archive.FilePath);
		}

		[Fact]
		public void SaveAs_WhenItFails_TheArchiveStaysWhatItWas()
		{
			String first = Path.Combine(_root, "a.dat");
			using (DatArchive created = DatArchive.Create(first, DatFormat.Fallout2))
			{
				String source = Path.Combine(_root, "source.txt");
				File.WriteAllText(source, "content");
				created.AddFile(source, "data");
				created.Save();
			}

			using DatArchive archive = DatArchive.Open(first);
			Assert.ThrowsAny<IOException>(() => archive.SaveAs(Path.Combine(_root, "no such folder", "b.dat")));

			Assert.Equal(first, archive.FilePath);
			Assert.True(archive.HasBackingFile);
			Assert.Equal("content", Encoding.ASCII.GetString(archive.ReadData(archive.Find("data\\source.txt")!)));

			// A folder archive that cannot be written stays a folder archive.
			using DatArchive fromFolder = DatArchive.OpenFolder(MakeFolder());
			Assert.ThrowsAny<IOException>(() => fromFolder.SaveAs(Path.Combine(_root, "no such folder", "b.dat")));
			Assert.True(fromFolder.IsFolderBacked);
			Assert.Equal(DatFormat.Fallout2, fromFolder.Format);
			Assert.Equal(ArtData, fromFolder.ReadData(fromFolder.Find("art\\critters\\hero.frm")!));
		}

		[Fact]
		public void SaveAs_TheFormatOfStoredFilesCannotChange_AndAFolderArchiveCannotBeSavedInPlace()
		{
			String first = Path.Combine(_root, "a.dat");
			using (DatArchive created = DatArchive.Create(first, DatFormat.Fallout2))
			{
				String source = Path.Combine(_root, "source.txt");
				File.WriteAllText(source, "content");
				created.AddFile(source, "data");
				created.Save();
			}

			using DatArchive archive = DatArchive.Open(first);
			Assert.Throws<InvalidOperationException>(() => archive.SaveAs(Path.Combine(_root, "c.dat"), DatFormat.Arcanum));

			using DatArchive fromFolder = DatArchive.OpenFolder(MakeFolder());
			fromFolder.AddFolder("changed");
			Assert.Throws<InvalidOperationException>(() => fromFolder.Save());
		}

		#endregion

		#region View model

		private sealed class FakeDialogs : IDialogService
		{
			public SaveAsChoice? Choice { get; set; }

			public List<String> Errors { get; } = [];

			public List<String> Messages { get; } = [];

			public Int32 SaveAsAsked { get; private set; }

			public Boolean? ChooseFormatAsked { get; private set; }

			public String? SuggestedName { get; private set; }

			public String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons) => null;

			public Boolean Answer { get; set; } = true;

			public Int32 Confirmations { get; private set; }

			public Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question)
			{
				Confirmations++;
				return Answer;
			}

			public void ShowMessage(String title, String message, DialogIcon icon) => Messages.Add(message);

			public void ShowError(String message) => Errors.Add(message);

			public String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate) => null;

			public IReadOnlyList<String> PickDatFiles(String? initialFolder) => [];

			public String? PickNewDatFile(DatFormat format, String? initialFolder) => null;

			public SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat)
			{
				SaveAsAsked++;
				SuggestedName = suggestedName;
				ChooseFormatAsked = chooseFormat;
				return Choice;
			}

			public ComparisonSources? ComparisonChoice { get; set; }

			public ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions) => ComparisonChoice;

			public IReadOnlyList<String> PickFiles(String? initialFolder) => [];

			public String? PickFolder(String title, String? initialFolder) => null;

			public void ShowSearchResults(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
			{ }

			public Int32 ViewersShown { get; private set; }

			public void ShowViewer(ViewerViewModel viewer)
			{
				ViewersShown++;
			}

			public Func<SettingsDialogViewModel, Boolean>? SettingsScript { get; set; }

			public Boolean ShowSettings(SettingsDialogViewModel settings) => SettingsScript?.Invoke(settings) ?? false;

			/// <summary>The problems of message files that were shown.</summary>
			public List<MsgIssuesViewModel> ShownIssues { get; } = [];

			public void ShowMessageIssues(MsgIssuesViewModel issues) => ShownIssues.Add(issues);

			public void ShowAbout()
			{ }
		}

		private MainViewModel CreateViewModel(FakeDialogs dialogs)
		{
			SettingsService settings = new(Path.Combine(_root, "settings"), null);
			MainViewModel viewModel = new(dialogs, settings, new TempFolder(), new ManualUiThread(), new ScrollBarMetrics());
			_viewModels.Add(viewModel);
			return viewModel;
		}

		[Fact]
		public async Task ViewModel_OpensAFolderLikeAnArchive_AndSavesItUnderAnotherName()
		{
			String folder = MakeFolder();
			String target = Path.Combine(_root, "master_new.dat");
			FakeDialogs dialogs = new() { Choice = new SaveAsChoice(target, DatFormat.Fallout2) };
			MainViewModel viewModel = CreateViewModel(dialogs);

			await viewModel.OpenFilesAsync([folder]);

			FolderNode root = Assert.Single(viewModel.Roots);
			Assert.Equal("[DIR] master.dat", root.DisplayName);
			Assert.False(root.Document.ShouldSave);
			Assert.Equal([folder], viewModel.RecentFiles);

			await viewModel.SaveAsAsync();

			// The suggestion is not the name of the folder, and the dialog lets the user choose the format.
			Assert.Equal(1, dialogs.SaveAsAsked);
			Assert.Equal("master_new.dat", dialogs.SuggestedName);
			Assert.True(dialogs.ChooseFormatAsked);
			Assert.Empty(dialogs.Errors);

			Assert.Equal("[F2] master_new.dat", root.DisplayName);
			Assert.Equal(target, root.Document.FilePath);
			Assert.True(File.Exists(target));
			Assert.Equal(target, viewModel.RecentFiles[0]);
			Assert.True(Directory.Exists(folder));
		}

		private static readonly String Sep = Path.DirectorySeparatorChar.ToString();

		private static String MixedCaseFolder(String root)
		{
			String folder = Path.Combine(root, "mixed");
			foreach (String path in new[] { "Art" + Sep + "Critters" + Sep + "Hero.FRM", "Art" + Sep + "Items" + Sep + "Gun.Pro", "Data" + Sep + "Vault13.GAM", "ROOT.TXT" })
			{
				String full = Path.Combine(folder, path);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllText(full, "x");
			}

			return folder;
		}

		[Fact]
		public void Archive_LowercaseTree_PutsAFolderItsSubFoldersAndItsFilesInLowerCase_KeepingWhatIsAbove()
		{
			using DatArchive archive = DatArchive.OpenFolder(MixedCaseFolder(_root));

			// Only the names under the folder count: "Art" is above "Critters" and keeps its case.
			Assert.Equal(2, archive.LowercaseTree("Art" + Sep + "Critters", false));
			Assert.Equal(DatPendingChanges.None, archive.PendingChanges);
			Assert.Equal(2, archive.LowercaseTree("Art" + Sep + "Critters"));

			Assert.Contains("Art" + Sep + "critters" + Sep, archive.Folders);
			DatEntry hero = archive.Find("art" + Sep + "critters" + Sep + "hero.frm")!;
			Assert.Equal("Art" + Sep + "critters" + Sep, hero.Directory);
			Assert.Equal("hero.frm", hero.Name);
			Assert.Equal("Art" + Sep + "Items" + Sep, archive.Find("art" + Sep + "items" + Sep + "gun.pro")!.Directory);
			Assert.NotEqual(DatPendingChanges.None, archive.PendingChanges);

			// Nothing left to do: the same call changes nothing.
			Assert.Equal(0, archive.LowercaseTree("Art" + Sep + "Critters"));
		}

		[Fact]
		public void Archive_LowercaseTree_OfTheRoot_ChangesTheWholeArchive()
		{
			using DatArchive archive = DatArchive.OpenFolder(MixedCaseFolder(_root));

			Int32 count = archive.LowercaseTree(String.Empty);

			// Folders: Art, Critters, Items, Data; files: Hero.FRM, Gun.Pro, Vault13.GAM, ROOT.TXT.
			Assert.Equal(8, count);
			Assert.All(archive.Folders, folder => Assert.Equal(folder.ToLowerInvariant(), folder));
			Assert.All(archive.Entries, entry => Assert.Equal(entry.FullPath.ToLowerInvariant(), entry.FullPath));
			Assert.Equal(4, archive.Count);
		}

		[Fact]
		public async Task ViewModel_LowercasesTheSelectedFolder_AfterAsking()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MixedCaseFolder(_root)]);
			FolderNode root = Assert.Single(viewModel.Roots);
			FolderNode art = root.Children.First(child => child.Name == "Art");

			// The user says no: nothing changes.
			dialogs.Answer = false;
			viewModel.SelectFolder(art);
			viewModel.LowercaseCommand.Execute(null);
			Assert.Equal(1, dialogs.Confirmations);
			Assert.Contains(root.Document.Archive.Entries, entry => entry.Name == "Hero.FRM");

			dialogs.Answer = true;
			viewModel.LowercaseCommand.Execute(null);
			Assert.Equal(2, dialogs.Confirmations);
			Assert.DoesNotContain(root.Document.Archive.Entries.Where(entry => entry.Directory.StartsWith("Art", StringComparison.Ordinal)), entry => entry.Name != entry.Name.ToLowerInvariant());
			Assert.Contains(root.Document.Archive.Entries, entry => entry.Name == "ROOT.TXT");
			Assert.True(root.Document.ShouldSave);

			// A folder that is all in lower case now: a message, no question.
			viewModel.LowercaseCommand.Execute(null);
			Assert.Equal(2, dialogs.Confirmations);
		}

		[Fact]
		public async Task ViewModel_TheDoubleClickCanDoNothing_ButAFolderStillOpens()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolder()]);

			ListEntry file = viewModel.Items.First(item => item.Name == "readme.txt");
			ListEntry folder = viewModel.Items.First(item => item.Folder != null);

			// By default a file opens in the viewer.
			Assert.True(viewModel.IsDoubleClickViewer);
			viewModel.SetSelectedItems([file]);
			viewModel.OpenItemCommand.Execute(null);
			Assert.Equal(1, dialogs.ViewersShown);

			// "Nothing": the choice is the only one on, and the double click does not open the file.
			viewModel.SetDoubleClickCommand.Execute("None");
			Assert.True(viewModel.IsDoubleClickNothing);
			Assert.False(viewModel.IsDoubleClickViewer);
			Assert.False(viewModel.IsDoubleClickProgram);
			viewModel.OpenItemCommand.Execute(null);
			Assert.Equal(1, dialogs.ViewersShown);

			// A folder is still entered.
			viewModel.SetSelectedItems([folder]);
			viewModel.OpenItemCommand.Execute(null);
			Assert.Same(folder.Folder, viewModel.SelectedFolder);

			// Back to the viewer: it is the choice that was made before.
			viewModel.SetDoubleClickCommand.Execute("Viewer");
			Assert.True(viewModel.IsDoubleClickViewer);
			Assert.False(viewModel.IsDoubleClickNothing);

			viewModel.SetDoubleClickCommand.Execute("Program");
			Assert.True(viewModel.IsDoubleClickProgram);
			Assert.False(viewModel.IsDoubleClickViewer);
		}

		[Fact]
		public async Task ViewModel_SaveOnAFolderArchive_AsksForANewName()
		{
			String target = Path.Combine(_root, "saved.dat");
			FakeDialogs dialogs = new() { Choice = new SaveAsChoice(target, DatFormat.Arcanum) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolder()]);

			viewModel.CurrentDocument!.Archive.AddFolder("changed");
			Assert.True(viewModel.CurrentDocument.ShouldSave);

			await viewModel.SaveAsync(false);

			Assert.Equal(1, dialogs.SaveAsAsked);
			Assert.Equal("[AR] saved.dat", viewModel.Roots[0].DisplayName);
			using DatArchive saved = DatArchive.Open(target);
			Assert.Equal(DatFormat.Arcanum, saved.Format);
			Assert.True(saved.FolderExists("changed"));
		}

		[Fact]
		public async Task ViewModel_SaveAs_RefusesTheNameOfAFolder_AndTheNameOfAnotherOpenArchive()
		{
			String folder = MakeFolder();
			FakeDialogs dialogs = new() { Choice = new SaveAsChoice(folder, DatFormat.Fallout2) };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([folder]);

			await viewModel.SaveAsAsync();

			Assert.Single(dialogs.Errors);
			Assert.Equal("[DIR] master.dat", viewModel.Roots[0].DisplayName);

			// Another archive that is open cannot be written over.
			String other = Path.Combine(_root, "other.dat");
			using (DatArchive created = DatArchive.Create(other, DatFormat.Fallout2))
			{
				String source = Path.Combine(_root, "source.txt");
				File.WriteAllText(source, "x");
				created.AddFile(source, "data");
				created.Save();
			}

			await viewModel.OpenFilesAsync([other]);
			viewModel.SelectFolder(viewModel.Roots[0]);
			dialogs.Choice = new SaveAsChoice(other, DatFormat.Fallout2);
			await viewModel.SaveAsAsync();

			Assert.Equal(2, dialogs.Errors.Count);
			Assert.Equal("[DIR] master.dat", viewModel.Roots[0].DisplayName);
		}

		[Fact]
		public async Task ViewModel_SaveAs_DoesNothingWhenTheUserGivesUp()
		{
			FakeDialogs dialogs = new() { Choice = null };
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolder()]);

			await viewModel.SaveAsAsync();

			Assert.Equal(1, dialogs.SaveAsAsked);
			Assert.Empty(dialogs.Errors);
			Assert.Equal("[DIR] master.dat", viewModel.Roots[0].DisplayName);
			Assert.True(viewModel.CurrentDocument!.Archive.IsFolderBacked);
		}

		[Fact]
		public async Task ViewModel_ClearsTheListOfRecentFiles()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolder()]);
			Assert.Single(viewModel.RecentFiles);

			viewModel.ClearRecentCommand.Execute(null);

			Assert.Empty(viewModel.RecentFiles);
			Assert.Empty(new SettingsService(Path.Combine(_root, "settings"), null).Data.RecentFiles);
		}

		[Fact]
		public async Task ViewModel_AZip_CannotBeChanged_ButCanBeExtracted()
		{
			String zipPath = Path.Combine(_root, "test.zip");
			using (System.IO.Compression.ZipArchive zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
			{
				using StreamWriter writer = new(zip.CreateEntry("a.txt").Open());
				writer.Write("hello");
			}

			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([zipPath]);

			Assert.True(viewModel.CurrentDocument!.Archive.IsReadOnly);
			Assert.False(viewModel.ImportFilesCommand.CanExecute(null));
			Assert.False(viewModel.ImportFolderCommand.CanExecute(null));
			Assert.False(viewModel.NewFolderCommand.CanExecute(null));
			Assert.False(viewModel.SaveAsCommand.CanExecute(null));
			Assert.False(viewModel.ImportDroppedCommand.CanExecute(new[] { zipPath }));
			Assert.True(viewModel.ExtractAllCommand.CanExecute(null));
		}

		[Fact]
		public async Task ViewModel_MovesTheSelectedDat_UpAndDown_WithinTheList()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([MakeFolder("first")]);
			await viewModel.OpenFilesAsync([MakeFolder("second")]);
			Assert.Equal(2, viewModel.Roots.Count);
			DatDocument second = viewModel.CurrentDocument!;
			Assert.Same(second.Root, viewModel.Roots[1]);

			// The last of the list cannot go down; it can go up.
			Assert.False(viewModel.MoveDatDownCommand.CanExecute(null));
			Assert.True(viewModel.MoveDatUpCommand.CanExecute(null));

			viewModel.MoveDatUpCommand.Execute(null);

			Assert.Same(second.Root, viewModel.Roots[0]);
			Assert.Same(second, viewModel.CurrentDocument);
			Assert.False(viewModel.MoveDatUpCommand.CanExecute(null));
			Assert.True(viewModel.MoveDatDownCommand.CanExecute(null));
		}

		#endregion

		#region Shortcuts and drag and drop

		[Fact]
		public void DropCommands_AcceptOnlyWhatTheyCanUse()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());

			// The tree takes archives; the list takes anything that is a file or a folder.
			Assert.False(viewModel.OpenDroppedCommand.CanExecute(null));
			Assert.False(viewModel.OpenDroppedCommand.CanExecute(new[] { "notes.txt" }));
			Assert.True(viewModel.OpenDroppedCommand.CanExecute(new[] { "notes.txt", "master.dat" }));
			Assert.True(viewModel.OpenDroppedCommand.CanExecute(new[] { "arcanum.patch3" }));

			Assert.False(viewModel.ImportDroppedCommand.CanExecute(null));
			Assert.False(viewModel.ImportDroppedCommand.CanExecute(Array.Empty<String>()));
			Assert.True(viewModel.ImportDroppedCommand.CanExecute(new[] { "notes.txt" }));
		}

		[Fact]
		public async Task DragSources_ExtractWhatIsDragged_AndRefuseWhatCannotBe()
		{
			// A folder opened like an archive can be dragged out too: its files are not in a DAT, they are copied from the folder.
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([MakeFolder()]);

			// Nothing selected: nothing to drag.
			Assert.Null(viewModel.ListDragSource.PrepareDragOut(null));

			ListEntry file = viewModel.Items.First(item => item.Name == "readme.txt");
			viewModel.SetSelectedItems([file]);
			String path = Assert.Single(viewModel.ListDragSource.PrepareDragOut(null)!);
			Assert.Equal("root file", File.ReadAllText(path));
			Assert.Equal("readme.txt", Path.GetFileName(path));

			// A folder of the tree is dragged with what it holds; the root of an archive is not dragged, nor is something that is not a folder.
			FolderNode root = Assert.Single(viewModel.Roots);
			FolderNode art = root.Document.FindNode("art")!;
			String folderPath = Assert.Single(viewModel.FolderDragSource.PrepareDragOut(art)!);
			Assert.True(File.Exists(Path.Combine(folderPath, "critters", "hero.frm")));
			Assert.Null(viewModel.FolderDragSource.PrepareDragOut(root));
			Assert.Null(viewModel.FolderDragSource.PrepareDragOut("art"));
		}

		[Fact]
		public async Task TheSortAndTheColumnWidths_AreKeptForTheNextRun()
		{
			String settingsFolder = Path.Combine(_root, "settings");
			MainViewModel first = CreateViewModel(new FakeDialogs());
			await first.OpenFilesAsync([MakeFolder()]);
			Assert.Equal(DatSortColumn.Name, first.SortColumn);
			Assert.Empty(first.ColumnWidths);

			first.SortCommand.Execute("Size");
			first.SortCommand.Execute("Size");
			first.ColumnWidths = [120, 60, 70, 100];
			first.ColumnWidths = [1, 2];
			Assert.Equal([120.0, 60.0, 70.0, 100.0], first.ColumnWidths);
			Assert.False(first.SortCommand.CanExecute("Nope"));
			first.Shutdown();

			MainViewModel second = new(new FakeDialogs(), new SettingsService(settingsFolder, null), new TempFolder(), new ManualUiThread(), new ScrollBarMetrics());
			_viewModels.Add(second);
			Assert.Equal(DatSortColumn.Size, second.SortColumn);
			Assert.True(second.SortDescending);
			Assert.Equal([120.0, 60.0, 70.0, 100.0], second.ColumnWidths);
		}

		[Fact]
		public void TheVolumeAndTheRepeatOfTheSounds_AreKeptForTheNextRun()
		{
			MainViewModel first = CreateViewModel(new FakeDialogs());
			Assert.Equal(100, first.Preview.Audio.Volume);
			Assert.False(first.Preview.Audio.IsLooping);

			first.Preview.Audio.Volume = 35;
			first.Preview.Audio.IsLooping = true;
			first.Shutdown();

			MainViewModel second = CreateViewModel(new FakeDialogs());
			Assert.Equal(35, second.Preview.Audio.Volume);
			Assert.True(second.Preview.Audio.IsLooping);
		}

		/// <summary>A folder opened as an archive, with a message file that is right, one that is not, and a text.</summary>
		private String MakeFolderWithMessages()
		{
			String folder = MakeFolder("messages.dat");
			String game = Path.Combine(folder, "text", "english", "game");
			File.WriteAllText(Path.Combine(game, "combat.msg"), "{100}{}{Hello}\r\n");
			File.WriteAllText(Path.Combine(game, "broken.msg"), "{x}{}{one}\r\n{2}{}{two}\r\n{2}{}{three}\r\n");
			File.WriteAllText(Path.Combine(game, "notes.txt"), "{x}{}{this is not a message file}");
			return folder;
		}

		[Fact]
		public async Task TheMessageFilesOfTheFolderShown_GetAWarning_WhenTheyHaveProblems()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			FolderNode root = Assert.Single(viewModel.Roots);

			viewModel.SelectFolder(root.Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;

			ListEntry broken = viewModel.Items.Single(item => item.Name == "broken.msg");
			ListEntry fine = viewModel.Items.Single(item => item.Name == "combat.msg");
			ListEntry text = viewModel.Items.Single(item => item.Name == "notes.txt");
			Assert.True(broken.IsMessageFile);
			Assert.True(broken.HasMessageIssues);
			Assert.Equal(MsgIssueSeverity.Critical, broken.MessageSeverity);
			Assert.Equal(2, broken.Messages!.Issues.Count);
			Assert.False(String.IsNullOrEmpty(broken.MessageTip));
			Assert.NotNull(fine.Messages);
			Assert.False(fine.HasMessageIssues);
			Assert.Null(fine.MessageSeverity);
			Assert.Equal(String.Empty, fine.MessageTip);
			Assert.False(text.IsMessageFile);
			Assert.Null(text.Messages);
		}

		[Fact]
		public async Task TheWarning_TellsItsViewWhenTheCheckEnds_AndTheResultsAreKeptWhenTheListIsMadeAgain()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			ListEntry first = viewModel.Items.Single(item => item.Name == "broken.msg");
			await viewModel.MessageScan;
			Assert.True(first.HasMessageIssues);

			// A result tells the view (the check runs in the background: it can end before anything listens, so it is given again).
			List<String?> changes = [];
			first.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
			first.SetMessages(first.Messages!);
			Assert.Contains(nameof(ListEntry.HasMessageIssues), changes);
			Assert.Contains(nameof(ListEntry.MessageSeverity), changes);

			// A new list (a refresh, a sort) has the warnings at once, without a new check.
			viewModel.RefreshCommand.Execute(null);
			ListEntry second = viewModel.Items.Single(item => item.Name == "broken.msg");
			Assert.NotSame(first, second);
			Assert.True(second.HasMessageIssues);
			await viewModel.MessageScan;
		}

		[Fact]
		public async Task TheProblemsOfTheSelectedMessageFiles_AreListedInAWindow()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;

			// Nothing selected, or a text that is not a message file: the command is not there.
			Assert.False(viewModel.ShowMessageProblemsCommand.CanExecute(null));
			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "notes.txt")]);
			Assert.False(viewModel.ShowMessageProblemsCommand.CanExecute(null));

			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "broken.msg"), viewModel.Items.Single(item => item.Name == "combat.msg")]);
			Assert.True(viewModel.ShowMessageProblemsCommand.CanExecute(null));
			await viewModel.ShowMessageProblemsAsync();

			MsgIssuesViewModel issues = Assert.Single(dialogs.ShownIssues);
			MsgFileItem file = Assert.Single(issues.Files);
			Assert.EndsWith("broken.msg", file.Path, StringComparison.Ordinal);
			Assert.Equal(2, file.Count);

			// A file that is right: a message, no window.
			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "combat.msg")]);
			await viewModel.ShowMessageProblemsAsync();
			Assert.Single(dialogs.ShownIssues);
			Assert.Contains("Str.MsgNoProblems", dialogs.Messages);
		}

		[Fact]
		public async Task TheMessageFilesOfTheWholeArchive_AreCheckedFromTheToolsMenu()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			Assert.False(viewModel.ScanMessagesCommand.CanExecute(null));
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			Assert.True(viewModel.ScanMessagesCommand.CanExecute(null));

			await viewModel.ScanMessagesAsync();

			MsgIssuesViewModel issues = Assert.Single(dialogs.ShownIssues);
			Assert.Equal(2, Assert.Single(issues.Files).Count);

			// What was found is kept: the list of a folder opened later has its warnings at once.
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			Assert.True(viewModel.Items.Single(item => item.Name == "broken.msg").HasMessageIssues);
			await viewModel.MessageScan;
		}

		[Fact]
		public async Task AnArchiveWhoseMessageFilesAreRight_SaysSo()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolder()]);

			await viewModel.ScanMessagesAsync();

			// The one message file there is has no problem: a message, no window.
			Assert.Empty(dialogs.ShownIssues);
			Assert.Contains("Str.MsgNoProblems", dialogs.Messages);
		}

		[Fact]
		public async Task TheChecksThatAreTurnedOff_AreNotLookedForInTheMessageFiles()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;
			ListEntry broken = viewModel.Items.Single(item => item.Name == "broken.msg");
			Assert.Equal([MsgIssueKind.NoFinalEmptyLine], viewModel.DisabledMessageChecks);
			Assert.Equal(2, broken.Messages!.Issues.Count);

			// The duplicate numbers are not looked for any more: at once, and without checking again.
			SettingsDialogViewModel model = new(new SettingsData());
			model.MessageChecks.SelectMany(group => group.Items).Single(option => option.Kind == MsgIssueKind.DuplicateId).IsEnabled = false;
			await viewModel.ApplySettingsAsync(model);
			Assert.Equal([MsgIssueKind.DuplicateId, MsgIssueKind.NoFinalEmptyLine], viewModel.DisabledMessageChecks);
			Assert.Equal(MsgIssueKind.InvalidId, Assert.Single(broken.Messages!.Issues).Kind);
			Assert.Equal([MsgIssueKind.DuplicateId, MsgIssueKind.NoFinalEmptyLine], viewModel.Preview.DisabledMessageChecks);

			// The window lists what is looked for only.
			viewModel.SetSelectedItems([broken]);
			await viewModel.ShowMessageProblemsAsync();
			Assert.Equal(1, Assert.Single(Assert.Single(dialogs.ShownIssues).Files).Count);

			// Turned on again: everything comes back.
			await viewModel.ApplySettingsAsync(new SettingsDialogViewModel(new SettingsData()));
			Assert.Equal(2, broken.Messages!.Issues.Count);
		}

		[Fact]
		public async Task TheExtractionItem_SaysFileForOneFile_AndFilesForMore()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			List<String?> changes = [];
			viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

			Assert.Equal("Str.ExtractFiles", viewModel.ExtractItemsText);
			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "combat.msg")]);
			Assert.Equal("Str.ExtractFile", viewModel.ExtractItemsText);
			Assert.Contains(nameof(MainViewModel.ExtractItemsText), changes);

			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "combat.msg"), viewModel.Items.Single(item => item.Name == "broken.msg")]);
			Assert.Equal("Str.ExtractFiles", viewModel.ExtractItemsText);

			// The menu of the list is shorter with several files selected.
			Assert.True(viewModel.IsMultiSelection);
			viewModel.SetSelectedItems([viewModel.Items.Single(item => item.Name == "combat.msg")]);
			Assert.False(viewModel.IsMultiSelection);
			Assert.Contains(nameof(MainViewModel.IsMultiSelection), changes);
		}

		[Fact]
		public async Task ADoubleClickOnAMessageFileWithProblems_OpensTheWindowOfTheProblems_WhenTheSettingSaysSo()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;
			ListEntry broken = viewModel.Items.Single(item => item.Name == "broken.msg");
			ListEntry fine = viewModel.Items.Single(item => item.Name == "combat.msg");

			// Not asked: the file opens, as always (here the viewer).
			viewModel.SetSelectedItems([broken]);
			await viewModel.OpenItemAsync(broken);
			Assert.Empty(dialogs.ShownIssues);

			await viewModel.ApplySettingsAsync(new SettingsDialogViewModel(new SettingsData()) { DoubleClickMessageProblems = true });

			// A file that has problems: the window, and not the file.
			viewModel.SetSelectedItems([broken]);
			await viewModel.OpenItemAsync(broken);
			MsgFileItem file = Assert.Single(Assert.Single(dialogs.ShownIssues).Files);
			Assert.EndsWith("broken.msg", file.Path, StringComparison.Ordinal);

			// A file that is right opens as usual.
			viewModel.SetSelectedItems([fine]);
			await viewModel.OpenItemAsync(fine);
			Assert.Single(dialogs.ShownIssues);
		}

		[Fact]
		public async Task WhenNothingIsLookedFor_TheWindowsSaySo_AndTheListHasNoWarning()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;
			ListEntry broken = viewModel.Items.Single(item => item.Name == "broken.msg");
			Assert.True(broken.HasMessageIssues);

			SettingsDialogViewModel model = new(new SettingsData());
			foreach (MsgCheckOption option in model.MessageChecks.SelectMany(group => group.Items))
			{
				option.IsEnabled = false;
			}

			await viewModel.ApplySettingsAsync(model);

			Assert.False(broken.HasMessageIssues);
			viewModel.SetSelectedItems([broken]);
			await viewModel.ShowMessageProblemsAsync();
			await viewModel.ScanMessagesAsync();
			Assert.Empty(dialogs.ShownIssues);
			Assert.Equal(["Str.MsgAllChecksOff", "Str.MsgAllChecksOff"], dialogs.Messages);
		}

		[Fact]
		public async Task TheViewer_OpensTheWindowOfTheProblemsOfTheMessageFileItShows()
		{
			FakeDialogs dialogs = new();
			MainViewModel viewModel = CreateViewModel(dialogs);
			await viewModel.OpenFilesAsync([MakeFolderWithMessages()]);
			viewModel.SelectFolder(Assert.Single(viewModel.Roots).Document.FindNode("text\\english\\game")!);
			await viewModel.MessageScan;

			TextViewerViewModel text = viewModel.Preview.Text;
			Assert.False(text.CanShowProblems);
			await viewModel.Preview.LoadAsync(viewModel.CurrentDocument!.Archive, viewModel.CurrentDocument.Archive.Find("text\\english\\game\\broken.msg")!);

			Assert.True(text.CanShowProblems);
			Assert.True(text.ShowProblemsCommand.CanExecute(null));
			text.ShowProblemsCommand.Execute(null);
			MsgFileItem file = Assert.Single(Assert.Single(dialogs.ShownIssues).Files);
			Assert.EndsWith("broken.msg", file.Path, StringComparison.Ordinal);
			Assert.Equal(2, file.Count);

			await viewModel.Preview.LoadAsync(viewModel.CurrentDocument.Archive, viewModel.CurrentDocument.Archive.Find("text\\english\\game\\notes.txt")!);
			Assert.False(text.CanShowProblems);
		}

		[Fact]
		public async Task TheWidthOfTheBlocks_IsKeptWhenTheSettingSaysSo()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			Assert.True(viewModel.SaveBlockWidths);
			Assert.Equal(0, viewModel.TreeWidth);
			Assert.Equal(0, viewModel.PreviewShare);

			viewModel.SaveBlocks(333, 0.75);
			Assert.Equal(333, viewModel.TreeWidth);
			Assert.Equal(0.75, viewModel.PreviewShare);

			// Not wanted any more: forgotten, and nothing is kept until it is wanted again.
			await viewModel.ApplySettingsAsync(new SettingsDialogViewModel(new SettingsData { SaveBlockWidths = false }) { SaveBlockWidths = false });
			Assert.False(viewModel.SaveBlockWidths);
			Assert.Equal(0, viewModel.TreeWidth);
			viewModel.SaveBlocks(400, 1);
			Assert.Equal(0, viewModel.TreeWidth);
			Assert.Equal(0, viewModel.PreviewShare);

			await viewModel.ApplySettingsAsync(new SettingsDialogViewModel(new SettingsData()));
			viewModel.SaveBlocks(410, 1.5);
			Assert.Equal(410, viewModel.TreeWidth);
			Assert.Equal(1.5, viewModel.PreviewShare);
		}

		[Fact]
		public async Task Shortcuts_AreCommands()
		{
			MainViewModel viewModel = CreateViewModel(new FakeDialogs());
			await viewModel.OpenFilesAsync([MakeFolder()]);

			Boolean preview = viewModel.IsPreviewVisible;
			viewModel.TogglePreviewCommand.Execute(null);
			Assert.NotEqual(preview, viewModel.IsPreviewVisible);
			viewModel.TogglePreviewCommand.Execute(null);
			Assert.Equal(preview, viewModel.IsPreviewVisible);

			List<String?> changes = [];
			viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
			Int32 requests = viewModel.SearchFocusRequests;
			viewModel.FocusSearchCommand.Execute(null);
			Assert.Equal(requests + 1, viewModel.SearchFocusRequests);
			Assert.Contains(nameof(MainViewModel.SearchFocusRequests), changes);

			// Enter goes to the next entry that matches, Shift+Enter to the previous one; the list is told to show it.
			List<ListEntry> revealed = [];
			viewModel.RevealRequested += revealed.Add;
			viewModel.SearchText = "e";
			viewModel.FindNextCommand.Execute(null);
			viewModel.FindNextCommand.Execute(null);
			viewModel.FindPreviousCommand.Execute(null);
			Assert.Equal(3, revealed.Count);
			Assert.All(revealed, item => Assert.Contains("e", item.Name));
		}

		#endregion
	}
}