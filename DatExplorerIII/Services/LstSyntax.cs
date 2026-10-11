namespace DatExplorerIII.Services
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
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = new();
			Int32 position = 0;

			while (position < text.Length)
			{
				Int32 end = position;
				while (end < text.Length && text[end] is not ('\r' or '\n'))
				{
					end++;
				}

				Int32 semicolon = text.IndexOf(';', position, end - position);
				if (semicolon < 0)
				{
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
				}
				else
				{
					SyntaxSpans.Add(spans, position, semicolon - position, SyntaxKind.Plain);
					SyntaxSpans.Add(spans, semicolon, end - semicolon, SyntaxKind.Comment);
				}

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