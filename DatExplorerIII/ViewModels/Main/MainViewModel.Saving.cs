namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Core;
	using Models.Archives;
	using Models.Interface;
	using Services.Interface;

	public sealed partial class MainViewModel
	{
		/// <summary>Damaged files cannot be copied: the user decides to drop them. False when the user gives up.</summary>
		private Boolean ConfirmRemoveDamaged(DatDocument document)
		{
			DatArchive archive = document.Archive;
			if (archive.DamagedCount == 0)
				return true;

			if (!_dialogs.Confirm(Title, Loc.Format("Str.SaveDamagedPrompt", archive.DamagedCount), DialogIcon.Warning))
				return false;

			archive.RemoveDamaged();
			document.Refresh();
			RefreshItems();
			return true;
		}

		/// <summary>
		/// Writes the archive, complete, to a file with another name. The file (or the folder) it came from is not changed.
		/// </summary>
		internal Task SaveAsAsync() => SaveDocumentAsAsync(CurrentDocument);

		internal async Task SaveDocumentAsAsync(DatDocument? document)
		{
			if (document == null || !ConfirmRemoveDamaged(document))
				return;

			DatArchive archive = document.Archive;
			String current = archive.FilePath;
			String? parent = Path.GetDirectoryName(current);
			String name = Path.GetFileNameWithoutExtension(current);

			// The suggestion is not the name of the folder or file it comes from: that one must be kept.
			String suggested = archive.IsFolderBacked ? name + "_new.dat" : Path.GetFileName(current);
			SaveAsChoice? choice = _dialogs.PickSaveAsFile(suggested, parent, archive.Format, archive.IsFolderBacked);
			if (choice == null)
				return;

			String target = Path.GetFullPath(choice.Path);
			if (Directory.Exists(target))
			{
				_dialogs.ShowError(Loc.Format("Str.SaveAsIsFolder", target));
				return;
			}

			if (!archive.IsFolderBacked && String.Equals(target, current, StringComparison.OrdinalIgnoreCase))
			{
				await SaveDocumentAsync(document, false);
				return;
			}

			if (Roots.Any(root => root.Document != document && String.Equals(root.Document.FilePath, target, StringComparison.OrdinalIgnoreCase)))
			{
				_dialogs.ShowError(Loc.Format("Str.SaveAsOpenElsewhere", target));
				return;
			}

			Boolean saved = await RunBusyAsync(
				Loc.Get("Str.Saving"),
				false,
				(progress, _) => Task.Run(() => archive.SaveAs(target, choice.Format, progress)));

			document.Refresh();
			RefreshItems();

			if (saved)
			{
				RememberRecent(target);
				StatusText = Loc.Format("Str.SavedAs", target);
			}
			else
			{
				StatusText = Loc.Get("Str.SaveFailed");
			}
		}

		internal Task SaveAsync(Boolean quick) => SaveDocumentAsync(CurrentDocument, quick);

		internal async Task SaveDocumentAsync(DatDocument? document, Boolean quick)
		{
			if (document == null)
				return;

			DatArchive archive = document.Archive;

			// An archive made from a folder has no file to save to: it is saved under a new name.
			if (archive.IsFolderBacked)
			{
				await SaveDocumentAsAsync(document);
				return;
			}

			if (!ConfirmRemoveDamaged(document))
				return;

			Boolean repair = archive.RequiresRebuild;
			String message = Loc.Get("Str.ConfirmSave");
			if (archive.Format == DatFormat.Fallout1)
				message += Environment.NewLine + Environment.NewLine + Loc.Get("Str.Fallout1SaveNote");

			if (repair)
			{
				message += Environment.NewLine + Environment.NewLine + Loc.Get("Str.RepairSaveNote");
			}
			else if (quick)
			{
				message += Environment.NewLine + Environment.NewLine + Loc.Get("Str.QuickSaveWarning");
			}

			if (!_dialogs.Confirm(Title, message))
				return;

			Boolean saved = await RunBusyAsync(
				Loc.Get("Str.Saving"),
				false,
				(progress, _) => Task.Run(() => archive.Save(quick, progress)));

			document.Refresh();
			RefreshItems();

			if (!saved)
			{
				StatusText = Loc.Get("Str.SaveFailed");
			}
			else if (repair && archive.BackupPath != null)
			{
				StatusText = Loc.Format("Str.SavedBackup", archive.BackupPath);
			}
			else
			{
				StatusText = Loc.Get("Str.Saved");
			}
		}
	}
}