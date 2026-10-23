namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
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

	/// <summary>The kinds of problems of a severity.</summary>
	public sealed record MsgCheckGroup(String Title, IReadOnlyList<MsgCheckOption> Items);

	/// <summary>
	/// The values shown by the settings window while it is open: a copy of the settings, which the program takes when the
	/// user presses OK (and forgets when the user gives up).
	/// </summary>
	public sealed class SettingsDialogViewModel : ObservableObject
	{
		private Boolean _isTextMethod;

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