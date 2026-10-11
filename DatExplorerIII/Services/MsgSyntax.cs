namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Splits a message file of Fallout (.msg) into the parts that get a color. A line is <c>{number}{sound}{text}</c>; the text
	/// can run over several lines. A line that starts with # is a comment, and so is what is written outside the braces.
	/// </summary>
	public static class MsgSyntax
	{
		/// <summary>Longest text that gets colors: a larger one is shown as it is, because coloring it would be slow.</summary>
		public const Int32 MaxLength = 512 * 1024;

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = new();
			Int32 position = 0;
			Int32 group = 0;

			while (position < text.Length)
			{
				Char current = text[position];
				if (current == '{')
				{
					SyntaxSpans.Add(spans, position, 1, SyntaxKind.Brace);
					Int32 close = text.IndexOf('}', position + 1);
					Int32 contentEnd = close < 0 ? text.Length : close;
					SyntaxKind kind = group switch
					{
						0 => SyntaxKind.Identifier,
						1 => SyntaxKind.Sound,
						_ => SyntaxKind.Message,
					};

					if (kind == SyntaxKind.Message)
					{
						AddMessage(spans, text, position + 1, contentEnd);
					}
					else
					{
						SyntaxSpans.Add(spans, position + 1, contentEnd - position - 1, kind);
					}

					if (close >= 0)
					{
						SyntaxSpans.Add(spans, close, 1, SyntaxKind.Brace);
						position = close + 1;
					}
					else
					{
						position = text.Length;
					}

					group = group == 2 ? 0 : group + 1;
				}
				else if (current == '#')
				{
					// A comment runs to the end of the line, braces included.
					Int32 end = position;
					while (end < text.Length && text[end] is not ('\r' or '\n'))
					{
						end++;
					}

					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Comment);
					position = end;
				}
				else if (current is '\r' or '\n')
				{
					Int32 end = position;
					while (end < text.Length && text[end] is '\r' or '\n')
					{
						end++;
					}

					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
					position = end;

					// A line that is not finished does not carry its braces over to the next one.
					group = 0;
				}
				else
				{
					// Outside the braces: spaces are kept plain, the rest of the line is a comment.
					Int32 end = position;
					Boolean blank = current is ' ' or '\t';
					while (end < text.Length && text[end] is not ('{' or '\r' or '\n') && (text[end] is ' ' or '\t') == blank)
					{
						end++;
					}

					SyntaxSpans.Add(spans, position, end - position, blank ? SyntaxKind.Plain : SyntaxKind.Comment);
					position = end;
				}
			}

			return spans;
		}

		/// <summary>A text with the places where the game puts a value: a % followed by a letter (%s, %d, %2d, %%).</summary>
		private static void AddMessage(List<SyntaxSpan> spans, String text, Int32 start, Int32 end)
		{
			Int32 position = start;
			while (position < end)
			{
				Int32 percent = text.IndexOf('%', position, end - position);
				if (percent < 0)
				{
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Message);
					return;
				}

				Int32 stop = percent + 1;
				while (stop < end && (Char.IsAsciiDigit(text[stop]) || text[stop] is '.' or '-' or '+' or '#'))
				{
					stop++;
				}

				if (stop < end && (Char.IsAsciiLetter(text[stop]) || text[stop] == '%'))
				{
					SyntaxSpans.Add(spans, position, percent - position, SyntaxKind.Message);
					SyntaxSpans.Add(spans, percent, stop + 1 - percent, SyntaxKind.Placeholder);
					position = stop + 1;
				}
				else
				{
					SyntaxSpans.Add(spans, position, percent + 1 - position, SyntaxKind.Message);
					position = percent + 1;
				}
			}
		}
	}
}