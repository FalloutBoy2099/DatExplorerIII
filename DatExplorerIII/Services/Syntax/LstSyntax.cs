namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Splits a list file of Fallout (<c>.lst</c>) into the parts that get a color. A line is one element of the list (usually a
	/// file name); a comment starts at a semicolon, the semicolon included, and runs to the end of the line.
	/// </summary>
	public static class LstSyntax
	{
		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		/// <param name="text">The list.</param>
		/// <param name="missingLines">The lines (from 1) whose name is a file that is not there: their name is marked.</param>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text, IReadOnlySet<Int32>? missingLines = null)
		{
			List<SyntaxSpan> spans = [];
			Int32 position = 0;
			Int32 line = 0;

			while (position < text.Length)
			{
				line++;
				Int32 end = position;
				while (end < text.Length && text[end] is not ('\r' or '\n'))
				{
					end++;
				}

				Int32 semicolon = text.IndexOf(';', position, end - position);
				Int32 codeEnd = semicolon < 0 ? end : semicolon;

				// The name is up to a space, a comma, a semicolon or a tab; the rest of the line is data.
				Int32 nameEnd = position;
				while (nameEnd < codeEnd && text[nameEnd] is not (' ' or ',' or '\t'))
				{
					nameEnd++;
				}

				if (missingLines != null && missingLines.Contains(line) && nameEnd > position)
				{
					SyntaxSpans.Add(spans, position, nameEnd - position, SyntaxKind.Missing);
					SyntaxSpans.Add(spans, nameEnd, codeEnd - nameEnd, SyntaxKind.Plain);
				}
				else
				{
					SyntaxSpans.Add(spans, position, codeEnd - position, SyntaxKind.Plain);
				}

				if (semicolon >= 0)
					SyntaxSpans.Add(spans, semicolon, end - semicolon, SyntaxKind.Comment);

				// The line break (\r\n, \n or \r) is plain.
				Int32 next = end;
				while (next < text.Length && text[next] is '\r' or '\n')
				{
					next++;
				}

				SyntaxSpans.Add(spans, end, next - end, SyntaxKind.Plain);
				position = next;
			}

			return spans;
		}
	}
}