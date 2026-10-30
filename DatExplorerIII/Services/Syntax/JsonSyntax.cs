namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>Marks the parts of a JSON text: the names (the strings that are followed by a colon), the strings, the numbers, true, false and null, and the comments (some files have them).</summary>
	public static class JsonSyntax
	{
		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			SpanWriter writer = new(text.Length);
			Int32 index = 0;
			while (index < text.Length)
			{
				Char character = text[index];
				if (character == '"')
				{
					Int32 end = EndOfString(text, index);
					Int32 after = end;
					while (after < text.Length && text[after] is ' ' or '\t' or '\r' or '\n')
					{
						after++;
					}

					writer.Part(index, end, after < text.Length && text[after] == ':' ? SyntaxKind.Identifier : SyntaxKind.String);
					index = end;
				}
				else if (character == '/' && index + 1 < text.Length && text[index + 1] is '/' or '*')
				{
					Int32 end = text[index + 1] == '/' ? LineEnd(text, index) : BlockEnd(text, index);
					writer.Part(index, end, SyntaxKind.Comment);
					index = end;
				}
				else if (character is '-' or (>= '0' and <= '9'))
				{
					Int32 end = index + 1;
					while (end < text.Length && (Char.IsAsciiLetterOrDigit(text[end]) || text[end] is '.' or '+' or '-'))
					{
						end++;
					}

					writer.Part(index, end, SyntaxKind.Number);
					index = end;
				}
				else if (Char.IsAsciiLetter(character))
				{
					Int32 end = index + 1;
					while (end < text.Length && Char.IsAsciiLetter(text[end]))
					{
						end++;
					}

					writer.Part(index, end, SyntaxKind.Keyword);
					index = end;
				}
				else
				{
					index++;
				}
			}

			return writer.Finish();
		}

		private static Int32 EndOfString(String text, Int32 start)
		{
			Int32 index = start + 1;
			while (index < text.Length && text[index] is not ('"' or '\n'))
			{
				index += text[index] == '\\' ? 2 : 1;
			}

			return Math.Min(text.Length, index < text.Length && text[index] == '"' ? index + 1 : index);
		}

		private static Int32 LineEnd(String text, Int32 start)
		{
			Int32 end = text.IndexOf('\n', start);
			return end < 0 ? text.Length : end;
		}

		private static Int32 BlockEnd(String text, Int32 start)
		{
			Int32 end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);
			return end < 0 ? text.Length : end + 2;
		}
	}
}
