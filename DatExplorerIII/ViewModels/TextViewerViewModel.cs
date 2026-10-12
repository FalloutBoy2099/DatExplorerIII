namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>
	/// A text shown in the viewer, with the choice of the encoding it is read with.
	/// </summary>
	public sealed class TextViewerViewModel : ObservableObject
	{
		/// <summary>The longest text that is colored, in characters.</summary>
		public const Int32 MaxColoredLength = 512 * 1024;

		/// <summary>The most parts a colored text can have: the document of the viewer has a run for each.</summary>
		public const Int32 MaxColoredParts = 50_000;

		private Byte[] _data = Array.Empty<Byte>();
		private Int64 _fullSize;
		private IReadOnlyList<TextEncodingOption> _options;
		private TextEncodingOption _selectedEncoding;
		private String _text = String.Empty;
		private String _status = String.Empty;
		private String _notice = String.Empty;
		private Boolean _wrapLines = true;
		private Boolean _isMessageFile;
		private Boolean _isScript;
		private Boolean _isBiography;
		private Boolean _isList;
		private readonly IUiThread _uiThread;
		private Boolean _tooLongForColors;
		private String _note = String.Empty;

		/// <summary>True while the list of encodings is replaced: the box that shows it then empties its selection, which must not be taken for a choice.</summary>
		private Boolean _refreshingLanguage;
		private IReadOnlyList<SyntaxSpan>? _syntaxSpans;

		public TextViewerViewModel(IUiThread uiThread)
		{
			_uiThread = uiThread;
			_options = TextEncodings.CreateOptions();
			_selectedEncoding = _options[0];
			CopyCommand = new RelayCommand(Copy, () => _text.Length > 0);
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
				{
					return;
				}

				if (SetProperty(ref _selectedEncoding, value) && _data.Length > 0)
				{
					Decode();
				}
			}
		}

		public String Text
		{
			get => _text;
			private set => SetProperty(ref _text, value);
		}

		/// <summary>The encoding, the size and the number of lines.</summary>
		public String Status
		{
			get => _status;
			private set => SetProperty(ref _status, value);
		}

		/// <summary>A remark about the text: it is cut, it looks binary, it is empty.</summary>
		public String Notice
		{
			get => _notice;
			private set => SetProperty(ref _notice, value);
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
				{
					OnPropertyChanged(nameof(IsHighlighted));
				}
			}
		}

		public Boolean IsHighlighted => _syntaxSpans != null;

		/// <param name="data">The bytes to show (the beginning of the file when it is large).</param>
		/// <param name="fullSize">The size of the whole file.</param>
		/// <param name="extension">The extension of the file (with the dot): it tells if the text gets colors.</param>
		/// <param name="note">Something to say about the text when there is nothing else to say (where it comes from).</param>
		public void Show(Byte[] data, Int64 fullSize, String? extension = null, String? note = null)
		{
			_data = data;
			_fullSize = fullSize;
			_isMessageFile = String.Equals(extension, ".msg", StringComparison.OrdinalIgnoreCase);
			_isScript = String.Equals(extension, ".ssl", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".h", StringComparison.OrdinalIgnoreCase);
			_isBiography = String.Equals(extension, ".bio", StringComparison.OrdinalIgnoreCase);
			_isList = String.Equals(extension, ".lst", StringComparison.OrdinalIgnoreCase);
			_note = note ?? String.Empty;
			Decode();
		}

		public void Clear()
		{
			_data = Array.Empty<Byte>();
			_fullSize = 0;
			_isMessageFile = false;
			_isScript = false;
			_isBiography = false;
			_isList = false;
			_note = String.Empty;
			SyntaxSpans = null;
			Text = String.Empty;
			Status = String.Empty;
			Notice = String.Empty;
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
			{
				Decode();
			}
		}

		private void Decode()
		{
			DecodedText decoded = TextDecoder.Decode(_data, _selectedEncoding.CodePage);
			Text = decoded.Text;
			SyntaxSpans = Colorize(decoded);

			String encoding = decoded.IsDetected ? Loc.Format("Str.EncodingDetected", decoded.EncodingName) : decoded.EncodingName;
			Int32 lines = 1 + decoded.Text.Count(character => character == '\n');
			Status = Loc.Format("Str.ViewerTextStatus", encoding, SizeFormatter.Format(_fullSize), lines.ToString("N0", CultureInfo.CurrentCulture));

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
				Notice = _tooLongForColors ? Loc.Get("Str.ViewerNotColored") : _note;
			}
		}

		/// <summary>
		/// The parts of the text that get a color, for the kinds of file that have colors. A text with too many parts is not colored
		/// (a run is made for each, and tens of thousands of them take seconds to lay out): it is shown plainly and the notice says so.
		/// </summary>
		private IReadOnlyList<SyntaxSpan>? Colorize(DecodedText decoded)
		{
			_tooLongForColors = false;
			Func<String, IReadOnlyList<SyntaxSpan>>? tokenize = _isMessageFile ? MsgSyntax.Tokenize : _isScript ? SslSyntax.Tokenize : _isBiography ? BioSyntax.Tokenize : _isList ? LstSyntax.Tokenize : null;
			if (decoded.LooksBinary || tokenize == null)
			{
				return null;
			}

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