namespace DatExplorerIII.Views.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;
	using Services.Syntax;

	/// <summary>
	/// Gives the colors of a text (a message file, a script) to the lines the editor draws. Only the lines that can be seen are
	/// drawn, so a long text costs no more than a short one. The colors are those of the theme: after the theme changed,
	/// <see cref="ThemeChanged"/> looks them up again.
	/// </summary>
	internal sealed class SyntaxColorizer : DocumentColorizingTransformer
	{
		private readonly IReadOnlyList<SyntaxSpan> _spans;
		private readonly FrameworkElement _resources;
		private readonly Dictionary<SyntaxKind, Look?> _looks = [];

		/// <param name="spans">The parts of the text, in order, one after the other.</param>
		/// <param name="resources">Where the brushes of the theme are looked up.</param>
		public SyntaxColorizer(IReadOnlyList<SyntaxSpan> spans, FrameworkElement resources)
		{
			_spans = spans;
			_resources = resources;
		}

		/// <summary>How a kind of part is drawn: its color, the color behind it, and the weight or the line of its letters.</summary>
		private sealed record Look(Brush? Foreground, Brush? Background, Boolean SemiBold, Boolean Underline);

		/// <summary>The brushes are looked up again (the theme changed).</summary>
		public void ThemeChanged()
		{
			_looks.Clear();
		}

		/// <summary>
		/// The parts of a line that have a kind: where they start and end in the text, a part of the text cut at the ends of the
		/// line. The parts are found by a binary search, so a line of a long text costs as little as one of a short text.
		/// </summary>
		internal static IEnumerable<(Int32 Start, Int32 End, SyntaxKind Kind)> PartsOf(IReadOnlyList<SyntaxSpan> spans, Int32 lineStart, Int32 lineEnd)
		{
			// The first part that ends after the start of the line.
			Int32 low = 0;
			Int32 high = spans.Count;
			while (low < high)
			{
				Int32 middle = (low + high) / 2;
				if (spans[middle].Start + spans[middle].Length <= lineStart)
				{
					low = middle + 1;
				}
				else
				{
					high = middle;
				}
			}

			for (Int32 index = low; index < spans.Count && spans[index].Start < lineEnd; index++)
			{
				SyntaxSpan span = spans[index];
				Int32 start = Math.Max(span.Start, lineStart);
				Int32 end = Math.Min(span.Start + span.Length, lineEnd);
				if (end > start)
					yield return (start, end, span.Kind);
			}
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			if (line.Length == 0)
				return;

			foreach ((Int32 start, Int32 end, SyntaxKind kind) in PartsOf(_spans, line.Offset, line.EndOffset))
			{
				if (LookOf(kind) is { } look)
					ChangeLinePart(start, end, element => Apply(element, look));
			}
		}

		private static void Apply(VisualLineElement element, Look look)
		{
			if (look.Foreground != null)
				element.TextRunProperties.SetForegroundBrush(look.Foreground);

			if (look.Background != null)
				element.TextRunProperties.SetBackgroundBrush(look.Background);

			if (look.SemiBold)
			{
				Typeface typeface = element.TextRunProperties.Typeface;
				element.TextRunProperties.SetTypeface(new Typeface(typeface.FontFamily, typeface.Style, FontWeights.SemiBold, typeface.Stretch));
			}

			if (look.Underline)
				element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
		}

		/// <summary>How a kind is drawn; null for the kinds that have the color of the text.</summary>
		private Look? LookOf(SyntaxKind kind)
		{
			if (!_looks.TryGetValue(kind, out Look? look))
			{
				look = CreateLook(kind);
				_looks[kind] = look;
			}

			return look;
		}

		private Look? CreateLook(SyntaxKind kind)
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
				SyntaxKind.Missing => "SystemFillColorCriticalBrush",
				_ => null,
			};

			if (brush == null)
				return null;

			// What does not fit is marked on its background too, so that spaces past the end of a line show.
			Brush? background = kind == SyntaxKind.Overflow ? _resources.TryFindResource("SystemFillColorCriticalBackgroundBrush") as Brush : null;
			return new Look(_resources.TryFindResource(brush) as Brush, background, kind == SyntaxKind.Keyword, kind == SyntaxKind.Missing);
		}
	}
}