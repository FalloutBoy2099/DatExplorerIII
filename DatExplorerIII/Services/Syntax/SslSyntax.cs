namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;
	using Scripts;

	/// <summary>
	/// Splits the source of a script of Fallout (<c>.ssl</c>, its headers <c>.h</c>, or a compiled script written back as source)
	/// into the parts that get a color: comments, the words of the language, the lines of the preprocessor, texts, numbers and the
	/// functions of the game.
	/// </summary>
	public static class SslSyntax
	{
		/// <summary>Longest text that gets colors: a larger one is shown as it is, because coloring it would be slow.</summary>
		public const Int32 MaxLength = 512 * 1024;

		/// <summary>The words of the language (the compilers do not tell upper case from lower case).</summary>
		private static readonly HashSet<String> Keywords = new(StringComparer.OrdinalIgnoreCase)
		{
			"procedure", "begin", "end", "variable", "import", "export", "critical",
			"if", "then", "else", "while", "do", "for", "foreach", "in", "switch", "case", "default",
			"call", "return", "break", "continue", "when",
			"and", "or", "not", "bwand", "bwor", "bwxor", "bwnot", "andalso", "orelse",
		};

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = [];
			Int32 position = 0;

			// True while only spaces came before on the line: a # there starts a line of the preprocessor.
			Boolean lineStart = true;

			while (position < text.Length)
			{
				Char current = text[position];
				Char next = position + 1 < text.Length ? text[position + 1] : '\0';
				Int32 end;

				if (current is '\r' or '\n')
				{
					end = SkipWhile(text, position, character => character is '\r' or '\n');
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
					lineStart = true;
				}
				else if (current is ' ' or '\t')
				{
					end = SkipWhile(text, position, character => character is ' ' or '\t');
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
				}
				else if (current == '/' && next == '/')
				{
					end = SkipWhile(text, position, character => character is not ('\r' or '\n'));
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Comment);
					lineStart = false;
				}
				else if (current == '/' && next == '*')
				{
					// A comment can run over several lines; one that is not closed runs to the end.
					Int32 close = text.IndexOf("*/", position + 2, StringComparison.Ordinal);
					end = close < 0 ? text.Length : close + 2;
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Comment);
					lineStart = false;
				}
				else if (current == '"')
				{
					end = EndOfString(text, position);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.String);
					lineStart = false;
				}
				else if (current == '#' && lineStart)
				{
					end = SkipWhile(text, position + 1, IsWordCharacter);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Directive);
					lineStart = false;
				}
				else if (Char.IsAsciiDigit(current) || (current == '.' && Char.IsAsciiDigit(next)))
				{
					end = EndOfNumber(text, position);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Number);
					lineStart = false;
				}
				else if (Char.IsAsciiLetter(current) || current == '_')
				{
					end = SkipWhile(text, position, IsWordCharacter);
					String word = text.Substring(position, end - position);
					SyntaxKind kind = Keywords.Contains(word) ? SyntaxKind.Keyword : OpcodeTable.FunctionNames.Contains(word) ? SyntaxKind.Function : SyntaxKind.Plain;
					SyntaxSpans.Add(spans, position, end - position, kind);
					lineStart = false;
				}
				else
				{
					end = position + 1;
					SyntaxSpans.Add(spans, position, 1, SyntaxKind.Plain);
					lineStart = false;
				}

				position = end;
			}

			return spans;
		}

		private static Boolean IsWordCharacter(Char character) => Char.IsAsciiLetterOrDigit(character) || character == '_';

		private static Int32 SkipWhile(String text, Int32 start, Func<Char, Boolean> predicate)
		{
			Int32 end = start;
			while (end < text.Length && predicate(text[end]))
			{
				end++;
			}

			return end;
		}

		/// <summary>The end of a text between quotation marks: after the closing mark, or at the end of the line when it is not closed.</summary>
		private static Int32 EndOfString(String text, Int32 start)
		{
			Int32 position = start + 1;
			while (position < text.Length && text[position] is not ('"' or '\r' or '\n'))
			{
				// A backslash protects the character after it (a quotation mark, a backslash).
				position += text[position] == '\\' && position + 1 < text.Length && text[position + 1] is not ('\r' or '\n') ? 2 : 1;
			}

			return position < text.Length && text[position] == '"' ? position + 1 : Math.Min(position, text.Length);
		}

		/// <summary>The end of a number: hexadecimal (<c>0x1F</c>), or decimal with a point and an exponent.</summary>
		private static Int32 EndOfNumber(String text, Int32 start)
		{
			if (text[start] == '0' && start + 1 < text.Length && text[start + 1] is 'x' or 'X')
				return SkipWhile(text, start + 2, Char.IsAsciiHexDigit);

			Int32 position = SkipWhile(text, start, Char.IsAsciiDigit);
			if (position < text.Length && text[position] == '.')
				position = SkipWhile(text, position + 1, Char.IsAsciiDigit);

			return position;
		}
	}
}