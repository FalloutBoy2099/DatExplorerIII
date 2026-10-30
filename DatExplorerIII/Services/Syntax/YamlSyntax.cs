namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>Marks the parts of a YAML text: the keys, the comments, the strings, the numbers, true, false and null, the anchors and tags, and the markers of a document and of a list.</summary>
	public static class YamlSyntax
	{
		private static readonly HashSet<String> Literals = new(StringComparer.OrdinalIgnoreCase) { "true", "false", "null", "yes", "no", "on", "off", "~" };

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			SpanWriter writer = new(text.Length);
			Int32 blockIndent = -1;
			Int32 position = 0;
			while (position < text.Length)
			{
				Int32 lineEnd = text.IndexOf('\n', position);
				Int32 next = lineEnd < 0 ? text.Length : lineEnd + 1;
				Int32 end = lineEnd < 0 ? text.Length : lineEnd;
				if (end > position && text[end - 1] == '\r')
					end--;

				Int32 start = position;
				while (start < end && text[start] is ' ' or '\t')
				{
					start++;
				}

				if (start < end)
				{
					Int32 indent = start - position;
					if (blockIndent >= 0 && indent > blockIndent)
					{
						// The text of a block scalar (after | or >): all of it is a text.
						writer.Part(start, end, SyntaxKind.String);
					}
					else
					{
						blockIndent = Line(writer, text, start, end, indent);
					}
				}

				position = next;
			}

			return writer.Finish();
		}

		/// <summary>One line; returns the indentation of a block scalar that starts on it, else -1.</summary>
		private static Int32 Line(SpanWriter writer, String text, Int32 start, Int32 end, Int32 indent)
		{
			if (text[start] == '#')
			{
				writer.Part(start, end, SyntaxKind.Comment);
				return -1;
			}

			if (end - start >= 3 && (String.CompareOrdinal(text, start, "---", 0, 3) == 0 || String.CompareOrdinal(text, start, "...", 0, 3) == 0) && (end - start == 3 || text[start + 3] == ' '))
			{
				writer.Part(start, start + 3, SyntaxKind.Directive);
				Value(writer, text, start + 3, end);
				return -1;
			}

			Int32 index = start;
			while (index < end && text[index] == '-' && (index + 1 == end || text[index + 1] == ' '))
			{
				writer.Part(index, index + 1, SyntaxKind.Brace);
				index++;
				while (index < end && text[index] == ' ')
				{
					index++;
				}
			}

			Int32 colon = KeyEnd(text, index, end);
			if (colon > index)
			{
				writer.Part(index, colon, SyntaxKind.Identifier);
				writer.Part(colon, colon + 1, SyntaxKind.Brace);
				index = colon + 1;
			}

			Value(writer, text, index, end);

			String tail = text.Substring(index, end - index).Trim();
			Int32 comment = tail.IndexOf(" #", StringComparison.Ordinal);
			if (comment >= 0)
				tail = tail[..comment].TrimEnd();

			return tail.Length > 0 && tail[0] is '|' or '>' && (tail.Length == 1 || tail[1] is '-' or '+' or (>= '0' and <= '9')) ? indent : -1;
		}

		/// <summary>Where the colon of a key is (the key is a word, or a text in quotes, followed by a colon and a space or the end); -1 when the line has no key.</summary>
		private static Int32 KeyEnd(String text, Int32 start, Int32 end)
		{
			Int32 index = start;
			if (index < end && text[index] is '"' or '\'')
			{
				Int32 close = text.IndexOf(text[index], index + 1, end - index - 1);
				if (close < 0)
					return -1;

				index = close + 1;
				return index < end && text[index] == ':' && (index + 1 == end || text[index + 1] == ' ') ? index : -1;
			}

			for (; index < end; index++)
			{
				if (text[index] == ':' && (index + 1 == end || text[index + 1] == ' '))
					return index;

				if (text[index] is '[' or '{' or '"' or '\'' || (text[index] == '#' && index > start && text[index - 1] == ' '))
					return -1;
			}

			return -1;
		}

		/// <summary>What follows a key or a marker: a scalar, a flow of values, an anchor or a tag, and perhaps a comment.</summary>
		private static void Value(SpanWriter writer, String text, Int32 start, Int32 end)
		{
			Int32 index = start;
			while (index < end)
			{
				Char character = text[index];
				if (character is ' ' or '\t')
				{
					index++;
				}
				else if (character == '#' && (index == start || text[index - 1] is ' ' or '\t'))
				{
					writer.Part(index, end, SyntaxKind.Comment);
					return;
				}
				else if (character is '"' or '\'')
				{
					Int32 close = index + 1;
					while (close < end && text[close] != character)
					{
						close += character == '"' && text[close] == '\\' ? 2 : 1;
					}

					Int32 stop = Math.Min(end, close + 1);
					writer.Part(index, stop, SyntaxKind.String);
					index = stop;
				}
				else if (character is '&' or '*' or '!')
				{
					Int32 stop = index + 1;
					while (stop < end && text[stop] is not (' ' or ',' or ']' or '}'))
					{
						stop++;
					}

					writer.Part(index, stop, SyntaxKind.Directive);
					index = stop;
				}
				else if (character is '[' or ']' or '{' or '}' or ',')
				{
					index++;
				}
				else
				{
					Int32 stop = index;
					while (stop < end && text[stop] is not (',' or ']' or '}') && !(text[stop] == '#' && stop > index && text[stop - 1] == ' '))
					{
						stop++;
					}

					Int32 trimmed = stop;
					while (trimmed > index && text[trimmed - 1] == ' ')
					{
						trimmed--;
					}

					String word = text.Substring(index, trimmed - index);
					SyntaxKind kind = Literals.Contains(word) ? SyntaxKind.Keyword
						: Double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _) || word.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? SyntaxKind.Number
						: SyntaxKind.String;
					writer.Part(index, trimmed, kind);
					index = Math.Max(stop, index + 1);
				}
			}
		}
	}
}
