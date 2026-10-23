namespace DatExplorerIII.Views.Viewer
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;
	using ViewModels.Viewer;
	using Services.Syntax;

	/// <summary>
	/// A text. A text with colors (a message file, a script) is shown by an editor that draws only the lines that can be seen, with
	/// the colors of its parts; selecting and scrolling to a part of the text (a search found it) is done here too.
	/// </summary>
	public partial class TextView : UserControl
	{
		/// <summary>The color of the text of the theme: when it changes, the theme changed, and the colors of the parts are looked up again.</summary>
		private static readonly DependencyProperty ThemeTextBrushProperty = DependencyProperty.Register(
			"ThemeTextBrush",
			typeof(Brush),
			typeof(TextView),
			new PropertyMetadata(null, (sender, _) => ((TextView)sender).OnThemeChanged()));

		private TextViewerViewModel? _text;
		private SyntaxColorizer? _colorizer;

		public TextView()
		{
			InitializeComponent();
			ConfigureEditor();
			SetResourceReference(ThemeTextBrushProperty, "TextFillColorPrimaryBrush");
			DataContextChanged += OnDataContextChanged;
		}

		/// <summary>A text to read: no links, no dragging, no boxes for the control characters, the selection in the color of the theme.</summary>
		private void ConfigureEditor()
		{
			HighlightedEditor.Options.EnableHyperlinks = false;
			HighlightedEditor.Options.EnableEmailHyperlinks = false;
			HighlightedEditor.Options.EnableTextDragDrop = false;
			HighlightedEditor.Options.ShowBoxForControlCharacters = false;

			TextArea area = HighlightedEditor.TextArea;
			area.SelectionCornerRadius = 0;
			area.SelectionBorder = null;
			area.SelectionForeground = null;
			SolidColorBrush selection = new(SystemColors.AccentColor) { Opacity = 0.45 };
			selection.Freeze();
			area.SelectionBrush = selection;

			// The menu is on the area that has the text: its commands (copy, select all) are handled there.
			area.ContextMenu = (ContextMenu)FindResource("TextMenu");
		}

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (_text != null)
				_text.PropertyChanged -= OnTextChanged;

			_text = e.NewValue as TextViewerViewModel;
			if (_text != null)
				_text.PropertyChanged += OnTextChanged;
		}

		private void OnTextChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not TextViewerViewModel text)
				return;

			if (e.PropertyName == nameof(TextViewerViewModel.SyntaxSpans))
			{
				ShowHighlighted(text);
			}
			else if (e.PropertyName == nameof(TextViewerViewModel.WrapLines))
			{
				ApplyWrap(text.WrapLines);
			}
			else if (e.PropertyName == nameof(TextViewerViewModel.Reveal) && text.Reveal is { } reveal)
			{
				RevealPart(text, reveal);
			}
		}

		/// <summary>Shows a colored text (nothing when the text has no colors: the plain text box shows it).</summary>
		private void ShowHighlighted(TextViewerViewModel text)
		{
			if (_colorizer != null)
			{
				HighlightedEditor.TextArea.TextView.LineTransformers.Remove(_colorizer);
				_colorizer = null;
			}

			TextDocument document = new(text.SyntaxSpans == null ? String.Empty : text.Text);
			document.UndoStack.SizeLimit = 0;
			HighlightedEditor.Document = document;
			if (text.SyntaxSpans != null)
			{
				_colorizer = new SyntaxColorizer(text.SyntaxSpans, this);
				HighlightedEditor.TextArea.TextView.LineTransformers.Add(_colorizer);
			}

			ApplyWrap(text.WrapLines);
			HighlightedEditor.ScrollToHome();
		}

		private void OnThemeChanged()
		{
			if (_colorizer != null)
			{
				_colorizer.ThemeChanged();
				HighlightedEditor.TextArea.TextView.Redraw();
			}
		}

		/// <summary>Selects a part of the text and scrolls to it, in the box that shows the text.</summary>
		private void RevealPart(TextViewerViewModel text, TextReveal reveal)
		{
			if (!text.IsHighlighted)
			{
				PlainTextBox.Focus();
				PlainTextBox.Select(reveal.Start, reveal.Length);
				PlainTextBox.ScrollToLine(PlainTextBox.GetLineIndexFromCharacterIndex(reveal.Start));
				return;
			}

			TextDocument document = HighlightedEditor.Document;
			Int32 start = Math.Clamp(reveal.Start, 0, document.TextLength);
			Int32 length = Math.Clamp(reveal.Length, 0, document.TextLength - start);
			HighlightedEditor.TextArea.Focus();
			HighlightedEditor.Select(start, length);
			TextLocation location = document.GetLocation(start);
			HighlightedEditor.ScrollTo(location.Line, location.Column);
		}

		/// <summary>A text that wraps follows the width of the box; one that does not is as wide as its longest line and scrolls.</summary>
		private void ApplyWrap(Boolean wrap)
		{
			HighlightedEditor.WordWrap = wrap;
		}
	}
}