namespace DatExplorerIII.ViewModels
{
	using System;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>
	/// A line of the file list: a sub folder or a file of the current folder.
	/// </summary>
	public sealed class ListEntry : ObservableObject
	{
		private MsgFileReport? _messages;

		public ListEntry(FolderNode folder)
		{
			Folder = folder;
			Name = folder.Name;
			Glyph = "\uF12B";
			TypeText = Loc.Get("Str.TypeFolder");
			SizeText = String.Empty;
			PackedSizeText = String.Empty;
		}

		/// <param name="entry">The file.</param>
		/// <param name="fromFolder">True when the archive is made from a folder: all its files come from the disk, so none is marked as new.</param>
		public ListEntry(DatEntry entry, Boolean fromFolder = false)
		{
			Boolean isNew = entry.IsNew && !fromFolder;
			Entry = entry;
			Name = entry.Name;
			IsDamaged = entry.IsDamaged;
			Glyph = entry.IsDamaged ? "\uE7BA" : isNew ? "\uE8A5" : "\uE7C3";
			TypeText = entry.IsDamaged ? Loc.Get("Str.TypeDamaged")
				: isNew ? Loc.Get("Str.TypeNew")
				: entry.IsCompressed ? Loc.Get("Str.TypeCompressed")
				: String.Empty;
			SizeText = SizeFormatter.Format(entry.UnpackedSize);
			PackedSizeText = entry.IsCompressed ? SizeFormatter.Format(entry.PackedSize) : String.Empty;
		}

		public FolderNode? Folder { get; }

		public DatEntry? Entry { get; }

		public Boolean IsFolder => Folder != null;

		public String Name { get; }

		public String Glyph { get; }

		/// <summary>True for a file whose data is known to be unusable.</summary>
		public Boolean IsDamaged { get; }

		public String TypeText { get; }

		public String SizeText { get; }

		public String PackedSizeText { get; }

		/// <summary>True for a file of messages (.msg), which the program checks.</summary>
		public Boolean IsMessageFile => Entry != null && !IsDamaged && MsgScanner.IsMsgName(Name);

		/// <summary>What the check of the message file found; null until it was checked (or when this is not a message file).</summary>
		public MsgFileReport? Messages => _messages;

		/// <summary>True when the check found something: the list shows a warning.</summary>
		public Boolean HasMessageIssues => _messages is { HasIssues: true };

		/// <summary>The worst problem of the file, which tells the color of the warning.</summary>
		public MsgIssueSeverity? MessageSeverity => _messages?.Worst;

		/// <summary>What the warning says: how many problems, of which severity.</summary>
		public String MessageTip => _messages is { HasIssues: true } report ? MsgIssueSummary.Describe(report.Issues) : String.Empty;

		/// <summary>Gives the result of the check of the message file.</summary>
		public void SetMessages(MsgFileReport report)
		{
			_messages = report;
			OnPropertyChanged(nameof(Messages));
			OnPropertyChanged(nameof(HasMessageIssues));
			OnPropertyChanged(nameof(MessageSeverity));
			OnPropertyChanged(nameof(MessageTip));
		}
	}
}