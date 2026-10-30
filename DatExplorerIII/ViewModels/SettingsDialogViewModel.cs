namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using Infrastructure;
	using Models.Comparison;
	using Models.GameFiles;
	using Models.Interface;
	using Services.Interface;
	using Services.Platform;

	/// <summary>A kind of problem of the message files, which the user can turn off.</summary>
	public sealed class MsgCheckOption : ObservableObject
	{
		private Boolean _isEnabled;

		public MsgCheckOption(MsgIssueKind kind, String name, Boolean isEnabled)
		{
			Kind = kind;
			Name = name;
			_isEnabled = isEnabled;
		}

		public MsgIssueKind Kind { get; }

		public String Name { get; }

		public Boolean IsEnabled
		{
			get => _isEnabled;
			set => SetProperty(ref _isEnabled, value);
		}
	}

	/// <summary>An extension of file that the list of the files can show, with its box.</summary>
	public sealed class ExtensionOption : ObservableObject
	{
		private Boolean _isVisible;

		public ExtensionOption(String extension, Boolean isVisible)
		{
			Extension = extension;
			_isVisible = isVisible;
		}

		public String Extension { get; }

		public Boolean IsVisible
		{
			get => _isVisible;
			set => SetProperty(ref _isVisible, value);
		}
	}

	/// <summary>A reader that an extension can be given, with its name.</summary>
	public sealed record ReaderChoice(ViewerReader Reader, String Name);

	/// <summary>An extension that is given a reader, with the reader.</summary>
	public sealed class AssociationOption : ObservableObject
	{
		private ReaderChoice _reader;

		public AssociationOption(String extension, ReaderChoice reader)
		{
			Extension = extension;
			_reader = reader;
		}

		public String Extension { get; }

		public ReaderChoice Reader
		{
			get => _reader;
			set => SetProperty(ref _reader, value);
		}
	}

	/// <summary>An extension that has a limit of its own for the preview, in megabytes.</summary>
	public sealed class LimitOption : ObservableObject
	{
		private Int32 _megabytes;

		public LimitOption(String extension, Int32 megabytes)
		{
			Extension = extension;
			_megabytes = megabytes;
		}

		public String Extension { get; }

		public Int32 Megabytes
		{
			get => _megabytes;
			set => SetProperty(ref _megabytes, value);
		}
	}

	/// <summary>The kinds of problems of a severity.</summary>
	public sealed record MsgCheckGroup(String Title, IReadOnlyList<MsgCheckOption> Items);

	/// <summary>
	/// The values shown by the settings window while it is open: a copy of the settings, which the program takes when the
	/// user presses OK (and forgets when the user gives up).
	/// </summary>
	public sealed class SettingsDialogViewModel : ObservableObject
	{
		private Boolean _isTextMethod;
		private Boolean _showAllExtensions;
		private String _newExtension = String.Empty;
		private String _newLimitExtension = String.Empty;
		private Int32 _newLimitMegabytes;
		private ReaderChoice _newReader;

		public SettingsDialogViewModel(SettingsData data)
		{
			SingleInstance = data.SingleInstance;
			WindowFrameVisible = data.WindowFrameVisible;
			BlockSeparatorsVisible = data.BlockSeparatorsVisible;
			ShowHiddenFolders = data.ShowHiddenFolders;
			SaveWindowSize = data.SaveWindowSize;
			SaveWindowPosition = data.SaveWindowPosition;
			SaveBlockWidths = data.SaveBlockWidths;
			DoubleClickMessageProblems = data.DoubleClickMessageProblems;
			MessageChecks = Enum.GetValues<MsgIssueKind>()
				.GroupBy(MsgIssue.SeverityOf)
				.OrderBy(group => group.Key)
				.Select(group => new MsgCheckGroup(
					Loc.Get("Str.MsgSeverity" + group.Key),
					group.Select(kind => new MsgCheckOption(kind, Loc.Get("Str.MsgCheck" + kind), !data.DisabledMessageChecks.Contains(kind))).ToList()))
				.ToList();
			ShowSubtitles = data.ShowSubtitles;
			UnknownExtensionsAsText = data.UnknownExtensionsAsText;
			_showAllExtensions = data.ShowAllExtensions;
			HashSet<String> visible = new(data.VisibleExtensions ?? FileViewers.AllExtensions.ToList(), StringComparer.OrdinalIgnoreCase);
			Extensions = FileViewers.AllExtensions.Select(extension => new ExtensionOption(extension, visible.Contains(extension))).ToList();
			Readers = Enum.GetValues<ViewerReader>().Select(reader => new ReaderChoice(reader, Loc.Get("Str.Reader" + reader))).ToList();
			_newReader = Readers[0];
			Associations = new ObservableCollection<AssociationOption>(data.ExtensionReaders
				.OrderBy(pair => pair.Key, StringComparer.Ordinal)
				.Select(pair => new AssociationOption(pair.Key, Readers.First(choice => choice.Reader == pair.Value))));
			PreviewLimitMegabytes = data.PreviewLimitMegabytes;
			_newLimitMegabytes = PreviewLimitMegabytes;
			Limits = new ObservableCollection<LimitOption>(data.PreviewLimitsByExtension
				.Select(pair => new LimitOption(FileViewers.Normalize(pair.Key), pair.Value))
				.OrderBy(option => option.Extension, StringComparer.Ordinal));
			ScrollBarWidth = data.ScrollBarWidth;
			ScrollBarMinimumThumb = data.ScrollBarMinimumThumb;

			ComparisonOptions options = data.ComparisonOptions;
			IgnoreWhitespace = options.IgnoreWhitespace;
			IgnoreBlankLines = options.IgnoreBlankLines;
			IgnoreCase = options.IgnoreCase;
			IgnoreLineEndings = options.IgnoreLineEndings;
			IgnoreCodePage = options.IgnoreCodePage;
			IgnoreNumbers = options.IgnoreNumbers;
			IgnoreComments = options.IgnoreComments;
			IgnoreMissingFinalEol = options.IgnoreMissingFinalEol;
			_isTextMethod = options.Method == CompareMethod.Text;
			CompareSyncScroll = data.CompareSyncScroll;
		}

		public Boolean SingleInstance { get; set; }

		public Boolean WindowFrameVisible { get; set; }

		public Boolean BlockSeparatorsVisible { get; set; }

		public Boolean ShowHiddenFolders { get; set; }

		public Boolean SaveWindowSize { get; set; }

		public Boolean SaveWindowPosition { get; set; }

		public Boolean SaveBlockWidths { get; set; }

		public Boolean DoubleClickMessageProblems { get; set; }

		/// <summary>The kinds of problems of the message files, by severity, each with its box.</summary>
		public IReadOnlyList<MsgCheckGroup> MessageChecks { get; }

		/// <summary>The kinds of problems that are not looked for in the message files.</summary>
		public List<MsgIssueKind> DisabledMessageChecks()
		{
			return MessageChecks.SelectMany(group => group.Items).Where(option => !option.IsEnabled).Select(option => option.Kind).ToList();
		}

		public Boolean ShowSubtitles { get; set; }

		/// <summary>The files of an extension that is not known are shown as a text.</summary>
		public Boolean UnknownExtensionsAsText { get; set; }

		/// <summary>The list of the files shows every file, whatever its extension.</summary>
		public Boolean ShowAllExtensions
		{
			get => _showAllExtensions;
			set
			{
				if (SetProperty(ref _showAllExtensions, value))
					OnPropertyChanged(nameof(ExtensionsEditable));
			}
		}

		/// <summary>Whether the boxes of the extensions mean anything (they do not while every file is shown).</summary>
		public Boolean ExtensionsEditable => !_showAllExtensions;

		/// <summary>The extensions that the program knows, each with its box.</summary>
		public IReadOnlyList<ExtensionOption> Extensions { get; }

		/// <summary>The extensions that the list shows when it does not show all of them.</summary>
		public List<String> VisibleExtensionList()
		{
			return Extensions.Where(option => option.IsVisible).Select(option => option.Extension).ToList();
		}

		/// <summary>Checks or unchecks the box of every extension.</summary>
		public void SetAllExtensions(Boolean isVisible)
		{
			foreach (ExtensionOption option in Extensions)
			{
				option.IsVisible = isVisible;
			}
		}

		/// <summary>The readers that an extension can be given.</summary>
		public IReadOnlyList<ReaderChoice> Readers { get; }

		/// <summary>The extensions that are given a reader.</summary>
		public ObservableCollection<AssociationOption> Associations { get; }

		/// <summary>The extension that is typed, to be given a reader.</summary>
		public String NewExtension
		{
			get => _newExtension;
			set
			{
				if (SetProperty(ref _newExtension, value))
					OnPropertyChanged(nameof(CanAddAssociation));
			}
		}

		/// <summary>The reader that is chosen for the extension that is typed.</summary>
		public ReaderChoice NewReader
		{
			get => _newReader;
			set => SetProperty(ref _newReader, value);
		}

		/// <summary>An extension is typed (more than the dot).</summary>
		public Boolean CanAddAssociation => FileViewers.Normalize(_newExtension).Length > 1;

		/// <summary>Gives the typed extension the chosen reader (it replaces the one it had).</summary>
		public void AddAssociation()
		{
			String extension = FileViewers.Normalize(_newExtension);
			if (extension.Length < 2)
				return;

			AssociationOption? existing = Associations.FirstOrDefault(option => option.Extension == extension);
			if (existing != null)
			{
				existing.Reader = _newReader;
			}
			else
			{
				Int32 index = 0;
				while (index < Associations.Count && String.CompareOrdinal(Associations[index].Extension, extension) < 0)
				{
					index++;
				}

				Associations.Insert(index, new AssociationOption(extension, _newReader));
			}

			NewExtension = String.Empty;
		}

		public void RemoveAssociation(AssociationOption option)
		{
			Associations.Remove(option);
		}

		/// <summary>The readers chosen for the extensions.</summary>
		public Dictionary<String, ViewerReader> ExtensionReaderMap()
		{
			return Associations.ToDictionary(option => option.Extension, option => option.Reader.Reader);
		}

		/// <summary>The size in megabytes above which a file is not read for a preview.</summary>
		public Int32 PreviewLimitMegabytes { get; set; }

		/// <summary>The extensions that have a limit of their own.</summary>
		public ObservableCollection<LimitOption> Limits { get; }

		/// <summary>The extension that is typed, to be given a limit.</summary>
		public String NewLimitExtension
		{
			get => _newLimitExtension;
			set
			{
				if (SetProperty(ref _newLimitExtension, value))
					OnPropertyChanged(nameof(CanAddLimit));
			}
		}

		/// <summary>The limit that is typed for the extension.</summary>
		public Int32 NewLimitMegabytes
		{
			get => _newLimitMegabytes;
			set => SetProperty(ref _newLimitMegabytes, value);
		}

		public Boolean CanAddLimit => FileViewers.Normalize(_newLimitExtension).Length > 1;

		/// <summary>Gives the typed extension the typed limit (it replaces the one it had).</summary>
		public void AddLimit()
		{
			String extension = FileViewers.Normalize(_newLimitExtension);
			if (extension.Length < 2)
				return;

			Int32 megabytes = PreviewLimits.Clamp(_newLimitMegabytes);
			LimitOption? existing = Limits.FirstOrDefault(option => option.Extension == extension);
			if (existing != null)
			{
				existing.Megabytes = megabytes;
			}
			else
			{
				Int32 index = 0;
				while (index < Limits.Count && String.CompareOrdinal(Limits[index].Extension, extension) < 0)
				{
					index++;
				}

				Limits.Insert(index, new LimitOption(extension, megabytes));
			}

			NewLimitExtension = String.Empty;
		}

		public void RemoveLimit(LimitOption option)
		{
			Limits.Remove(option);
		}

		/// <summary>The limits chosen for the extensions.</summary>
		public Dictionary<String, Int32> LimitMap()
		{
			return Limits.ToDictionary(option => option.Extension, option => PreviewLimits.Clamp(option.Megabytes), StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>The width of the scroll bars (a slider of the window gives a number with a fraction).</summary>
		public Double ScrollBarWidth { get; set; }

		/// <summary>The smallest length of the slider of a scroll bar.</summary>
		public Double ScrollBarMinimumThumb { get; set; }

		public Boolean IgnoreWhitespace { get; set; }

		public Boolean IgnoreBlankLines { get; set; }

		public Boolean IgnoreCase { get; set; }

		public Boolean IgnoreLineEndings { get; set; }

		public Boolean IgnoreCodePage { get; set; }

		public Boolean IgnoreNumbers { get; set; }

		public Boolean IgnoreComments { get; set; }

		public Boolean IgnoreMissingFinalEol { get; set; }

		public Boolean CompareSyncScroll { get; set; }

		/// <summary>The files made of text are compared as text (and the options above apply to them).</summary>
		public Boolean IsTextMethod
		{
			get => _isTextMethod;
			set
			{
				if (SetProperty(ref _isTextMethod, value))
				{
					OnPropertyChanged(nameof(IsBinaryMethod));
					OnPropertyChanged(nameof(IgnoresApply));
				}
			}
		}

		/// <summary>Every file is compared byte for byte.</summary>
		public Boolean IsBinaryMethod
		{
			get => !_isTextMethod;
			set => IsTextMethod = !value;
		}

		/// <summary>Whether the options about the text mean anything (they do not for a comparison byte for byte).</summary>
		public Boolean IgnoresApply => _isTextMethod;

		/// <summary>What the comparison ignores, as chosen.</summary>
		public ComparisonOptions ToComparisonOptions()
		{
			return new ComparisonOptions
			{
				Method = _isTextMethod ? CompareMethod.Text : CompareMethod.Binary,
				IgnoreWhitespace = IgnoreWhitespace,
				IgnoreBlankLines = IgnoreBlankLines,
				IgnoreCase = IgnoreCase,
				IgnoreLineEndings = IgnoreLineEndings,
				IgnoreCodePage = IgnoreCodePage,
				IgnoreNumbers = IgnoreNumbers,
				IgnoreComments = IgnoreComments,
				IgnoreMissingFinalEol = IgnoreMissingFinalEol,
			};
		}
	}
}