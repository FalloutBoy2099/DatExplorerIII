namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Core;
	using Infrastructure;
	using Models.Archives;

	/// <summary>
	/// An open DAT archive and the folder tree shown for it.
	/// </summary>
	public sealed class DatDocument : ObservableObject
	{
		private readonly Dictionary<String, FolderNode> _nodes = new(StringComparer.Ordinal);

		public DatDocument(DatArchive archive)
		{
			Archive = archive;
			Root = new FolderNode(this, null, String.Empty, Path.GetFileName(archive.FilePath));
			_nodes[String.Empty] = Root;
			SyncTree();
		}

		public DatArchive Archive { get; }

		public FolderNode Root { get; }

		public String FilePath => Archive.FilePath;

		public Boolean ShouldSave => Archive.PendingChanges != DatPendingChanges.None;

		/// <summary>The quick save only exists for Fallout 2 archives that are already stored on disk.</summary>
		public Boolean CanQuickSave => ShouldSave && Archive.Format == DatFormat.Fallout2 && Archive.HasBackingFile && !Archive.RequiresRebuild;

		/// <summary>True when problems were found in the archive (damaged files, a recovered directory...).</summary>
		public Boolean HasIssues => Archive.Issues.Count > 0;

		public Int32 DamagedCount => Archive.DamagedCount;

		/// <summary>Updates the tree and the state after the archive changed.</summary>
		public void Refresh()
		{
			Root.RefreshCaption();
			SyncTree();
			OnPropertyChanged(nameof(FilePath));
			OnPropertyChanged(nameof(ShouldSave));
			OnPropertyChanged(nameof(CanQuickSave));
			OnPropertyChanged(nameof(HasIssues));
			OnPropertyChanged(nameof(DamagedCount));
		}

		public FolderNode? FindNode(String path)
		{
			_nodes.TryGetValue(DatArchive.NormalizeDirectory(path).ToLowerInvariant(), out FolderNode? node);
			return node;
		}

		/// <summary>
		/// Renames a folder and returns its node afterwards. Nodes that were expanded stay expanded.
		/// </summary>
		public FolderNode RenameFolder(FolderNode node, String newName)
		{
			String oldKey = node.Key;
			List<String> expanded = _nodes
				.Where(pair => pair.Key.StartsWith(oldKey, StringComparison.Ordinal) && pair.Value.IsExpanded)
				.Select(pair => pair.Key)
				.ToList();

			String newPath = Archive.RenameFolder(node.Path, newName);
			Refresh();

			foreach (String key in expanded)
			{
				String newKey = newPath.ToLowerInvariant() + key.Substring(oldKey.Length);
				if (_nodes.TryGetValue(newKey, out FolderNode? renamed))
					renamed.IsExpanded = true;
			}

			return _nodes[newPath.ToLowerInvariant()];
		}

		/// <summary>
		/// Makes the nodes match the folders of the archive, keeping the existing nodes (and their state) when possible.
		/// </summary>
		private void SyncTree()
		{
			Dictionary<String, String> folders = new(StringComparer.Ordinal);
			foreach (String path in Archive.Folders)
			{
				folders[path.ToLowerInvariant()] = path;
			}

			foreach (String key in _nodes.Keys.Where(key => key.Length > 0 && !folders.ContainsKey(key)).ToList())
			{
				FolderNode removed = _nodes[key];
				removed.Parent?.Children.Remove(removed);
				_nodes.Remove(key);
			}

			// Shorter paths first: a parent always exists before its children.
			foreach (KeyValuePair<String, String> folder in folders.OrderBy(pair => pair.Key.Length))
			{
				String name = GetName(folder.Value);

				if (_nodes.TryGetValue(folder.Key, out FolderNode? node))
				{
					node.Path = folder.Value;
					node.Name = name;
					continue;
				}

				String parentKey = folder.Key.Substring(0, folder.Key.TrimEnd('\\').LastIndexOf('\\') + 1);
				FolderNode parent = _nodes[parentKey];
				node = new FolderNode(this, parent, folder.Value, name);
				_nodes[folder.Key] = node;
				InsertSorted(parent, node);
			}
		}

		private static String GetName(String path)
		{
			String trimmed = path.TrimEnd('\\');
			return trimmed.Substring(trimmed.LastIndexOf('\\') + 1);
		}

		private static void InsertSorted(FolderNode parent, FolderNode node)
		{
			Int32 index = 0;
			while (index < parent.Children.Count && String.Compare(parent.Children[index].Name, node.Name, StringComparison.OrdinalIgnoreCase) < 0)
			{
				index++;
			}

			parent.Children.Insert(index, node);
		}
	}
}