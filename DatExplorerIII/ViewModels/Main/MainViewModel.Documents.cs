namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Models.Archives;
	using Models.Interface;
	using Services.Interface;
	using Services.Platform;

	public sealed partial class MainViewModel
	{
		private readonly SemaphoreSlim _forwardedOpens = new(1, 1);

		public async Task OpenFilesAsync(IEnumerable<String> paths)
		{
			foreach (String path in paths)
			{
				try
				{
					await OpenArchiveAsync(path);
				}
				catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
				{
					_dialogs.ShowError(ErrorText.Describe(exception));
				}
			}
		}

		/// <summary>
		/// Opens the files another launch of the program handed over. They wait for the end of the operation that is running, and
		/// for the ones that came before.
		/// </summary>
		public async Task OpenFromAnotherInstanceAsync(IReadOnlyList<String> paths)
		{
			await _forwardedOpens.WaitAsync();
			try
			{
				while (Busy.IsBusy)
				{
					await Task.Delay(200);
				}

				await OpenFilesAsync(paths);
			}
			finally
			{
				_forwardedOpens.Release();
			}
		}

		private async Task PickAndOpenAsync()
		{
			IReadOnlyList<String> files = _dialogs.PickDatFiles(_settings.Data.LastDatFolder);
			if (files.Count > 0)
			{
				_settings.Data.LastDatFolder = Path.GetDirectoryName(files[0]);
				await OpenFilesAsync(files);
			}
		}

		private async Task PickAndOpenFolderAsync()
		{
			String? folder = _dialogs.PickFolder(Loc.Get("Str.OpenFolderTitle"), _settings.Data.LastDatFolder);
			if (folder != null)
			{
				_settings.Data.LastDatFolder = Path.GetDirectoryName(folder);
				await OpenFilesAsync([folder]);
			}
		}

		/// <summary>Opens a folder like an archive (a folder named master.dat that holds the files of a DAT, for instance).</summary>
		private async Task OpenFolderAsync(String fullPath)
		{
			DatArchive? archive = null;
			Boolean ran = await RunBusyAsync(Loc.Get("Str.Opening"), false, async (_, _) =>
			{
				try
				{
					archive = await Task.Run(() => DatArchive.OpenFolder(fullPath, DatFormat.Fallout2, _settings.Data.ShowHiddenFolders));
				}
				catch (DatException exception)
				{
					_dialogs.ShowError(ErrorText.Describe(exception));
				}
			});

			if (!ran || archive == null)
				return;

			DatDocument document = new(archive);
			AddDocument(document);
			RememberRecent(fullPath);

			String name = Path.GetFileName(fullPath);
			if (archive.SkippedFiles.Count == 0)
			{
				StatusText = Loc.Format("Str.OpenedFolder", name, archive.Count);
				return;
			}

			StatusText = Loc.Format("Str.OpenedFolderSkipped", name, archive.Count, archive.SkippedFiles.Count);
			List<String> lines = archive.SkippedFiles.Take(MaxListedItems).ToList();
			if (archive.SkippedFiles.Count > MaxListedItems)
				lines.Add(Loc.Format("Str.AndMore", archive.SkippedFiles.Count - MaxListedItems));

			_dialogs.ShowMessage(Title, Loc.Get("Str.FolderSkippedNote") + Environment.NewLine + Environment.NewLine + String.Join(Environment.NewLine, lines), DialogIcon.Warning);
		}

		private async Task OpenArchiveAsync(String path)
		{
			String fullPath = Path.GetFullPath(path);

			FolderNode? existing = Roots.FirstOrDefault(root => String.Equals(root.Document.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
			if (existing != null)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.AlreadyOpen"), DialogIcon.Information);
				SelectFolder(existing);
				return;
			}

			if (Directory.Exists(fullPath))
			{
				await OpenFolderAsync(fullPath);
				return;
			}

			DatArchive? archive = null;
			DatException? damage = null;
			Boolean ran = await RunBusyAsync(Loc.Get("Str.Opening"), false, async (_, _) =>
			{
				try
				{
					archive = await Task.Run(() => DatArchive.Open(fullPath));
				}
				catch (DatException exception) when (exception.Kind is DatErrorKind.NotADat or DatErrorKind.TruncatedFile or DatErrorKind.InvalidTree)
				{
					damage = exception;
				}
			});

			if (ran && archive == null && damage != null)
				archive = await RecoverAsync(fullPath, damage);

			if (archive == null)
				return;

			DatDocument document = new(archive);
			AddDocument(document);
			ReportOpened(document);
			RememberRecent(fullPath);
		}

		/// <summary>Puts a file that was opened at the top of the recent files.</summary>
		private void RememberRecent(String fullPath)
		{
			Services.Platform.RecentFiles.Add(_settings.Data.RecentFiles, fullPath);
			_settings.Save();
			OnPropertyChanged(nameof(RecentFiles));
		}

		/// <summary>Empties the list of the recent files (the files themselves are not touched).</summary>
		private void ClearRecent()
		{
			_settings.Data.RecentFiles.Clear();
			_settings.Save();
			OnPropertyChanged(nameof(RecentFiles));
		}

		private async Task OpenRecentAsync(Object? parameter)
		{
			if (parameter is not String path)
				return;

			if (!File.Exists(path) && !Directory.Exists(path))
			{
				// A file that was moved or deleted does not stay in the list.
				Services.Platform.RecentFiles.Remove(_settings.Data.RecentFiles, path);
				_settings.Save();
				OnPropertyChanged(nameof(RecentFiles));
				_dialogs.ShowMessage(Title, Loc.Format("Str.RecentMissing", path), DialogIcon.Warning);
				return;
			}

			await OpenFilesAsync([path]);
		}

		/// <summary>
		/// The file cannot be opened normally: offers to read what can be read.
		/// </summary>
		private async Task<DatArchive?> RecoverAsync(String fullPath, DatException damage)
		{
			String question = ErrorText.Describe(damage) + Environment.NewLine + Environment.NewLine + Loc.Get("Str.OfferRecovery");
			if (!_dialogs.Confirm(Title, question, DialogIcon.Warning))
				return null;

			DatArchive? recovered = null;
			Boolean ran = await RunBusyAsync(Loc.Get("Str.Recovering"), false, async (_, _) =>
			{
				try
				{
					recovered = await Task.Run(() => DatArchive.Open(fullPath, DatOpenMode.Recover));
				}
				catch (DatException exception)
				{
					_dialogs.ShowError(Loc.Get("Str.RecoveryFailed") + Environment.NewLine + ErrorText.Describe(exception));
				}
			});

			return ran ? recovered : null;
		}

		private void ReportOpened(DatDocument document)
		{
			DatArchive archive = document.Archive;
			String name = Path.GetFileName(document.FilePath);

			if (archive.IsRecovered)
			{
				StatusText = Loc.Format("Str.OpenedRecovered", name, archive.Count, archive.Issues.Count);
				_dialogs.ShowMessage(Title, Loc.Format("Str.RecoveryReport", name, archive.Count, archive.Issues.Count) + Environment.NewLine + Environment.NewLine + DescribeIssues(archive), DialogIcon.Warning);
			}
			else if (archive.Issues.Count > 0)
			{
				StatusText = Loc.Format("Str.OpenedWithProblems", name, archive.Count, archive.Issues.Count);
			}
			else
			{
				StatusText = Loc.Format("Str.Opened", name, archive.Count);
			}
		}

		/// <summary>The first problems of an archive, one per line.</summary>
		private static String DescribeIssues(DatArchive archive)
		{
			List<String> lines = archive.Issues.Take(MaxListedItems).Select(issue => ErrorText.Describe(issue)).ToList();
			if (archive.Issues.Count > MaxListedItems)
				lines.Add(Loc.Format("Str.AndMore", archive.Issues.Count - MaxListedItems));

			return String.Join(Environment.NewLine, lines);
		}

		private void CreateArchive(DatFormat format)
		{
			String? path = _dialogs.PickNewDatFile(format, _settings.Data.LastDatFolder);
			if (path == null)
				return;

			path = Path.GetFullPath(path);
			_settings.Data.LastDatFolder = Path.GetDirectoryName(path);

			if (Roots.Any(root => String.Equals(root.Document.FilePath, path, StringComparison.OrdinalIgnoreCase)))
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.AlreadyOpen"), DialogIcon.Information);
				return;
			}

			AddDocument(new DatDocument(DatArchive.Create(path, format)));
		}

		private void AddDocument(DatDocument document)
		{
			Roots.Add(document.Root);
			document.Root.IsExpanded = true;
			SelectFolder(document.Root);
		}

		/// <summary>Closes every DAT that is open, after one confirmation when some have changes that are not saved.</summary>
		private void CloseAllDocuments()
		{
			if (Roots.Count == 0)
				return;

			if (Roots.Any(root => root.Document.ShouldSave) && !_dialogs.Confirm(Title, Loc.Get("Str.QuitUnsaved"), DialogIcon.Warning))
				return;

			List<FolderNode> roots = Roots.ToList();
			SelectedFolder = null;
			foreach (FolderNode root in roots)
			{
				Roots.Remove(root);
				root.Document.Archive.Dispose();
			}

			StatusText = Loc.Get("Str.Ready");
		}

		private Boolean CanMoveCurrentDocument(Int32 step)
		{
			if (!CanBrowse() || CurrentDocument == null)
				return false;

			Int32 target = Roots.IndexOf(CurrentDocument.Root) + step;
			return target >= 0 && target < Roots.Count;
		}

		/// <summary>Moves the DAT that is selected one place up (-1) or down (1) in the list of the open ones; it stays selected.</summary>
		private void MoveCurrentDocument(Int32 step)
		{
			if (!CanMoveCurrentDocument(step) || CurrentDocument is not { } document)
				return;

			FolderNode? selected = SelectedFolder;
			Int32 index = Roots.IndexOf(document.Root);

			// The tree forgets what is selected when an item moves: the folder is selected again.
			SelectedFolder = null;
			Roots.Move(index, index + step);
			if (selected != null)
				SelectFolder(selected);
		}

		private void CloseCurrentDocument()
		{
			DatDocument? document = CurrentDocument;
			if (document == null)
				return;

			if (document.ShouldSave && !_dialogs.Confirm(Title, Loc.Format("Str.ConfirmCloseUnsaved", Path.GetFileName(document.FilePath)), DialogIcon.Warning))
				return;

			Int32 index = Roots.IndexOf(document.Root);
			SelectedFolder = null;
			Roots.Remove(document.Root);
			document.Archive.Dispose();

			if (Roots.Count > 0)
				SelectFolder(Roots[Math.Min(index, Roots.Count - 1)]);

			StatusText = Loc.Get("Str.Ready");
		}
	}
}