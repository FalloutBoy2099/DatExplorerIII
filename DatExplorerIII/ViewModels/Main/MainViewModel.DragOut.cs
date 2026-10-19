namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Core;
	using Models;
	using Services;

	public sealed partial class MainViewModel
	{
		/// <summary>
		/// Extracts the selected items to a scratch folder so that they can be dropped in the file explorer.
		/// Returns the files and folders to drop, or null when there is nothing to drag.
		/// </summary>
		public IReadOnlyList<String>? PrepareDragOut(IReadOnlyList<ListEntry> items)
		{
			FolderNode? folder = SelectedFolder;
			if (folder == null || items.Count == 0)
			{
				return null;
			}

			List<DatEntry> entries = CollectEntries(folder.Document, items).ToList();
			List<String> top = items.Select(item => item.Name).ToList();
			return ExtractForDrag(folder.Document, entries, folder.Path, top);
		}

		public IReadOnlyList<String>? PrepareDragOut(FolderNode node)
		{
			if (node.IsRoot)
			{
				return null;
			}

			String parentPath = node.Parent?.Path ?? String.Empty;
			return ExtractForDrag(node.Document, node.Document.Archive.GetEntriesUnder(node.Path).ToList(), parentPath, new List<String> { node.Name });
		}

		private IReadOnlyList<String>? ExtractForDrag(DatDocument document, List<DatEntry> entries, String stripPrefix, List<String> topLevelNames)
		{
			if (entries.Count == 0 || Busy.IsBusy)
			{
				return null;
			}

			if (entries.Sum(entry => (Int64)entry.UnpackedSize) > MaxDragBytes)
			{
				StatusText = Loc.Get("Str.DragTooLarge");
				return null;
			}

			String folder = _temp.Create("drag");
			DatExtractResult result = document.Archive.Extract(entries, folder, stripPrefix);
			StatusText = Loc.Format("Str.Extracted", result.Succeeded, entries.Count);

			List<String> paths = topLevelNames.Select(name => Path.Combine(folder, name)).Where(path => File.Exists(path) || Directory.Exists(path)).ToList();
			return paths.Count > 0 ? paths : null;
		}
	}
}