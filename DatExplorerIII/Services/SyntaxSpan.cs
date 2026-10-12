namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;

	/// <summary>What a part of a text is, for the colors of the viewer (message files and scripts).</summary>
	public enum SyntaxKind
	{
		/// <summary>Text with no meaning of its own: spaces, line breaks, operators and punctuation.</summary>
		Plain,

		/// <summary>A comment (in a message file: a line that starts with #, or anything written outside the braces).</summary>
		Comment,

		/// <summary>A brace that opens or closes a part of a line of a message file.</summary>
		Brace,

		/// <summary>The number that names a line, in the first braces.</summary>
		Identifier,

		/// <summary>The name of the sound file that is played with the line, in the second braces.</summary>
		Sound,

		/// <summary>The text that is shown, in the third braces.</summary>
		Message,

		/// <summary>A place in a text where the game puts a value (%s, %d...).</summary>
		Placeholder,

		/// <summary>The name of a file that a list names and that is not there.</summary>
		Missing,

		/// <summary>A word of the language of the scripts (<c>procedure</c>, <c>begin</c>, <c>if</c>...).</summary>
		Keyword,

		/// <summary>A line for the preprocessor of the scripts (<c>#define</c>, <c>#include</c>...): the word that starts it.</summary>
		Directive,

		/// <summary>A text between quotation marks in a script.</summary>
		String,

		/// <summary>A number in a script (decimal, hexadecimal, with a decimal point).</summary>
		Number,

		/// <summary>The name of a function of the game that a script can call.</summary>
		Function,

		/// <summary>What does not fit in the place the game has for it: the characters of a biography past the end of a line, the lines past the last one.</summary>
		Overflow,
	}

	/// <summary>A part of a text, with its kind.</summary>
	public readonly record struct SyntaxSpan(Int32 Start, Int32 Length, SyntaxKind Kind);

	/// <summary>Builds the list of the parts of a text.</summary>
	internal static class SyntaxSpans
	{
		/// <summary>Adds a part; nothing is added for an empty one, and neighbors of the same kind are one part.</summary>
		public static void Add(List<SyntaxSpan> spans, Int32 start, Int32 length, SyntaxKind kind)
		{
			if (length <= 0)
			{
				return;
			}

			if (spans.Count > 0 && spans[^1].Kind == kind && spans[^1].Start + spans[^1].Length == start)
			{
				spans[^1] = spans[^1] with { Length = spans[^1].Length + length };
				return;
			}

			spans.Add(new SyntaxSpan(start, length, kind));
		}
	}
}