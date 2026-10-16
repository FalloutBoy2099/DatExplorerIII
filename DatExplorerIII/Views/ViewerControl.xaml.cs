namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Linq;
	using System.Windows;
	using System.Windows.Documents;
	using System.Windows.Input;
	using System.Windows.Threading;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using ViewModels;

	/// <summary>
	/// Shows a file as a text, a picture or a sound. It binds to a <see cref="ViewModels.ViewerViewModel"/>.
	/// </summary>
	public partial class ViewerControl : UserControl
	{
		public ViewerControl()
		{
			InitializeComponent();
			DataContextChanged += OnDataContextChanged;
		}

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is ViewerViewModel old)
			{
				old.Text.PropertyChanged -= OnTextChanged;
			}

			if (e.NewValue is ViewerViewModel viewer)
			{
				viewer.Text.PropertyChanged += OnTextChanged;
			}
		}

		private void OnTextChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not TextViewerViewModel text)
			{
				return;
			}

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

		/// <summary>How long the first paragraphs of a colored text may take to build and lay out before the text is shown, in milliseconds.</summary>
		private const Int32 FirstBatchMilliseconds = 25;

		/// <summary>How long each of the other batches may take, in milliseconds: the window stays alive while the end of a long text is added.</summary>
		private const Int32 NextBatchMilliseconds = 15;

		/// <summary>Tells a text from the one that came after it: the batches of the text that was replaced stop.</summary>
		private Int32 _highlightGeneration;

		/// <summary>The paragraphs of the colored text that are not in the document yet (null: all of them are).</summary>
		private IEnumerator<Paragraph>? _pendingParagraphs;

		/// <summary>
		/// Shows a colored text. The first lines are there at once; the others are added in the background, a few at a time (a
		/// whole message file takes seconds to build and to lay out as one document).
		/// </summary>
		private void ShowHighlighted(TextViewerViewModel text)
		{
			Int32 generation = ++_highlightGeneration;
			FlowDocument document = HighlightedDocument.NewDocument();

			// A document does not take the size of the box by itself: the colored text has the size of the plain text.
			document.FontSize = HighlightedBox.FontSize;
			IEnumerator<Paragraph>? paragraphs = text.SyntaxSpans == null ? null : HighlightedDocument.Paragraphs(text.Text, text.SyntaxSpans).GetEnumerator();
			Boolean more = AddParagraphs(document, paragraphs, FirstBatchMilliseconds);
			HighlightedBox.Document = document;
			ApplyWrap(text.WrapLines);
			_pendingParagraphs = more ? paragraphs : null;
			if (more)
			{
				Dispatcher.BeginInvoke(DispatcherPriority.Background, () => AddNextParagraphs(generation, document, paragraphs!));
			}
		}

		private void AddNextParagraphs(Int32 generation, FlowDocument document, IEnumerator<Paragraph> paragraphs)
		{
			if (generation != _highlightGeneration)
			{
				paragraphs.Dispose();
				return;
			}

			if (AddParagraphs(document, paragraphs, NextBatchMilliseconds))
			{
				Dispatcher.BeginInvoke(DispatcherPriority.Background, () => AddNextParagraphs(generation, document, paragraphs));
			}
			else if (generation == _highlightGeneration)
			{
				_pendingParagraphs = null;
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

			// The document has a paragraph for each line, and the last ones may not be there yet: they are made now.
			String content = text.Text;
			Int32 line = content.AsSpan(0, reveal.Start).Count('\n');
			Int32 lineStart = reveal.Start == 0 ? 0 : content.LastIndexOf('\n', reveal.Start - 1) + 1;
			FlowDocument document = HighlightedBox.Document;
			while (document.Blocks.Count <= line && _pendingParagraphs != null)
			{
				if (!AddParagraphs(document, _pendingParagraphs, 200))
				{
					_pendingParagraphs = null;
				}
			}

			Block? block = document.Blocks.Skip(line).FirstOrDefault();
			if (block is not Paragraph paragraph)
			{
				return;
			}

			TextPointer start = PointerAt(paragraph, reveal.Start - lineStart);
			TextPointer end = PointerAt(paragraph, reveal.Start - lineStart + reveal.Length);
			HighlightedBox.Focus();
			HighlightedBox.Selection.Select(start, end);
			paragraph.BringIntoView();
		}

		/// <summary>The place of a column of a line (a paragraph) in the document.</summary>
		private static TextPointer PointerAt(Paragraph paragraph, Int32 column)
		{
			Int32 remaining = column;
			foreach (Inline inline in paragraph.Inlines)
			{
				if (inline is Run run)
				{
					if (remaining <= run.Text.Length)
					{
						return run.ContentStart.GetPositionAtOffset(remaining) ?? run.ContentEnd;
					}

					remaining -= run.Text.Length;
				}
			}

			return paragraph.ContentEnd;
		}

		/// <summary>Adds paragraphs for the time that is given (at least one).</summary>
		/// <returns>True when there are more to add.</returns>
		private static Boolean AddParagraphs(FlowDocument document, IEnumerator<Paragraph>? paragraphs, Int32 milliseconds)
		{
			if (paragraphs == null)
			{
				return false;
			}

			List<Paragraph> batch = new();
			Stopwatch watch = Stopwatch.StartNew();
			Boolean more = true;
			while (more && (batch.Count == 0 || watch.ElapsedMilliseconds < milliseconds))
			{
				more = paragraphs.MoveNext();
				if (more)
				{
					batch.Add(paragraphs.Current);
				}
			}

			document.Blocks.AddRange(batch);
			if (!more)
			{
				paragraphs.Dispose();
			}

			return more;
		}

		/// <summary>A document that wraps follows the width of the box; one that does not is as wide as it needs and scrolls.</summary>
		private void ApplyWrap(Boolean wrap)
		{
			HighlightedBox.HorizontalScrollBarVisibility = wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
			HighlightedBox.Document.PageWidth = wrap ? Double.NaN : 100000;
		}

		/// <summary>The columns of a table are made by the grid: they get the header in the language and a width that suits them.</summary>
		private void OnAutoGeneratingColumn(Object? sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			if (((FrameworkElement)sender!).DataContext is not TableViewerViewModel table)
			{
				return;
			}

			e.Column.Header = table.GetHeader(e.PropertyName);
			if (e.Column is DataGridTextColumn column)
			{
				Boolean isText = e.PropertyName is "Text" or "Text2" or "Comment";
				column.Width = isText ? new DataGridLength(340) : DataGridLength.Auto;
				if (isText)
				{
					column.ElementStyle = (Style)FindResource("WrapCell");
				}
			}
		}

		/// <summary>The wheel of the mouse magnifies the picture (up) or reduces it (down).</summary>
		private void OnImageWheel(Object sender, MouseWheelEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is ImageViewerViewModel image && e.Delta != 0)
			{
				image.Wheel(e.Delta);
				e.Handled = true;
			}
		}

		/// <summary>The button of the wheel shows the picture at its real size (100%).</summary>
		private void OnImageMouseDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Middle && ((FrameworkElement)sender).DataContext is ImageViewerViewModel image)
			{
				image.ActualSize();
				e.Handled = true;
			}
		}

		private void OnSeekStarted(Object sender, DragStartedEventArgs e)
		{
			(((FrameworkElement)sender).DataContext as ISeekable)?.BeginSeek();
		}

		private void OnSeekCompleted(Object sender, DragCompletedEventArgs e)
		{
			(((FrameworkElement)sender).DataContext as ISeekable)?.EndSeek();
		}
	}
}