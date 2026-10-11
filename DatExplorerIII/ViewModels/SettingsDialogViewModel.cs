namespace DatExplorerIII.ViewModels
{
	using System;
	using Infrastructure;
	using Models;

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
			SaveWindowSize = data.SaveWindowSize;
			SaveWindowPosition = data.SaveWindowPosition;
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

		public Boolean SaveWindowSize { get; set; }

		public Boolean SaveWindowPosition { get; set; }

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