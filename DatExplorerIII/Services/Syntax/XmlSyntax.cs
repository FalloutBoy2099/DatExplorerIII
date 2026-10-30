namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>Marks the parts of an XML text: the tags and their names, the attributes and their values, the comments, the CDATA sections, the declarations and the entities.</summary>
	public static class XmlSyntax
	{
		/// <summary>The parts of a text, in order, with no gap and no overlap: together they cover the whole text.</summary>
		public static IReadOnlyList<SyntaxSpan> Tokenize(String text)
		{
			SpanWriter writer = new(text.Length);
			Int32 index = 0;
			while (index < text.Length)
			{
				if (text[index] == '&')
				{
					Int32 semicolon = text.IndexOf(';', index);
					if (semicolon > index && semicolon < Math.Min(text.Length, index + 12))
					{
						writer.Part(index, semicolon + 1, SyntaxKind.Placeholder);
						index = semicolon + 1;
						continue;
					}
				}

				if (text[index] != '<')
				{
					index++;
					continue;
				}

				if (Starts(text, index, "<!--"))
				{
					index = Block(writer, text, index, "-->", SyntaxKind.Comment);
				}
				else if (Starts(text, index, "<![CDATA["))
				{
					index = Block(writer, text, index, "]]>", SyntaxKind.String);
				}
				else if (Starts(text, index, "<?") || Starts(text, index, "<!"))
				{
					index = Block(writer, text, index, ">", SyntaxKind.Directive);
				}
				else
				{
					index = Tag(writer, text, index);
				}
			}

			return writer.Finish();
		}

		private static Boolean Starts(String text, Int32 index, String start) => String.CompareOrdinal(text, index, start, 0, start.Length) == 0;

		private static Int32 Block(SpanWriter writer, String text, Int32 start, String close, SyntaxKind kind)
		{
			Int32 end = text.IndexOf(close, start + 1, StringComparison.Ordinal);
			end = end < 0 ? text.Length : end + close.Length;
			writer.Part(start, end, kind);
			return end;
		}

		/// <summary>A tag: its brackets, its name, its attributes (a name and a value in quotes).</summary>
		private static Int32 Tag(SpanWriter writer, String text, Int32 start)
		{
			Int32 index = start + 1;
			if (index < text.Length && text[index] == '/')
				index++;

			writer.Part(start, index, SyntaxKind.Brace);
			Int32 nameEnd = index;
			while (nameEnd < text.Length && !Char.IsWhiteSpace(text[nameEnd]) && text[nameEnd] is not ('>' or '/'))
			{
				nameEnd++;
			}

			writer.Part(index, nameEnd, SyntaxKind.Keyword);
			index = nameEnd;
			while (index < text.Length && text[index] != '>')
			{
				Char character = text[index];
				if (character is '"' or '\'')
				{
					Int32 close = text.IndexOf(character, index + 1);
					Int32 end = close < 0 ? text.Length : close + 1;
					writer.Part(index, end, SyntaxKind.String);
					index = end;
				}
				else if (Char.IsWhiteSpace(character) || character is '=' or '/')
				{
					index++;
				}
				else
				{
					Int32 end = index;
					while (end < text.Length && !Char.IsWhiteSpace(text[end]) && text[end] is not ('=' or '>' or '/' or '"' or '\''))
					{
						end++;
					}

					writer.Part(index, end, SyntaxKind.Identifier);
					index = Math.Max(end, index + 1);
				}
			}

			// The end of the tag: > or />.
			Int32 closing = index < text.Length ? index + 1 : index;
			Int32 bracket = index > start + 1 && index <= text.Length && text[index - 1] == '/' ? index - 1 : index;
			writer.Part(Math.Max(bracket, writer.Position), closing, SyntaxKind.Brace);
			return closing;
		}
	}
}
