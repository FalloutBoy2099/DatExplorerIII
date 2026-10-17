namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using Models;

	/// <summary>
	/// The text format of the data files of Arcanum (.mes, .dlg): values between braces, one after the other. What is outside
	/// of the braces is ignored, and a comment starts with two slashes and goes to the end of the line.
	/// </summary>
	internal static class BraceText
	{
		/// <summary>A value between braces, or a comment (its text, after the two slashes, without the spaces around it).</summary>
		/// <param name="IsOwnLine">For a comment: true when nothing but spaces comes before it on its line (a comment between the lines of the file), false when it follows something (the end of a line).</param>
		public readonly record struct Item(Boolean IsComment, String Text, Boolean IsOwnLine);

		/// <summary>
		/// Reads every value between braces, in order. A value can hold several lines. Braces that are never closed give a
		/// last value that is cut at the end of the text.
		/// </summary>
		public static List<String> ReadValues(String text)
		{
			return ReadItems(text).Where(item => !item.IsComment).Select(item => item.Text).ToList();
		}

		/// <summary>The values between braces and the comments, in the order of the text.</summary>
		public static List<Item> ReadItems(String text)
		{
			List<Item> items = new();
			Int32 position = 0;

			while (position < text.Length)
			{
				Char character = text[position];

				if (character == '/' && position + 1 < text.Length && text[position + 1] == '/')
				{
					// A comment: to the end of the line.
					Int32 end = text.IndexOf('\n', position);
					Int32 stop = end < 0 ? text.Length : end;
					items.Add(new Item(true, text.Substring(position + 2, stop - position - 2).Trim(), IsOwnLine(text, position)));
					position = end < 0 ? text.Length : end + 1;
				}
				else if (character == '{')
				{
					Int32 end = text.IndexOf('}', position + 1);
					if (end < 0)
					{
						items.Add(new Item(false, text.Substring(position + 1), false));
						break;
					}

					items.Add(new Item(false, text.Substring(position + 1, end - position - 1), false));
					position = end + 1;
				}
				else
				{
					position++;
				}
			}

			return items;
		}

		/// <summary>True when only spaces come before the position on its line.</summary>
		private static Boolean IsOwnLine(String text, Int32 position)
		{
			for (Int32 i = position - 1; i >= 0 && text[i] != '\n'; i--)
			{
				if (text[i] is not (' ' or '\t' or '\r'))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Says where the comments of a file go. A comment that ends a line belongs to the entry that line ends (null for an entry that
		/// has none); a comment on its own line, or at the end of a line that is not the end of an entry, comes between entries.
		/// </summary>
		/// <param name="items">What <see cref="ReadItems"/> read.</param>
		/// <param name="lastValues">For each entry, in order, the index (among the values, comments not counted) of the last value it is made of.</param>
		public static (String?[] LineComments, List<FileComment> Between) AssignComments(IReadOnlyList<Item> items, IReadOnlyList<Int32> lastValues)
		{
			String?[] lineComments = new String?[lastValues.Count];
			List<FileComment> between = new();
			Int32 values = 0;
			Int32 entriesBefore = 0;

			foreach (Item item in items)
			{
				if (!item.IsComment)
				{
					values++;
					continue;
				}

				// The entries that are over before this place.
				while (entriesBefore < lastValues.Count && lastValues[entriesBefore] < values)
				{
					entriesBefore++;
				}

				Int32 endsEntry = !item.IsOwnLine && entriesBefore > 0 && lastValues[entriesBefore - 1] == values - 1 ? entriesBefore - 1 : -1;
				if (endsEntry >= 0)
				{
					lineComments[endsEntry] = lineComments[endsEntry] == null ? item.Text : lineComments[endsEntry] + " " + item.Text;
				}
				else
				{
					between.Add(new FileComment(entriesBefore, item.Text));
				}
			}

			return (lineComments, between);
		}

		/// <summary>Reads the number at the start of a value (spaces around it are ignored); false when there is none.</summary>
		public static Boolean TryReadNumber(String value, out Int32 number)
		{
			return Int32.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number);
		}
	}
}