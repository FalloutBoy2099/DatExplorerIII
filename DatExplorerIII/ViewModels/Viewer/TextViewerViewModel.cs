namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows.Input;
	using Formats.GameFiles;
	using Infrastructure;
	using Models.GameFiles;
	using Services.Checks;
	using Services.Interface;
	using Services.Syntax;
	using Services.Texts;

	/// <summary>
	/// A text shown in the viewer, with the choice of the encoding it is read with.
	/// </summary>
	public sealed class TextViewerViewModel : ObservableObject
	{
		/// <summary>
		/// The longest text that is colored, in characters (the viewer reads at most 4 MB of a file). The viewer draws only the lines
		/// of a colored text that can be seen, so a long one shows as fast as a short one.
		/// </summary>
		public const Int32 MaxColoredLength = 4 * 1024 * 1024;

		/// <summary>The most parts a colored text can have: they are kept in memory as long as the text is shown.</summary>
		public const Int32 MaxColoredParts = 500_000;

		private Byte[] _data = Array.Empty<Byte>();
		private Int64 _fullSize;
		private IReadOnlyList<TextEncodingOption> _options;
		private TextEncodingOption _selectedEncoding;
		private String _text = String.Empty;
		private String _plainText = String.Empty;
		private String _status = String.Empty;
		private String _notice = String.Empty;
		private Boolean _wrapLines = true;
		private Boolean _isMessageFile;
		private Action<MsgFileReport>? _problemsRequested;
		private Boolean _isScript;
		private Boolean _isBiography;
		private Boolean _isList;
		private Boolean _mayBeIni;
		private Boolean _isLua;
		private readonly IUiThread _uiThread;
		private Boolean _tooLongForColors;
		private String _note = String.Empty;
		private String? _noteTip;
		private IReadOnlySet<Int32>? _missingLines;
		private String? _noticeTip;
		private String _findText = String.Empty;
		private Int32 _cursor;
		private Int32 _revealSerial;
		private TextReveal? _reveal;

		/// <summary>True while the list of encodings is replaced: the box that shows it then empties its selection, which must not be taken for a choice.</summary>
		private Boolean _refreshingLanguage;
		private IReadOnlyList<SyntaxSpan>? _syntaxSpans;

		public TextViewerViewModel(IUiThread uiThread)
		{
			_uiThread = uiThread;
			_options = TextEncodings.CreateOptions();
			_selectedEncoding = _options[0];
			CopyCommand = new RelayCommand(Copy, () => _text.Length > 0);
			FindCommand = new RelayCommand(Find, () => CanSearch);
			FindNextCommand = new RelayCommand(() => FindFrom(_findText), () => CanSearch && _findText.Length > 0);
			GoToLineCommand = new RelayCommand(GoToLine, () => CanSearch);
			GoToIdCommand = new RelayCommand(GoToId, () => CanSearch && _isMessageFile);
			ShowProblemsCommand = new RelayCommand(
				() => _problemsRequested?.Invoke(new MsgFileReport(SourcePath, MsgValidator.Validate(_text))),
				() => CanShowProblems);
		}

		public ICommand CopyCommand { get; }

		public IReadOnlyList<TextEncodingOption> Options
		{
			get => _options;
			private set => SetProperty(ref _options, value);
		}

		/// <summary>The encoding the text is read with; changing it reads the same bytes again.</summary>
		public TextEncodingOption SelectedEncoding
		{
			get => _selectedEncoding;
			set
			{
				if (_refreshingLanguage || value is null)
					return;

				if (SetProperty(ref _selectedEncoding, value) && _data.Length > 0)
					Decode();
			}
		}

		public String Text
		{
			get => _text;
			private set => SetProperty(ref _text, value);
		}

		/// <summary>
		/// The text for the plain text box: the text when it has no colors, nothing when it has some (the colored text is shown by
		/// another box, and a text box that holds a long text, even hidden, takes seconds to make).
		/// </summary>
		public String PlainText
		{
			get => _plainText;
			private set => SetProperty(ref _plainText, value);
		}

		/// <summary>The encoding, the size and the number of lines.</summary>
		public String Status
		{
			get => _status;
			private set => SetProperty(ref _status, value);
		}

		/// <summary>The kinds of problems of a message file that its notice does not mention (the next text that is shown).</summary>
		public IReadOnlyCollection<MsgIssueKind> DisabledMessageChecks { get; set; } = Array.Empty<MsgIssueKind>();

		/// <summary>Where the text comes from (a path of an archive): the name of the file in the report of its problems.</summary>
		public String SourcePath { get; set; } = String.Empty;

		/// <summary>What the button of the problems does with the report of the message file that is shown (nothing: no button).</summary>
		public Action<MsgFileReport>? ProblemsRequested
		{
			get => _problemsRequested;
			set
			{
				if (SetProperty(ref _problemsRequested, value))
					OnPropertyChanged(nameof(CanShowProblems));
			}
		}

		/// <summary>The viewer shows a message file and can list its problems: the button is there.</summary>
		public Boolean CanShowProblems => _isMessageFile && _problemsRequested != null;

		/// <summary>Lists the problems of the message file that is shown.</summary>
		public ICommand ShowProblemsCommand { get; }

		/// <summary>Asks for a text to find.</summary>
		public ICommand FindCommand { get; }

		/// <summary>Goes to the next place of the text that was asked for (and starts again from the top after the last one).</summary>
		public ICommand FindNextCommand { get; }

		public ICommand GoToLineCommand { get; }

		/// <summary>Goes to a message of a message file by its number.</summary>
		public ICommand GoToIdCommand { get; }

		/// <summary>The text is a message file: it can be searched by the number of a message.</summary>
		public Boolean IsMessageFile => _isMessageFile;

		/// <summary>Asks the user for a text: the title, what is asked, the value to start with, and what tells if a value is acceptable (the message of the error, or null). Null: the user gave up.</summary>
		public Func<String, String, String, Func<String, String?>?, String?>? Ask { get; set; }

		/// <summary>Tells the user something (a search that found nothing).</summary>
		public Action<String>? Inform { get; set; }

		/// <summary>The part of the text that the view selects and scrolls to; changes each time a search or a go to has an answer.</summary>
		public TextReveal? Reveal
		{
			get => _reveal;
			private set => SetProperty(ref _reveal, value);
		}

		private Boolean CanSearch => Ask != null && _text.Length > 0;

		private void Find()
		{
			String? answer = Ask?.Invoke(Loc.Get("Str.TextFind").TrimEnd('.'), Loc.Get("Str.TextFindLabel"), _findText, null);
			if (String.IsNullOrEmpty(answer))
				return;

			_findText = answer;
			FindFrom(answer);
		}

		/// <summary>Finds a text from the place where the last answer ended, and from the top when there is none after it.</summary>
		private void FindFrom(String needle)
		{
			Int32 index = _text.IndexOf(needle, Math.Min(_cursor, _text.Length), StringComparison.OrdinalIgnoreCase);
			if (index < 0 && _cursor > 0)
				index = _text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);

			if (index < 0)
			{
				Inform?.Invoke(Loc.Get("Str.TextNotFound"));
				return;
			}

			RevealPart(index, needle.Length);
		}

		private void GoToLine()
		{
			Int32 lines = 1 + _text.Count(character => character == '\n');
			String? answer = Ask?.Invoke(
				Loc.Get("Str.TextGoToLine").TrimEnd('.'),
				Loc.Format("Str.TextGoToLineLabel", lines),
				"1",
				value => Int32.TryParse(value, NumberStyles.None, CultureInfo.CurrentCulture, out Int32 number) && number >= 1 && number <= lines ? null : Loc.Format("Str.TextLineRange", lines));
			if (answer == null || !Int32.TryParse(answer, NumberStyles.None, CultureInfo.CurrentCulture, out Int32 line))
				return;

			Int32 start = 0;
			for (Int32 skipped = 1; skipped < line; skipped++)
			{
				start = _text.IndexOf('\n', start) + 1;
			}

			Int32 end = _text.IndexOf('\n', start);
			Int32 length = (end < 0 ? _text.Length : end) - start;
			if (length > 0 && _text[start + length - 1] == '\r')
				length--;

			RevealPart(start, length);
		}

		private void GoToId()
		{
			String? answer = Ask?.Invoke(
				Loc.Get("Str.TextGoToId").TrimEnd('.'),
				Loc.Get("Str.TextGoToIdLabel"),
				String.Empty,
				value => UInt32.TryParse(value, NumberStyles.None, CultureInfo.CurrentCulture, out _) ? null : Loc.Get("Str.TextIdInvalid"));
			if (answer == null || !UInt32.TryParse(answer, NumberStyles.None, CultureInfo.CurrentCulture, out UInt32 id))
				return;

			// The number of a message is the first part of a line: {id}{sound}{text} (a number may have leading zeros).
			Match match = Regex.Match(_text, @"^[ \t]*(?<id>\{[ \t]*0*" + id.ToString(CultureInfo.InvariantCulture) + @"[ \t]*\})", RegexOptions.Multiline | RegexOptions.CultureInvariant);
			if (!match.Success)
			{
				Inform?.Invoke(Loc.Get("Str.TextIdNotFound"));
				return;
			}

			RevealPart(match.Groups["id"].Index, match.Groups["id"].Length);
		}

		private void RevealPart(Int32 start, Int32 length)
		{
			_cursor = start + Math.Max(length, 1);
			Reveal = new TextReveal(start, length, ++_revealSerial);
		}

		/// <summary>A remark about the text: it is cut, it looks binary, it is empty.</summary>
		public String Notice
		{
			get => _notice;
			private set => SetProperty(ref _notice, value);
		}

		/// <summary>More about the remark (the files that a list does not find...); null when there is nothing more.</summary>
		public String? NoticeTip
		{
			get => _noticeTip;
			private set => SetProperty(ref _noticeTip, value);
		}

		public Boolean WrapLines
		{
			get => _wrapLines;
			set => SetProperty(ref _wrapLines, value);
		}

		/// <summary>
		/// The parts of the text that get a color; null when the text is shown plainly (only message files, .msg, scripts, .ssl and
		/// .h, biographies, .bio, and lists, .lst, are colored, and not when they are very large or look binary). It is set once the text is.
		/// </summary>
		public IReadOnlyList<SyntaxSpan>? SyntaxSpans
		{
			get => _syntaxSpans;
			private set
			{
				if (SetProperty(ref _syntaxSpans, value))
					OnPropertyChanged(nameof(IsHighlighted));
			}
		}

		public Boolean IsHighlighted => _syntaxSpans != null;

		/// <param name="data">The bytes to show (the beginning of the file when it is large).</param>
		/// <param name="fullSize">The size of the whole file.</param>
		/// <param name="extension">The extension of the file (with the dot): it tells if the text gets colors.</param>
		/// <param name="note">Something to say about the text when there is nothing else to say (where it comes from).</param>
		/// <param name="list">The check of a list of files (.lst): its notice says what is wrong, and the lines that name a missing file are marked.</param>
		public void Show(Byte[] data, Int64 fullSize, String? extension = null, String? note = null, LstReport? list = null)
		{
			_data = data;
			_fullSize = fullSize;
			_isMessageFile = String.Equals(extension, ".msg", StringComparison.OrdinalIgnoreCase);
			_cursor = 0;
			OnPropertyChanged(nameof(IsMessageFile));
			OnPropertyChanged(nameof(CanShowProblems));
			_isScript = String.Equals(extension, ".ssl", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".h", StringComparison.OrdinalIgnoreCase);
			_isBiography = String.Equals(extension, ".bio", StringComparison.OrdinalIgnoreCase);
			_isList = String.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
			_isLua = String.Equals(extension, ".lua", StringComparison.OrdinalIgnoreCase);
			_mayBeIni = extension != null && (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) || extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cfg", StringComparison.OrdinalIgnoreCase));
			_note = note ?? String.Empty;
			_noteTip = null;
			_missingLines = null;
			if (list is { HasProblems: true })
			{
				_note = ListNotice(list);
				_noteTip = ListTip(list);
				_missingLines = list.MissingLines;
			}

			Decode();
		}

		/// <summary>"2 missing files, 5 files not listed".</summary>
		private static String ListNotice(LstReport list)
		{
			List<String> parts = [];
			if (list.Missing.Count > 0)
				parts.Add(Loc.Plural("Str.PluralLstMissing", list.Missing.Count));

			if (list.Unlisted.Count > 0)
				parts.Add(Loc.Plural("Str.PluralLstUnlisted", list.Unlisted.Count));

			return String.Join(Loc.Get("Str.MsgListSeparator"), parts);
		}

		/// <summary>The names of the files, a few lines of each kind.</summary>
		private static String ListTip(LstReport list)
		{
			const Int32 limit = 25;
			List<String> lines = [];
			if (list.Missing.Count > 0)
			{
				lines.Add(Loc.Get("Str.LstTipMissing"));
				lines.AddRange(list.Missing.Take(limit).Select(file => "  " + file.Name + " (" + file.Line + ")"));
				if (list.Missing.Count > limit)
					lines.Add("  …");
			}

			if (list.Unlisted.Count > 0)
			{
				lines.Add(Loc.Get("Str.LstTipUnlisted"));
				lines.AddRange(list.Unlisted.Take(limit).Select(file => "  " + file));
				if (list.Unlisted.Count > limit)
					lines.Add("  …");
			}

			return String.Join(Environment.NewLine, lines);
		}

		public void Clear()
		{
			_data = Array.Empty<Byte>();
			_fullSize = 0;
			_isMessageFile = false;
			_cursor = 0;
			OnPropertyChanged(nameof(IsMessageFile));
			OnPropertyChanged(nameof(CanShowProblems));
			_isScript = false;
			_isBiography = false;
			_isList = false;
			_isLua = false;
			_mayBeIni = false;
			_note = String.Empty;
			_noteTip = null;
			_missingLines = null;
			SyntaxSpans = null;
			Text = String.Empty;
			PlainText = String.Empty;
			Status = String.Empty;
			Notice = String.Empty;
			NoticeTip = null;
		}

		/// <summary>Names the encodings again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			Int32 codePage = _selectedEncoding.CodePage;
			IReadOnlyList<TextEncodingOption> options = TextEncodings.CreateOptions();

			// The box that shows the list gives up its selection when the list is replaced, and tells so: that is not a choice.
			_refreshingLanguage = true;
			try
			{
				_selectedEncoding = options.First(option => option.CodePage == codePage);
				Options = options;
			}
			finally
			{
				_refreshingLanguage = false;
			}

			OnPropertyChanged(nameof(SelectedEncoding));

			if (_data.Length > 0)
				Decode();
		}

		private void Decode()
		{
			DecodedText decoded = TextDecoder.Decode(_data, _selectedEncoding.CodePage);
			Text = decoded.Text;
			SyntaxSpans = Colorize(decoded);
			PlainText = SyntaxSpans == null ? decoded.Text : String.Empty;

			String encoding = decoded.IsDetected ? Loc.Format("Str.EncodingDetected", decoded.EncodingName) : decoded.EncodingName;
			Int32 lines = 1 + decoded.Text.Count(character => character == '\n');
			Status = Loc.Format("Str.ViewerTextStatus", encoding, SizeFormatter.Format(_fullSize), lines.ToString("N0", CultureInfo.CurrentCulture));

			NoticeTip = null;
			if (decoded.LooksBinary)
			{
				Notice = Loc.Get("Str.ViewerBinary");
			}
			else if (_data.Length < _fullSize)
			{
				Notice = Loc.Format("Str.ViewerTruncated", SizeFormatter.Format(_data.Length));
			}
			else if (_data.Length == 0)
			{
				Notice = Loc.Get("Str.ViewerEmpty");
			}
			else
			{
				Notice = _tooLongForColors ? Loc.Get("Str.ViewerNotColored") : _isMessageFile ? MessageNotice(decoded.Text) : _note;
				NoticeTip = _isList && !_tooLongForColors ? _noteTip : null;
			}
		}

		/// <summary>What the notice of a message file says: how many problems it has, when it has some.</summary>
		private String MessageNotice(String text)
		{
			List<MsgIssue> issues = MsgValidator.Validate(text).Where(issue => !DisabledMessageChecks.Contains(issue.Kind)).ToList();
			if (issues.Count == 0)
				return _note;

			return MsgIssueSummary.Describe(issues);
		}

		/// <summary>
		/// The parts of the text that get a color, for the kinds of file that have colors. A text with too many parts is not colored
		/// (a run is made for each, and tens of thousands of them take seconds to lay out): it is shown plainly and the notice says so.
		/// </summary>
		private IReadOnlyList<SyntaxSpan>? Colorize(DecodedText decoded)
		{
			_tooLongForColors = false;
			Func<String, IReadOnlyList<SyntaxSpan>>? tokenize = _isMessageFile ? MsgSyntax.Tokenize : _isScript ? SslSyntax.Tokenize : _isBiography ? BioSyntax.Tokenize : _isLua ? LuaSyntax.Tokenize : _isList ? (text => LstSyntax.Tokenize(text, _missingLines)) : null;

			// Some text files are configurations in the INI form: the content tells, not the extension.
			if (tokenize == null && _mayBeIni && !decoded.LooksBinary && decoded.Text.Length <= MaxColoredLength && IniSyntax.LooksLikeIni(decoded.Text))
			{
				tokenize = IniSyntax.Tokenize;
			}
			else if (tokenize == null && _mayBeIni && !decoded.LooksBinary && decoded.Text.Length <= MaxColoredLength && HashCommentSyntax.HasComments(decoded.Text))
			{
				// Another text file with comments: the lines that start with a # (the other texts are not colored).
				tokenize = HashCommentSyntax.Tokenize;
			}

			if (decoded.LooksBinary || tokenize == null)
				return null;

			if (decoded.Text.Length > MaxColoredLength)
			{
				_tooLongForColors = true;
				return null;
			}

			IReadOnlyList<SyntaxSpan> spans = tokenize(decoded.Text);
			if (spans.Count > MaxColoredParts)
			{
				_tooLongForColors = true;
				return null;
			}

			return spans;
		}

		private void Copy()
		{
			_uiThread.SetClipboardText(_text);
		}
	}
}