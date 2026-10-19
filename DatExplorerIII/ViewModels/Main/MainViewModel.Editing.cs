namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Core;
	using Models;
	using Services;

	public sealed partial class MainViewModel
	{
		private void CreateFolder()
		{
			FolderNode? parent = SelectedFolder;
			if (parent == null)
			{
				return;
			}

			Int32 number = 0;
			String suggestion;
			do
			{
				suggestion = number == 0 ? Loc.Get("Str.DefaultFolderName") : Loc.Get("Str.DefaultFolderName") + " " + (number + 1);
				number++;
			}
			while (ValidateName(parent, suggestion, null, null) != null);

			String? name = _dialogs.Prompt(Loc.Get("Str.NewFolder"), Loc.Get("Str.FolderNamePrompt"), suggestion, text => ValidateName(parent, text, null, null));
			if (name == null)
			{
				return;
			}

			DatDocument document = parent.Document;
			document.Archive.AddFolder(parent.Path + name);
			document.Refresh();

			parent.IsExpanded = true;
			FolderNode? created = document.FindNode(parent.Path + name);
			if (created != null)
			{
				SelectFolder(created);
			}
		}

		private void RenameSelectedItem()
		{
			FolderNode? folder = SelectedFolder;
			ListEntry? item = SelectedItems.FirstOrDefault();
			if (folder == null || item == null || SelectedItems.Count != 1)
			{
				return;
			}

			String? name = _dialogs.Prompt(Loc.Get("Str.Rename"), Loc.Get("Str.NewNamePrompt"), item.Name, text => ValidateName(folder, text, item.Entry, item.Folder));
			if (name == null || name == item.Name)
			{
				return;
			}

			DatDocument document = folder.Document;
			try
			{
				if (item.Entry != null)
				{
					document.Archive.Rename(item.Entry, name);
					document.Refresh();
					RefreshItems();
					Reveal(candidate => candidate.Entry == item.Entry);
				}
				else
				{
					FolderNode renamed = document.RenameFolder(item.Folder!, name);
					RefreshItems();
					Reveal(candidate => candidate.Folder == renamed);
				}
			}
			catch (DatException exception)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
			}
		}

		private void RenameSelectedFolder()
		{
			FolderNode? folder = SelectedFolder;
			if (folder == null || folder.IsRoot || folder.Parent == null)
			{
				return;
			}

			FolderNode parent = folder.Parent;
			String? name = _dialogs.Prompt(Loc.Get("Str.Rename"), Loc.Get("Str.NewNamePrompt"), folder.Name, text => ValidateName(parent, text, null, folder));
			if (name == null || name == folder.Name)
			{
				return;
			}

			try
			{
				FolderNode renamed = folder.Document.RenameFolder(folder, name);
				SelectFolder(renamed);
			}
			catch (DatException exception)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
			}
		}

		/// <summary>Puts in lower case the names of the selected folder, of its sub folders and of the files in them (the whole archive for the root).</summary>
		private void LowercaseSelectedFolder()
		{
			FolderNode? folder = SelectedFolder;
			if (folder == null)
			{
				return;
			}

			DatDocument document = folder.Document;
			String directory = folder.IsRoot ? String.Empty : folder.Path;
			Int32 count = document.Archive.LowercaseTree(directory, false);
			if (count == 0)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.LowercaseNothing"), DialogIcon.Information);
				return;
			}

			String scope = folder.IsRoot ? Loc.Get("Str.LowercaseScopeAll") : Loc.Format("Str.LowercaseScopeFolder", folder.Path.TrimEnd('\\'));
			if (!_dialogs.Confirm(Title, Loc.Format("Str.LowercaseConfirm", scope, count.ToString("N0", CultureInfo.CurrentCulture)), DialogIcon.Question))
			{
				return;
			}

			try
			{
				document.Archive.LowercaseTree(directory);
				document.Refresh();
				RefreshItems();
				StatusText = Loc.Format("Str.LowercaseDone", count.ToString("N0", CultureInfo.CurrentCulture));
			}
			catch (DatException exception)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
			}
		}

		private void Reveal(Func<ListEntry, Boolean> match)
		{
			ListEntry? item = Items.FirstOrDefault(match);
			if (item != null)
			{
				RevealRequested?.Invoke(item);
			}
		}

		/// <summary>Returns why a name cannot be used in a folder, or null when it can.</summary>
		private String? ValidateName(FolderNode folder, String name, DatEntry? ignoredEntry, FolderNode? ignoredFolder)
		{
			try
			{
				DatArchive.ValidateName(name);
			}
			catch (DatException)
			{
				return Loc.Get("Str.InvalidName");
			}

			if (folder.Children.Any(child => child != ignoredFolder && String.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				return Loc.Get("Str.FolderExists");
			}

			if (folder.Document.Archive.GetEntries(folder.Path).Any(entry => entry != ignoredEntry && String.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)))
			{
				return Loc.Get("Str.FileExists");
			}

			return null;
		}

		private void DeleteSelectedItems()
		{
			FolderNode? folder = SelectedFolder;
			if (folder == null || SelectedItems.Count == 0)
			{
				return;
			}

			if (!_dialogs.Confirm(Title, Loc.Get("Str.ConfirmDelete"), DialogIcon.Warning))
			{
				return;
			}

			DatArchive archive = folder.Document.Archive;
			List<ListEntry> items = SelectedItems.ToList();

			archive.Remove(items.Where(item => item.Entry != null).Select(item => item.Entry!));
			foreach (ListEntry item in items.Where(item => item.Folder != null))
			{
				archive.RemoveFolder(item.Folder!.Path);
			}

			folder.Document.Refresh();
			RefreshItems();
			StatusText = Loc.Format("Str.Deleted", items.Count);
		}

		private void DeleteSelectedFolder()
		{
			FolderNode? folder = SelectedFolder;
			if (folder == null || folder.Parent == null)
			{
				return;
			}

			if (!_dialogs.Confirm(Title, Loc.Get("Str.ConfirmDelete"), DialogIcon.Warning))
			{
				return;
			}

			DatDocument document = folder.Document;
			FolderNode parent = folder.Parent;
			SelectFolder(parent);

			document.Archive.RemoveFolder(folder.Path);
			document.Refresh();
			RefreshItems();
		}
	}
}