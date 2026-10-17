namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Marks the comments of a text file that is not a configuration in the INI form: a line whose first character that is not a space
	/// is a # (a # inside a line is text, as in "item #3"). The rest of the text is plain.
	/// </summary>
	public static class HashCommentSyntax
	{
		/// <summary>True when at least one line is a comment.</summary>
		public static Boolean HasComments(String text)
		{
			Int32 lineStart = 0;
			while (lineStart < text.Length)
			{
				Int32 position = lineStart;
				while (position < text.Length && text[position] is ' ' or '\t')
				{
					position++;
				}

				if (position < text.Length && text[position] == '#')
				{
					return true;
				}

				Int32 end = text.IndexOf('\n', lineStart);
				if (end < 0)
				{
					break;
				}

				lineStart = end + 1;
			}

			return false;
		}

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = new();
			Int32 lineStart = 0;

			while (lineStart < text.Length)
			{
				Int32 end = text.IndexOf('\n', lineStart);
				Int32 next = end < 0 ? text.Length : end + 1;

				Int32 position = lineStart;
				while (position < next && text[position] is ' ' or '\t')
				{
					position++;
				}

				if (position < next && text[position] == '#')
				{
					// The comment is the line without its break (\r\n or \n), which stays plain like the text.
					Int32 stop = next;
					while (stop > position && text[stop - 1] is '\n' or '\r')
					{
						stop--;
					}

					SyntaxSpans.Add(spans, lineStart, position - lineStart, SyntaxKind.Plain);
					SyntaxSpans.Add(spans, position, stop - position, SyntaxKind.Comment);
					SyntaxSpans.Add(spans, stop, next - stop, SyntaxKind.Plain);
				}
				else
				{
					SyntaxSpans.Add(spans, lineStart, next - lineStart, SyntaxKind.Plain);
				}

				lineStart = next;
			}

			return spans;
		}
	}
}
