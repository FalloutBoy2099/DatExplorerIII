namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using Models;
	using Services;

	/// <summary>
	/// Reads the dialog files (.dlg) of Arcanum. A line has seven values between braces:
	/// <c>{number}{text}{text for a woman}{intelligence}{conditions}{goes to}{effects}</c>. A line with an intelligence is an
	/// answer of the player; one without is what a character says.
	/// </summary>
	public static class DlgReader
	{
		private const Int32 FieldCount = 7;

		/// <summary>
		/// Reads a dialog file.
		/// </summary>
		/// <param name="data">The bytes of the file.</param>
		/// <param name="codePage">The code page to read the text with, or <see cref="TextEncodingOption.Automatic"/> to detect it.</param>
		public static DlgFile Read(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			DecodedText decoded = TextDecoder.Decode(data, codePage);
			return Parse(decoded.Text, decoded.EncodingName);
		}

		/// <summary>Reads the lines of a text that is already decoded.</summary>
		public static DlgFile Parse(String text, String encodingName = "")
		{
			List<String> values = BraceText.ReadValues(text);
			List<DlgEntry> entries = new(values.Count / FieldCount);
			Int32 skipped = 0;

			// The values go by seven. The number starts a line: when the value where it should be is not a number, the
			// reading moves on by one value until a line starts again, so that one damaged line does not spoil the next ones.
			Int32 index = 0;
			while (index < values.Count)
			{
				// Modders leave empty lines, {}{}{}{}{}{}{}: they are nothing, not damage.
				if (index + FieldCount <= values.Count && IsBlank(values, index))
				{
					index += FieldCount;
				}
				else if (index + FieldCount <= values.Count && BraceText.TryReadNumber(values[index], out Int32 number))
				{
					entries.Add(new DlgEntry(number, values[index + 1], values[index + 2], values[index + 3], values[index + 4], values[index + 5], values[index + 6]));
					index += FieldCount;
				}
				else
				{
					skipped++;
					index++;
				}
			}

			return new DlgFile(entries, skipped, encodingName);
		}

		private static Boolean IsBlank(List<String> values, Int32 start)
		{
			for (Int32 i = start; i < start + FieldCount; i++)
			{
				if (values[i].Trim().Length > 0)
				{
					return false;
				}
			}

			return true;
		}
	}
}