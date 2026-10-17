namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Core;
	using Models;
	using Services;

	public sealed partial class MainViewModel
	{
		private async Task ImportFilesAsync(Object? parameter)
		{
			IReadOnlyList<String> files = _dialogs.PickFiles(_settings.Data.LastImportFolder);
			if (files.Count > 0)
			{
				_settings.Data.LastImportFolder = Path.GetDirectoryName(files[0]);
				await ImportAsync(files, false);
			}
		}

		private async Task ImportFolderAsync(Object? parameter)
		{
			String? folder = _dialogs.PickFolder(Loc.Get("Str.ImportFolder"), _settings.Data.LastImportFolder);
			if (folder != null)
			{
				_settings.Data.LastImportFolder = folder;
				await ImportAsync([folder], true);
			}
		}

		/// <summary>Imports files and folders dropped from the file explorer.</summary>
		public async Task ImportDroppedAsync(IReadOnlyList<String> paths)
		{
			if (Busy.IsBusy)
			{
				return;
			}

			if (CurrentDocument == null)
			{
				await OpenFilesAsync(paths.Where(DatFiles.IsArchiveFile));
				return;
			}

			// A DAT that cannot be changed takes nothing: the files are not imported.
			if (!CurrentDocument.Archive.IsReadOnly)
			{
				await ImportAsync(paths, false);
			}
		}

		/// <summary>Opens the DAT archives (.dat, .patch0 to .patch9) dropped on the tree.</summary>
		public async Task OpenDroppedAsync(IReadOnlyList<String> paths)
		{
			if (!Busy.IsBusy)
			{
				await OpenFilesAsync(paths.Where(File.Exists));
			}
		}

		/// <param name="paths">Files and folders to import into the selected folder.</param>
		/// <param name="mergeFolderContents">True to import the content of a folder, false to import the folder itself.</param>
		private async Task ImportAsync(IReadOnlyList<String> paths, Boolean mergeFolderContents)
		{
			FolderNode? target = SelectedFolder;
			if (target == null)
			{
				return;
			}

			DatDocument document = target.Document;
			List<ImportItem> items = new();

			Boolean listed = await RunBusyAsync(
				Loc.Get("Str.Importing"),
				true,
				(_, token) => Task.Run(() => items.AddRange(ImportPlanner.Collect(paths, mergeFolderContents, token)), token));

			if (!listed || items.Count == 0)
			{
				return;
			}

			List<ImportStep>? plan = ImportPlanner.Plan(document.Archive, target.Path, items, new ConflictResolver(_dialogs, Title), out List<DatEntry> replaced);
			if (plan == null)
			{
				return;
			}

			List<String> errors = new();
			Int32 added = 0;

			Boolean imported = await RunBusyAsync(
				Loc.Get("Str.Importing"),
				false,
				(progress, _) => Task.Run(() =>
				{
					document.Archive.Remove(replaced);

					for (Int32 index = 0; index < plan.Count; index++)
					{
						ImportStep step = plan[index];
						try
						{
							document.Archive.AddFile(step.SourceFile, step.Directory, step.Name);
							added++;
						}
						catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
						{
							errors.Add($"{step.SourceFile}: {ErrorText.Describe(exception)}");
						}

						if (index % 32 == 0 || index == plan.Count - 1)
						{
							progress.Report(new DatProgress(step.Name, index + 1, plan.Count));
						}
					}
				}));

			document.Refresh();
			RefreshItems();

			if (imported)
			{
				StatusText = Loc.Format("Str.Imported", added);
				ReportFailures(Loc.Format("Str.ImportFailures", errors.Count), errors);
			}
		}
	}
}