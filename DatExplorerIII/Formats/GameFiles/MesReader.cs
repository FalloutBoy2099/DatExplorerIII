namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Models.GameFiles;
	using Services.Texts;

	/// <summary>
	/// Reads the message files (.mes) of Arcanum: pairs of braces, a number then a text, <c>{5000}{The lever won't budge.}</c>.
	/// The text of the game is in 8 bit characters of the language of the game, whose encoding is found like the one of any text.
	/// </summary>
	public static class MesReader
	{
		/// <summary>
		/// Reads a message file.
		/// </summary>
		/// <param name="data">The bytes of the file.</param>
		/// <param name="codePage">The code page to read the text with, or <see cref="TextEncodingOption.Automatic"/> to detect it.</param>
		public static MesFile Read(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			DecodedText decoded = TextDecoder.Decode(data, codePage);
			return Parse(decoded.Text, decoded.EncodingName);
		}

		/// <summary>Reads the messages of a text that is already decoded.</summary>
		public static MesFile Parse(String text, String encodingName = "")
		{
			List<BraceText.Item> items = BraceText.ReadItems(text);
			List<String> values = items.Where(item => !item.IsComment).Select(item => item.Text).ToList();
			List<MesEntry> entries = new(values.Count / 2);
			List<Int32> lastValues = new(values.Count / 2);
			Int32 skipped = 0;

			// The values go by pairs; a value that is not a number cannot start a message, so it is skipped alone and the
			// pairs after it stay in step.
			Int32 index = 0;
			while (index < values.Count)
			{
				// An empty pair, {}{}, is a blank line that some editors leave: nothing, not damage.
				if (index + 1 < values.Count && values[index].Trim().Length == 0 && values[index + 1].Trim().Length == 0)
				{
					index += 2;
				}
				else if (index + 1 < values.Count && BraceText.TryReadNumber(values[index], out Int32 number))
				{
					entries.Add(new MesEntry(number, values[index + 1]));
					lastValues.Add(index + 1);
					index += 2;
				}
				else
				{
					skipped++;
					index++;
				}
			}

			// The comments: at the end of a line of a message, or between the messages.
			(String?[] lineComments, List<FileComment> between) = BraceText.AssignComments(items, lastValues);
			for (Int32 i = 0; i < entries.Count; i++)
			{
				if (lineComments[i] is { } comment)
					entries[i] = entries[i] with { Comment = comment };
			}

			return new MesFile(entries, skipped, encodingName) { Comments = between };
		}
	}
}