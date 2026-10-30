namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>Marks the parts of a TOML text: the tables, the keys, the strings (also the ones over several lines), the numbers and dates, true and false, and the comments.</summary>
	public static class TomlSyntax
	{
		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			SpanWriter writer = new(text.Length);
			String? open = null;
			Int32 position = 0;
			while (position < text.Length)
			{
				Int32 lineEnd = text.IndexOf('\n', position);
				Int32 next = lineEnd < 0 ? text.Length : lineEnd + 1;
				Int32 end = lineEnd < 0 ? text.Length : lineEnd;
				if (end > position && text[end - 1] == '\r')
					end--;

				Int32 index = position;
				if (open != null)
				{
					// Inside a string over several lines.
					Int32 close = text.IndexOf(open, index, end - index, StringComparison.Ordinal);
					if (close < 0)
					{
						writer.Part(index, end, SyntaxKind.String);
						position = next;
						continue;
					}

					index = close + 3;
					writer.Part(position, index, SyntaxKind.String);
					open = null;
				}
				else
				{
					while (index < end && text[index] is ' ' or '\t')
					{
						index++;
					}

					if (index < end && text[index] == '[')
					{
						Int32 stop = index;
						while (stop < end && text[stop] == '[')
						{
							stop++;
						}

						Int32 nameEnd = text.IndexOf(']', stop, end - stop);
						if (nameEnd > 0)
						{
							writer.Part(index, stop, SyntaxKind.Brace);
							writer.Part(stop, nameEnd, SyntaxKind.Keyword);
							Int32 closeEnd = nameEnd;
							while (closeEnd < end && text[closeEnd] == ']')
							{
								closeEnd++;
							}

							writer.Part(nameEnd, closeEnd, SyntaxKind.Brace);
							index = closeEnd;
						}
					}
					else
					{
						Int32 equals = KeyEnd(text, index, end);
						if (equals > index)
						{
							Int32 keyEnd = equals;
							while (keyEnd > index && text[keyEnd - 1] is ' ' or '\t')
							{
								keyEnd--;
							}

							writer.Part(index, keyEnd, SyntaxKind.Identifier);
							writer.Part(equals, equals + 1, SyntaxKind.Brace);
							index = equals + 1;
						}
					}
				}

				open = Value(writer, text, index, end);
				position = next;
			}

			return writer.Finish();
		}

		/// <summary>Where the = of a key is (a key can be in quotes); -1 when the line has none.</summary>
		private static Int32 KeyEnd(String text, Int32 start, Int32 end)
		{
			for (Int32 index = start; index < end; index++)
			{
				Char character = text[index];
				if (character == '=')
					return index > start ? index : -1;

				if (character is '"' or '\'')
				{
					Int32 close = text.IndexOf(character, index + 1, end - index - 1);
					if (close < 0)
						return -1;

					index = close;
				}
				else if (character == '#')
				{
					return -1;
				}
			}

			return -1;
		}

		/// <summary>The values of a line (and a comment); returns the quotes of a string that goes on the next line, else null.</summary>
		private static String? Value(SpanWriter writer, String text, Int32 start, Int32 end)
		{
			Int32 index = start;
			while (index < end)
			{
				Char character = text[index];
				if (character == '#')
				{
					writer.Part(index, end, SyntaxKind.Comment);
					return null;
				}

				if (character is '"' or '\'')
				{
					String triple = new(character, 3);
					if (String.CompareOrdinal(text, index, triple, 0, 3) == 0)
					{
						Int32 close = end - index - 3 > 0 ? text.IndexOf(triple, index + 3, end - index - 3, StringComparison.Ordinal) : -1;
						if (close < 0)
						{
							writer.Part(index, end, SyntaxKind.String);
							return triple;
						}

						writer.Part(index, close + 3, SyntaxKind.String);
						index = close + 3;
					}
					else
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
				}
				else if (Char.IsAsciiLetterOrDigit(character) || character is '+' or '-' or '.' or '_')
				{
					Int32 stop = index;
					while (stop < end && (Char.IsAsciiLetterOrDigit(text[stop]) || text[stop] is '+' or '-' or '.' or '_' or ':'))
					{
						stop++;
					}

					String word = text.Substring(index, stop - index);
					SyntaxKind kind = word is "true" or "false" or "inf" or "nan" ? SyntaxKind.Keyword
						: Char.IsAsciiDigit(word[0]) || word[0] is '+' or '-' || Double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? SyntaxKind.Number
						: SyntaxKind.Plain;
					writer.Part(index, stop, kind);
					index = stop;
				}
				else
				{
					index++;
				}
			}

			return null;
		}
	}
}
