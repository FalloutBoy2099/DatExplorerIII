namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Splits the source of a Lua script (<c>.lua</c>, Lua 5.1 to 5.4) into the parts that get a color: comments (<c>--</c> to the end
	/// of the line, and <c>--[[ ]]</c> over several lines), the words of the language, texts (with quotation marks or between long
	/// brackets), numbers and the names that are called as functions.
	/// </summary>
	public static class LuaSyntax
	{
		/// <summary>The reserved words of Lua, with the values <c>true</c>, <c>false</c> and <c>nil</c>.</summary>
		private static readonly HashSet<String> Keywords = new(StringComparer.Ordinal)
		{
			"and", "break", "do", "else", "elseif", "end", "false", "for", "function", "goto", "if", "in",
			"local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
		};

		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			List<SyntaxSpan> spans = [];
			Int32 position = 0;

			// A first line that starts with # (the line of the interpreter of a script that is run directly) is not Lua: a comment.
			if (text.StartsWith('#'))
			{
				Int32 lineEnd = EndOfLine(text, 0);
				SyntaxSpans.Add(spans, 0, lineEnd, SyntaxKind.Comment);
				position = lineEnd;
			}

			// True after the word function: the name that follows is a function.
			Boolean afterFunction = false;

			while (position < text.Length)
			{
				Char current = text[position];
				Int32 end;

				if (current is '\r' or '\n' or ' ' or '\t')
				{
					end = SkipWhile(text, position, character => character is '\r' or '\n' or ' ' or '\t');
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Plain);
				}
				else if (current == '-' && position + 1 < text.Length && text[position + 1] == '-')
				{
					// A long comment, --[[ ... ]] (with equal signs between the brackets, if any), or a line comment.
					Int32 level = LongBracketLevel(text, position + 2);
					end = level >= 0 ? EndOfLongBracket(text, position + 2, level) : EndOfLine(text, position);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Comment);
				}
				else if (current is '"' or '\'')
				{
					end = EndOfString(text, position);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.String);
				}
				else if (current == '[' && LongBracketLevel(text, position) >= 0)
				{
					end = EndOfLongBracket(text, position, LongBracketLevel(text, position));
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.String);
				}
				else if (Char.IsAsciiDigit(current) || (current == '.' && position + 1 < text.Length && Char.IsAsciiDigit(text[position + 1])))
				{
					end = EndOfNumber(text, position);
					SyntaxSpans.Add(spans, position, end - position, SyntaxKind.Number);
				}
				else if (IsWordStart(current))
				{
					end = SkipWhile(text, position, IsWordCharacter);
					String word = text.Substring(position, end - position);
					SyntaxKind kind;
					if (Keywords.Contains(word))
					{
						kind = SyntaxKind.Keyword;
						afterFunction = word == "function";
						SyntaxSpans.Add(spans, position, end - position, kind);
						position = end;
						continue;
					}

					// A name that is called (name(...), or a text or a table given to it), and the name of a function being defined.
					kind = afterFunction || IsCalled(text, end) ? SyntaxKind.Function : SyntaxKind.Plain;
					afterFunction = false;
					SyntaxSpans.Add(spans, position, end - position, kind);
				}
				else
				{
					end = position + 1;
					SyntaxSpans.Add(spans, position, 1, SyntaxKind.Plain);

					// A dot or a colon goes between the parts of a name (a.b.c, obj:method): the function is the last part.
					if (current is not ('.' or ':'))
						afterFunction = false;
				}

				position = end;
			}

			return spans;
		}

		private static Boolean IsWordStart(Char character) => Char.IsAsciiLetter(character) || character == '_';

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

		/// <summary>The end of the line (before its break); the position of the break, or the end of the text.</summary>
		private static Int32 EndOfLine(String text, Int32 start)
		{
			return SkipWhile(text, start, character => character is not ('\r' or '\n'));
		}

		/// <summary>True when the word that ends at the position is followed by a call: an opening parenthesis, a text or a table.</summary>
		private static Boolean IsCalled(String text, Int32 end)
		{
			Int32 position = SkipWhile(text, end, character => character is ' ' or '\t');
			return position < text.Length && text[position] is '(' or '"' or '\'' or '{';
		}

		/// <summary>The number of equal signs of a long bracket that starts at the position ([[ is 0, [=[ is 1), or -1 when there is none.</summary>
		private static Int32 LongBracketLevel(String text, Int32 start)
		{
			if (start >= text.Length || text[start] != '[')
				return -1;

			Int32 position = start + 1;
			while (position < text.Length && text[position] == '=')
			{
				position++;
			}

			return position < text.Length && text[position] == '[' ? position - start - 1 : -1;
		}

		/// <summary>The end of a long bracket text or comment (after its closing bracket); the end of the text when it is not closed.</summary>
		private static Int32 EndOfLongBracket(String text, Int32 start, Int32 level)
		{
			String closing = "]" + new String('=', level) + "]";
			Int32 close = text.IndexOf(closing, start + level + 2, StringComparison.Ordinal);
			return close < 0 ? text.Length : close + closing.Length;
		}

		/// <summary>The end of a text between quotation marks; one that is not closed on its line ends with the line.</summary>
		private static Int32 EndOfString(String text, Int32 start)
		{
			Char quote = text[start];
			Int32 position = start + 1;
			while (position < text.Length && text[position] is not ('\r' or '\n'))
			{
				if (text[position] == '\\' && position + 1 < text.Length)
				{
					// The character after a backslash is part of the text (a quotation mark does not end it).
					position += 2;
					continue;
				}

				if (text[position] == quote)
					return position + 1;

				position++;
			}

			return position;
		}

		/// <summary>The end of a number: decimal with a point and an exponent, or hexadecimal (with a point and a binary exponent).</summary>
		private static Int32 EndOfNumber(String text, Int32 start)
		{
			Boolean hexadecimal = text[start] == '0' && start + 1 < text.Length && text[start + 1] is 'x' or 'X';
			Int32 position = hexadecimal ? start + 2 : start;
			while (position < text.Length)
			{
				Char character = text[position];
				Boolean exponent = hexadecimal ? character is 'p' or 'P' : character is 'e' or 'E';
				if (exponent && position + 1 < text.Length && text[position + 1] is '+' or '-')
				{
					position += 2;
				}
				else if (Char.IsAsciiHexDigit(character) && (hexadecimal || Char.IsAsciiDigit(character)) || character == '.' || exponent)
				{
					position++;
				}
				else
				{
					break;
				}
			}

			return position;
		}
	}
}