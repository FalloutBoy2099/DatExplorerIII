namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.ObjectModel;
	using Core;
	using Infrastructure;
	using Models;

	/// <summary>
	/// A folder of the tree. The root node of a tree is the DAT archive itself.
	/// </summary>
	public sealed class FolderNode : ObservableObject
	{
		private String _name;
		private String _path;
		private Boolean _isExpanded;
		private Boolean _isSelected;

		public FolderNode(DatDocument document, FolderNode? parent, String path, String name)
		{
			Document = document;
			Parent = parent;
			_path = path;
			_name = name;

			if (parent == null)
			{
				// The warning sign of the archive follows what the archive reports.
				document.PropertyChanged += (_, e) =>
				{
					if (e.PropertyName == nameof(DatDocument.HasIssues))
					{
						OnPropertyChanged(nameof(HasIssues));
					}
				};
			}
		}

		public DatDocument Document { get; }

		public FolderNode? Parent { get; }

		public ObservableCollection<FolderNode> Children { get; } = new();

		public Boolean IsRoot => Parent == null;

		/// <summary>Folders are never damaged: only the files they hold can be (the icon style is shared with the list).</summary>
		public Boolean IsDamaged => false;

		/// <summary>True for the archive node when problems were found in the archive.</summary>
		public Boolean HasIssues => IsRoot && Document.HasIssues;

		/// <summary>Used to color the icon: every node but the archive itself is a folder.</summary>
		public Boolean IsFolder => Parent != null;

		/// <summary>Folder path in the archive: original case, trailing backslash, empty for the root.</summary>
		public String Path
		{
			get => _path;
			set => SetProperty(ref _path, value);
		}

		public String Key => _path.ToLowerInvariant();

		/// <summary>Name of the folder, or of the archive file for the root.</summary>
		public String Name
		{
			get => _name;
			set
			{
				if (SetProperty(ref _name, value))
				{
					OnPropertyChanged(nameof(DisplayName));
				}
			}
		}

		public String DisplayName
		{
			get
			{
				if (!IsRoot)
				{
					return _name;
				}

				String version = Document.Archive.IsFolderBacked ? "DIR" : Document.Archive.Format switch
				{
					DatFormat.Fallout2 => "F2",
					DatFormat.Arcanum => "AR",
					DatFormat.Toee => "TE",
					DatFormat.Zip => "ZIP",
					_ => "F1",
				};
				return $"[{version}] {System.IO.Path.GetFileName(Document.FilePath)}";
			}
		}

		public String ToolTip => IsRoot ? Document.FilePath : _path;

		/// <summary>The root shows the name and the kind of its archive: they change when the archive is saved under another name.</summary>
		public void RefreshCaption()
		{
			if (IsRoot)
			{
				Name = System.IO.Path.GetFileName(Document.FilePath);
				OnPropertyChanged(nameof(DisplayName));
				OnPropertyChanged(nameof(ToolTip));
			}
		}

		public String Glyph => IsRoot ? "\uE7B8" : "\uF12B";


		public Boolean IsExpanded
		{
			get => _isExpanded;
			set => SetProperty(ref _isExpanded, value);
		}

		public Boolean IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		public void ExpandParents()
		{
			for (FolderNode? node = Parent; node != null; node = node.Parent)
			{
				node.IsExpanded = true;
			}
		}
	}
}