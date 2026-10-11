namespace DatExplorerIII.Views
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Documents;
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
				FlowDocument document = text.SyntaxSpans == null ? new FlowDocument() : HighlightedDocument.Build(text.Text, text.SyntaxSpans);

				// A document does not take the size of the box by itself: the colored text has the size of the plain text.
				document.FontSize = HighlightedBox.FontSize;
				HighlightedBox.Document = document;
				ApplyWrap(text.WrapLines);
			}
			else if (e.PropertyName == nameof(TextViewerViewModel.WrapLines))
			{
				ApplyWrap(text.WrapLines);
			}
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