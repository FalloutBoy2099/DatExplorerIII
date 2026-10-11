namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Markup;
	using System.Windows.Documents;
	using System.Windows.Media;
	using Services;

	/// <summary>
	/// Makes the document of the viewer for a text with colors: one paragraph per line, one run per part. The colors are those
	/// of the theme, so they follow it when it changes.
	/// </summary>
	public static class HighlightedDocument
	{
		public static FlowDocument Build(String text, IReadOnlyList<SyntaxSpan> spans)
		{
			FlowDocument document = new() { PagePadding = new Thickness(0), FontFamily = new FontFamily("Consolas"), TextAlignment = TextAlignment.Left };
			Paragraph paragraph = NewParagraph();
			Dictionary<SyntaxKind, Style?> styles = new();

			foreach (SyntaxSpan span in spans)
			{
				Int32 position = span.Start;
				Int32 end = span.Start + span.Length;
				while (position < end)
				{
					Int32 lineBreak = text.IndexOf('\n', position, end - position);
					Int32 stop = lineBreak < 0 ? end : lineBreak;

					// A \r that comes before the \n belongs to the break.
					Int32 length = stop - position;
					if (length > 0 && text[stop - 1] == '\r' && lineBreak >= 0)
					{
						length--;
					}

					if (length > 0)
					{
						paragraph.Inlines.Add(CreateRun(text.Substring(position, length), span.Kind, styles));
					}

					if (lineBreak < 0)
					{
						position = end;
					}
					else
					{
						document.Blocks.Add(paragraph);
						paragraph = NewParagraph();
						position = lineBreak + 1;
					}
				}
			}

			document.Blocks.Add(paragraph);
			return document;
		}

		private static Paragraph NewParagraph() => new() { Margin = new Thickness(0) };

		private static Run CreateRun(String text, SyntaxKind kind, Dictionary<SyntaxKind, Style?> styles)
		{
			Run run = new(text);

			// One style for each kind, shared by its runs: much cheaper than a reference to each resource on each run, and the
			// colors still follow the theme.
			if (!styles.TryGetValue(kind, out Style? style))
			{
				style = CreateStyle(kind);
				styles[kind] = style;
			}

			if (style != null)
			{
				run.Style = style;
			}

			return run;
		}

		/// <summary>The style of the runs of a kind; null for the kinds that have the color of the text.</summary>
		private static Style? CreateStyle(SyntaxKind kind)
		{
			String? brush = kind switch
			{
				SyntaxKind.Comment => "SystemFillColorSuccessBrush",
				SyntaxKind.Brace => "TextFillColorTertiaryBrush",
				SyntaxKind.Identifier => "AccentTextFillColorPrimaryBrush",
				SyntaxKind.Sound => "SystemFillColorCautionBrush",
				SyntaxKind.Placeholder => "SystemFillColorCriticalBrush",
				SyntaxKind.Keyword => "AccentTextFillColorPrimaryBrush",
				SyntaxKind.Directive => "SystemFillColorCriticalBrush",
				SyntaxKind.String => "SystemFillColorCautionBrush",
				SyntaxKind.Number => "SystemFillColorCautionBrush",
				SyntaxKind.Function => "AccentTextFillColorSecondaryBrush",
				SyntaxKind.Overflow => "SystemFillColorCriticalBrush",
				_ => null,
			};

			if (brush == null)
			{
				return null;
			}

			Style style = new(typeof(Run));
			style.Setters.Add(new Setter(TextElement.ForegroundProperty, new DynamicResourceExtension(brush)));
			if (kind == SyntaxKind.Keyword)
			{
				style.Setters.Add(new Setter(TextElement.FontWeightProperty, FontWeights.SemiBold));
			}
			else if (kind == SyntaxKind.Overflow)
			{
				// What does not fit is marked on its background too, so that spaces past the end of a line show.
				style.Setters.Add(new Setter(TextElement.BackgroundProperty, new DynamicResourceExtension("SystemFillColorCriticalBackgroundBrush")));
			}

			return style;
		}
	}
}