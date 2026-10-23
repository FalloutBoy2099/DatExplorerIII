namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// Marks the parts of a configuration text in the INI form: <c>[section]</c> lines, <c>key=value</c> lines and comments (a line that
	/// starts with ; or #). Many text files of the games, and of their mods, are written that way whatever their extension.
	/// </summary>
	public static class IniSyntax
	{
		/// <summary>The share of the lines that have to be INI lines for a text to be taken for one.</summary>
		private const Double RequiredShare = 0.8;

		/// <summary>True when the text is a configuration text: a section, or at least three keys, and nearly every line is a section, a key or a comment.</summary>
		public static Boolean LooksLikeIni(String text)
		{
			Int32 sections = 0;
			Int32 keys = 0;
			Int32 others = 0;

			foreach ((Int32 start, Int32 length) in Lines(text))
			{
				ReadOnlySpan<Char> line = text.AsSpan(start, length).Trim();
				if (line.IsEmpty || line[0] is ';' or '#')
					continue;

				if (IsSection(line))
				{
					sections++;
				}
				else if (line.IndexOf('=') > 0)
				{
					keys++;
				}
				else
				{
					others++;
				}
			}

			Int32 counted = sections + keys + others;
			return keys > 0 && (sections > 0 || keys >= 3) && (counted - others) >= counted * RequiredShare;
		}

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = [];

			// The line breaks between the lines are plain text: nothing is left uncovered.
			Int32 covered = 0;
			foreach ((Int32 start, Int32 length) in Lines(text))
			{
				SyntaxSpans.Add(spans, covered, start - covered, SyntaxKind.Plain);
				TokenizeLine(text, start, length, spans);
				covered = start + length;
			}

			SyntaxSpans.Add(spans, covered, text.Length - covered, SyntaxKind.Plain);
			return spans;
		}

		private static void TokenizeLine(String text, Int32 start, Int32 length, List<SyntaxSpan> spans)
		{
			Int32 end = start + length;
			Int32 position = start;
			while (position < end && text[position] is ' ' or '\t')
			{
				position++;
			}

			SyntaxSpans.Add(spans, start, position - start, SyntaxKind.Plain);
			if (position >= end)
				return;

			if (text[position] is ';' or '#')
			{
				SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Comment);
				return;
			}

			if (text[position] == '[')
			{
				Int32 close = text.IndexOf(']', position, end - position);
				if (close > 0)
				{
					SyntaxSpans.Add(spans, position, 1, SyntaxKind.Brace);
					SyntaxSpans.Add(spans, position + 1, close - position - 1, SyntaxKind.Keyword);
					SyntaxSpans.Add(spans, close, 1, SyntaxKind.Brace);
					TokenizeTail(text, close + 1, end, spans);
					return;
				}
			}

			Int32 equals = text.IndexOf('=', position, end - position);
			if (equals > position)
			{
				Int32 keyEnd = equals;
				while (keyEnd > position && text[keyEnd - 1] is ' ' or '\t')
				{
					keyEnd--;
				}

				SyntaxSpans.Add(spans, position, keyEnd - position, SyntaxKind.Identifier);
				SyntaxSpans.Add(spans, keyEnd, equals - keyEnd, SyntaxKind.Plain);
				SyntaxSpans.Add(spans, equals, 1, SyntaxKind.Brace);
				TokenizeValue(text, equals + 1, end, spans);
				return;
			}

			SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
		}

		/// <summary>The end of a section line: spaces, and perhaps a comment.</summary>
		private static void TokenizeTail(String text, Int32 start, Int32 end, List<SyntaxSpan> spans)
		{
			Int32 comment = IndexOfComment(text, start, end);
			SyntaxSpans.Add(spans, start, (comment < 0 ? end : comment) - start, SyntaxKind.Plain);
			if (comment >= 0)
				SyntaxSpans.Add(spans, comment, end - comment, SyntaxKind.Comment);
		}

		/// <summary>What follows the = of a key: spaces, the value (a number or a text) and perhaps a comment.</summary>
		private static void TokenizeValue(String text, Int32 start, Int32 end, List<SyntaxSpan> spans)
		{
			Int32 comment = IndexOfComment(text, start, end);
			Int32 valueEnd = comment < 0 ? end : comment;

			Int32 valueStart = start;
			while (valueStart < valueEnd && text[valueStart] is ' ' or '\t')
			{
				valueStart++;
			}

			Int32 trimmedEnd = valueEnd;
			while (trimmedEnd > valueStart && text[trimmedEnd - 1] is ' ' or '\t')
			{
				trimmedEnd--;
			}

			SyntaxSpans.Add(spans, start, valueStart - start, SyntaxKind.Plain);
			String value = text.Substring(valueStart, trimmedEnd - valueStart);
			SyntaxKind kind = Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || IsHexadecimal(value) ? SyntaxKind.Number : SyntaxKind.String;
			SyntaxSpans.Add(spans, valueStart, trimmedEnd - valueStart, kind);
			SyntaxSpans.Add(spans, trimmedEnd, valueEnd - trimmedEnd, SyntaxKind.Plain);
			if (comment >= 0)
				SyntaxSpans.Add(spans, comment, end - comment, SyntaxKind.Comment);
		}

		/// <summary>The place of a comment at the end of a line: a ; or a # that follows a space (a # inside a value, as in a color, is not one).</summary>
		private static Int32 IndexOfComment(String text, Int32 start, Int32 end)
		{
			for (Int32 i = start + 1; i < end; i++)
			{
				if (text[i] is ';' or '#' && text[i - 1] is ' ' or '\t')
					return i;
			}

			return -1;
		}

		private static Boolean IsHexadecimal(String value)
		{
			return value.Length > 2 && value[0] == '0' && value[1] is 'x' or 'X' && Int64.TryParse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
		}

		private static Boolean IsSection(ReadOnlySpan<Char> line)
		{
			return line.Length > 2 && line[0] == '[' && line.IndexOf(']') > 1;
		}

		/// <summary>The lines of a text: where each starts and its length, without the line break.</summary>
		private static IEnumerable<(Int32 Start, Int32 Length)> Lines(String text)
		{
			Int32 position = 0;
			while (position < text.Length)
			{
				Int32 end = position;
				while (end < text.Length && text[end] is not ('\r' or '\n'))
				{
					end++;
				}

				yield return (position, end - position);

				position = end;
				if (position < text.Length && text[position] == '\r' && position + 1 < text.Length && text[position + 1] == '\n')
				{
					position += 2;
				}
				else if (position < text.Length)
				{
					position++;
				}
			}
		}
	}
}