namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// The text format of the data files of Arcanum (.mes, .dlg): values between braces, one after the other. What is outside
	/// of the braces is ignored, and a comment starts with two slashes and goes to the end of the line.
	/// </summary>
	internal static class BraceText
	{
		/// <summary>
		/// Reads every value between braces, in order. A value can hold several lines. Braces that are never closed give a
		/// last value that is cut at the end of the text.
		/// </summary>
		public static List<String> ReadValues(String text)
		{
			List<String> values = new();
			Int32 position = 0;

			while (position < text.Length)
			{
				Char character = text[position];

				if (character == '/' && position + 1 < text.Length && text[position + 1] == '/')
				{
					// A comment: to the end of the line.
					Int32 end = text.IndexOf('\n', position);
					position = end < 0 ? text.Length : end + 1;
				}
				else if (character == '{')
				{
					Int32 end = text.IndexOf('}', position + 1);
					if (end < 0)
					{
						values.Add(text.Substring(position + 1));
						break;
					}

					values.Add(text.Substring(position + 1, end - position - 1));
					position = end + 1;
				}
				else
				{
					position++;
				}
			}

			return values;
		}

		/// <summary>Reads the number at the start of a value (spaces around it are ignored); false when there is none.</summary>
		public static Boolean TryReadNumber(String value, out Int32 number)
		{
			return Int32.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number);
		}
	}
}