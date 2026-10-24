namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Core;
	using Models.Archives;
	using Models.Interface;
	using Services.Interface;
	using Services.Platform;

	public sealed partial class MainViewModel
	{
		/// <summary>More than one item is selected: the menu of the list only offers what is made of many files (extract, import, new folder).</summary>
		public Boolean IsMultiSelection => SelectedItems.Count > 1;

		/// <summary>"Extract file to..." for one file, "Extract files to..." for more (or for a folder): only what is selected is extracted.</summary>
		public String ExtractItemsText => Loc.Get(SelectedItems is [{ Entry: not null }] ? "Str.ExtractFile" : "Str.ExtractFiles");

		private async Task ExtractSelectedItemsAsync(Object? parameter)
		{
			DatDocument? document = CurrentDocument;
			if (document == null)
				return;

			await ExtractAsync(document, CollectEntries(document, SelectedItems), String.Empty);
		}

		private async Task ExtractSelectedFolderAsync(Object? parameter)
		{
			if (SelectedFolder is { IsRoot: false } folder)
				await ExtractAsync(folder.Document, folder.Document.Archive.GetEntriesUnder(folder.Path), String.Empty);
		}

		private async Task ExtractAllAsync(Object? parameter)
		{
			if (CurrentDocument is { } document)
				await ExtractAsync(document, document.Archive.Entries.ToList(), String.Empty);
		}

		private List<DatEntry> CollectEntries(DatDocument document, IEnumerable<ListEntry> items)
		{
			List<DatEntry> entries = [];
			foreach (ListEntry item in items)
			{
				if (item.Entry != null)
				{
					entries.Add(item.Entry);
				}
				else if (item.Folder != null)
				{
					entries.AddRange(document.Archive.GetEntriesUnder(item.Folder.Path));
				}
			}

			return entries;
		}

		private async Task ExtractAsync(DatDocument document, IReadOnlyList<DatEntry> entries, String stripPrefix)
		{
			if (entries.Count == 0)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.NothingToExtract"), DialogIcon.Information);
				return;
			}

			// The folders of the DAT are kept, or every file goes straight in the destination; the last choice is the default one.
			Boolean flat = _settings.Data.ExtractFlat;
			DialogButton[] modes =
			[
				new("tree", Loc.Get("Str.ExtractKeepTree"), IsDefault: !flat),
				new("flat", Loc.Get("Str.ExtractFlat"), IsDefault: flat),
				new("cancel", Loc.Get("Str.Cancel"), IsCancel: true),
			];

			String? mode = _dialogs.Choose(Title, Loc.Get("Str.ExtractModeQuestion"), DialogIcon.Question, modes);
			if (mode is not ("tree" or "flat"))
				return;

			flat = mode == "flat";
			_settings.Data.ExtractFlat = flat;

			String? destination = _dialogs.PickFolder(Loc.Get("Str.SelectDestination"), _settings.Data.LastExtractFolder);
			if (destination == null)
				return;

			_settings.Data.LastExtractFolder = destination;

			// Without folders, the files that share a name get a suffix (a_1.txt): the names are decided for all the files, before the questions.
			Dictionary<DatEntry, String>? flatNames = flat ? DatArchive.GetFlatNames(entries) : null;
			List<DatEntry>? selected = ResolveOverwrites(entries, destination, stripPrefix, flatNames);
			if (selected == null)
				return;

			DatExtractResult? result = null;
			Boolean completed = await RunBusyAsync(
				Loc.Get("Str.Extracting"),
				true,
				(progress, token) => Task.Run(() => result = document.Archive.Extract(selected, destination, stripPrefix, progress, token, flatNames), token));

			if (completed && result != null)
			{
				StatusText = Loc.Format("Str.Extracted", result.Succeeded, selected.Count);
				ReportFailures(Loc.Format("Str.ExtractFailures", result.Failures.Count), result.Failures);
			}
		}

		/// <summary>
		/// Asks what to do with the files that already exist. Returns the files to extract, or null when cancelled.
		/// </summary>
		private List<DatEntry>? ResolveOverwrites(IReadOnlyList<DatEntry> entries, String destination, String stripPrefix, IReadOnlyDictionary<DatEntry, String>? flatNames)
		{
			List<DatEntry> selected = new(entries.Count);
			ConflictResolver resolver = new(_dialogs, Title);

			foreach (DatEntry entry in entries)
			{
				String target;
				try
				{
					target = DatArchive.GetExtractionPath(destination, entry, stripPrefix, flatNames?[entry]);
				}
				catch (DatException)
				{
					// Reported as a failure by the extraction itself.
					selected.Add(entry);
					continue;
				}

				if (!File.Exists(target))
				{
					selected.Add(entry);
					continue;
				}

				switch (resolver.Resolve(Loc.Format("Str.OverwriteFile", target)))
				{
					case ConflictDecision.Cancel:
						return null;
					case ConflictDecision.Replace:
						selected.Add(entry);
						break;
				}
			}

			return selected;
		}

		private void ReportFailures(String title, IReadOnlyList<String> failures)
		{
			if (failures.Count == 0)
				return;

			String list = String.Join(Environment.NewLine, failures.Take(MaxListedItems));
			if (failures.Count > MaxListedItems)
				list += Environment.NewLine + Loc.Format("Str.AndMore", failures.Count - MaxListedItems);

			_dialogs.ShowMessage(Title, title + Environment.NewLine + Environment.NewLine + list, DialogIcon.Warning);
		}

		internal async Task OpenItemAsync(ListEntry? item)
		{
			if (item == null)
				return;

			if (item.Folder != null)
			{
				SelectFolder(item.Folder);
				return;
			}

			// A folder always opens; a file opens only when the user wants a double click (or Enter) to do something.
			if (_settings.Data.DoubleClickDisabled)
				return;

			if (await OpenMessageProblemsInsteadAsync(item))
				return;

			if (_settings.Data.DoubleClickOpensViewer && FileViewers.IsViewable(item.Name))
			{
				await ViewEntryAsync(item.Entry!);
				return;
			}

			await OpenExternalAsync(item);
		}

		/// <summary>Opens a file with the program Windows associates with it (never a program or a script).</summary>
		private async Task OpenExternalAsync(ListEntry? item)
		{
			if (item?.Entry is not { } entry)
				return;

			DatDocument? document = CurrentDocument;
			if (document == null)
				return;

			if (FilePolicy.IsDangerous(entry.Name))
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.BlockedFile"), DialogIcon.Warning);
				return;
			}

			String file;
			if (entry.IsNew)
			{
				file = entry.SourceFile!;
			}
			else
			{
				String folder = _temp.Create("open");
				DatExtractResult? result = null;
				Boolean completed = true;

				if (entry.UnpackedSize > LargeFileBytes)
				{
					completed = await RunBusyAsync(
						Loc.Get("Str.Extracting"),
						true,
						(progress, token) => Task.Run(() => result = document.Archive.Extract([entry], folder, entry.Directory, progress, token), token));
				}
				else
				{
					result = document.Archive.Extract([entry], folder, entry.Directory);
				}

				if (!completed)
					return;

				if (result == null || result.Failures.Count > 0)
				{
					String key = entry.IsDamaged && entry.Problem != DatIssueKind.EntryCorrupt ? "Str.Err.OutsideArchive" : "Str.Err.CorruptData";
					_dialogs.ShowError(Loc.Format(key, entry.FullPath));
					return;
				}

				file = Path.Combine(folder, entry.Name);
			}

			Launch(file);
		}

		private void Launch(String file)
		{
			try
			{
				if (File.Exists(file))
					Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
			}
			catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
			}
		}
	}
}