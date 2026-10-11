namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

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

		private readonly List<MainViewModel> _viewModels = new();

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

			public List<String> Errors { get; } = new();

			public List<String> Messages { get; } = new();

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
			{
			}

			public Int32 ViewersShown { get; private set; }

			public void ShowViewer(ViewerViewModel viewer)
			{
				ViewersShown++;
			}

			public Func<SettingsDialogViewModel, Boolean>? SettingsScript { get; set; }

			public Boolean ShowSettings(SettingsDialogViewModel settings) => SettingsScript?.Invoke(settings) ?? false;

			public void ShowAbout()
			{
			}
		}

		private MainViewModel CreateViewModel(FakeDialogs dialogs)
		{
			SettingsService settings = new(Path.Combine(_root, "settings"), null);
			MainViewModel viewModel = new(dialogs, settings, new TempFolder());
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

		#endregion
	}
}