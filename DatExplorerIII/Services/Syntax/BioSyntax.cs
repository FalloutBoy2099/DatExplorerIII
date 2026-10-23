namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Marks what does not fit in a biography of a premade character (<c>.bio</c>). The game shows it in the box of the character
	/// selection, which has room for 22 lines of 26 characters: a file starts with a blank line, the name of the character and a
	/// blank line (three lines that are not counted), then the text, and what is past the 22nd line, or past the 26th character of
	/// a line, is not seen.
	/// </summary>
	public static class BioSyntax
	{
		/// <summary>The lines at the start of the file that are not the text: a blank line, the name, a blank line.</summary>
		public const Int32 HeaderLines = 3;

		/// <summary>The lines of text the box shows.</summary>
		public const Int32 MaxLines = 22;

		/// <summary>The characters of a line of text the box shows.</summary>
		public const Int32 MaxColumns = 26;

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = [];
			Int32 position = 0;
			Int32 line = 0;

			while (position < text.Length)
			{
				Int32 end = position;
				while (end < text.Length && text[end] is not ('\r' or '\n'))
				{
					end++;
				}

				// The text of the line: the first lines are the header, then the lines of the text that fit, then the lines that do not.
				Int32 length = end - position;
				Int32 textLine = line - HeaderLines;
				if (textLine < 0)
				{
					SyntaxSpans.Add(spans, position, length, SyntaxKind.Plain);
				}
				else if (textLine >= MaxLines)
				{
					SyntaxSpans.Add(spans, position, length, SyntaxKind.Overflow);
				}
				else
				{
					SyntaxSpans.Add(spans, position, Math.Min(length, MaxColumns), SyntaxKind.Plain);
					SyntaxSpans.Add(spans, position + MaxColumns, length - MaxColumns, SyntaxKind.Overflow);
				}

				// The line break (\r\n, \n or \r) is part of the line it ends.
				Int32 next = end;
				if (next < text.Length && text[next] == '\r' && next + 1 < text.Length && text[next + 1] == '\n')
				{
					next += 2;
				}
				else if (next < text.Length)
				{
					next++;
				}

				SyntaxSpans.Add(spans, end, next - end, SyntaxKind.Plain);
				position = next;
				line++;
			}

			return spans;
		}
	}
}