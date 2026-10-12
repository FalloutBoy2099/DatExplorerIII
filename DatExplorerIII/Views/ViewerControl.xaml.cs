namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Windows;
	using System.Windows.Documents;
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
		}

		/// <summary>How long the first paragraphs of a colored text may take to build and lay out before the text is shown, in milliseconds.</summary>
		private const Int32 FirstBatchMilliseconds = 25;

		/// <summary>How long each of the other batches may take, in milliseconds: the window stays alive while the end of a long text is added.</summary>
		private const Int32 NextBatchMilliseconds = 15;

		/// <summary>Tells a text from the one that came after it: the batches of the text that was replaced stop.</summary>
		private Int32 _highlightGeneration;

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