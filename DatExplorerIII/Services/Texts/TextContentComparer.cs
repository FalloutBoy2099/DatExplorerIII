namespace DatExplorerIII.Services.Texts
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Core;
	using Formats.GameFiles;
	using Models.Archives;
	using Models.Comparison;
	using Models.GameFiles;
	using Scripts;
	using Services.Platform;

	/// <summary>A remark about two texts whose lines are the same: something else differs (the line endings, the encoding...).</summary>
	/// <param name="Key">The key of the text (<c>Str.*</c>).</param>
	/// <param name="Args">What fills the text.</param>
	public sealed record DiffNote(String Key, params Object[] Args);

	/// <summary>
	/// Compares the content of two files made of text for the comparison of two archives: the lines as the options say, and the
	/// way the text is written (the line endings, a last line break, the encoding) unless the options ignore that.
	/// </summary>
	public static class TextContentComparer
	{
		/// <summary>How the comparison of the files of two archives treats the files made of text; null when it is byte for byte.</summary>
		public static TextEquality? CreateEquality(ComparisonOptions options, Boolean messageIdsOnly = false)
		{
			if (messageIdsOnly)
			{
				// The message files are the same when they hold the same ids; the other files are compared as the options say.
				Boolean binary = options.Method == CompareMethod.Binary;
				return new TextEquality(
					name => IsMessageFile(name) || (!binary && AppliesTo(name)),
					(left, right) => IsMessageFile(left.Name)
						? MessageIdsEqual(left, right)
						: !binary && AreEqual(left, right, options));
			}

			if (options.Method == CompareMethod.Binary)
				return null;

			return new TextEquality(AppliesTo, (left, right) => AreEqual(left, right, options));
		}

		/// <summary>Whether a file is a message file: .msg (Fallout) or .mes (Arcanum).</summary>
		public static Boolean IsMessageFile(String fileName)
		{
			String extension = Path.GetExtension(fileName);
			return String.Equals(extension, ".msg", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".mes", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>The ids of the messages of a message file, in order and without repeats; null when the file cannot be read as one.</summary>
		public static IReadOnlyList<Int32>? MessageIds(String fileName, Byte[] data)
		{
			if (data.Length > FileTextView.MaxBytes)
				return null;

			DecodedText decoded = TextDecoder.Decode(data);
			if (decoded.LooksBinary)
				return null;

			SortedSet<Int32> ids = [];
			if (String.Equals(Path.GetExtension(fileName), ".mes", StringComparison.OrdinalIgnoreCase))
			{
				foreach (MesEntry entry in MesReader.Parse(decoded.Text).Entries)
				{
					ids.Add(entry.Number);
				}
			}
			else
			{
				// {id}{audio}{text}: the values go by three; one that is not a number cannot start a message.
				List<String> values = BraceText.ReadValues(decoded.Text);
				Int32 index = 0;
				while (index < values.Count)
				{
					if (index + 2 < values.Count && BraceText.TryReadNumber(values[index], out Int32 number))
					{
						ids.Add(number);
						index += 3;
					}
					else
					{
						index++;
					}
				}
			}

			return ids.ToList();
		}

		/// <summary>The ids of a message file as lines, for the display of the differences; null when it cannot be read.</summary>
		public static IReadOnlyList<String>? MessageIdLines(String fileName, Byte[] data)
		{
			return MessageIds(fileName, data)?.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
		}

		private static Boolean MessageIdsEqual(ComparedFile left, ComparedFile right)
		{
			IReadOnlyList<Int32>? leftIds = MessageIds(left.Name, left.Data);
			IReadOnlyList<Int32>? rightIds = MessageIds(right.Name, right.Data);
			return leftIds != null && rightIds != null && leftIds.SequenceEqual(rightIds);
		}

		/// <summary>
		/// Whether a file is compared as a text by the comparison of two archives: a text, or a compiled script (.int), which is
		/// compared as the source it decompiles to (two scripts that are not the same bytes can be the same program).
		/// </summary>
		public static Boolean AppliesTo(String fileName)
		{
			return IsText(fileName) || IsScript(fileName);
		}

		/// <summary>Whether a file is a compiled script: of Fallout (.int) or of Arcanum (.scr), compared as the text it is shown as.</summary>
		public static Boolean IsScript(String fileName)
		{
			String extension = Path.GetExtension(fileName);
			return String.Equals(extension, ".int", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".scr", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".ent", StringComparison.OrdinalIgnoreCase) || String.Equals(extension, ".chr", StringComparison.OrdinalIgnoreCase) || ArcanumFileText.Handles(extension);
		}

		/// <summary>
		/// Whether two versions of a file are the same: for a compiled script, the sources they decompile to (a script that cannot
		/// be decompiled is the same as another only when the bytes are), for a text, as <see cref="AreEqual(Byte[], Byte[], ComparisonOptions)"/> says.
		/// </summary>
		public static Boolean AreEqual(ComparedFile left, ComparedFile right, ComparisonOptions options)
		{
			if (!IsScript(left.Name))
				return AreEqual(left.Data, right.Data, options);

			if (left.Data.AsSpan().SequenceEqual(right.Data))
				return true;

			return FileTextView.TryGetLines(left.Name, left.Data, null, ScriptGameOf(left.Format), out IReadOnlyList<String> leftLines) == FileTextResult.Lines
				&& FileTextView.TryGetLines(right.Name, right.Data, null, ScriptGameOf(right.Format), out IReadOnlyList<String> rightLines) == FileTextResult.Lines
				&& TextDiff.AreEquivalent(leftLines, rightLines, options);
		}

		/// <summary>The game a compiled script is decompiled for.</summary>
		public static ScriptGame ScriptGameOf(DatFormat format) => format == DatFormat.Fallout1 ? ScriptGame.Fallout1 : ScriptGame.Fallout2;

		/// <summary>Whether a file is compared as a text: one the program shows as a text (not a prototype, a map...).</summary>
		public static Boolean IsText(String fileName)
		{
			return FileViewers.IsPlainText(fileName);
		}

		/// <summary>Whether two versions of a text file are the same for what <paramref name="options"/> ignores.</summary>
		public static Boolean AreEqual(Byte[] left, Byte[] right, ComparisonOptions options)
		{
			if (left.AsSpan().SequenceEqual(right))
				return true;

			if (left.Length > FileTextView.MaxBytes || right.Length > FileTextView.MaxBytes)
				return false;

			DecodedText leftText = TextDecoder.Decode(left);
			DecodedText rightText = TextDecoder.Decode(right);
			if (leftText.LooksBinary || rightText.LooksBinary)
				return false;

			return Notes(left, right, leftText, rightText, options).Count == 0
				&& TextDiff.AreEquivalent(FileTextView.SplitLines(leftText.Text), FileTextView.SplitLines(rightText.Text), options);
		}

		/// <summary>What differs in the way two texts are written, other than their lines, and is not ignored by <paramref name="options"/>.</summary>
		public static IReadOnlyList<DiffNote> Notes(Byte[] left, Byte[] right, ComparisonOptions options)
		{
			return Notes(left, right, TextDecoder.Decode(left), TextDecoder.Decode(right), options);
		}

		private static List<DiffNote> Notes(Byte[] left, Byte[] right, DecodedText leftText, DecodedText rightText, ComparisonOptions options)
		{
			List<DiffNote> notes = [];

			String leftEncoding = EncodingOf(left, leftText);
			String rightEncoding = EncodingOf(right, rightText);
			if (!options.IgnoreCodePage && !String.Equals(leftEncoding, rightEncoding, StringComparison.Ordinal))
				notes.Add(new DiffNote("Str.CompareDiffCodePage", leftEncoding, rightEncoding));

			LineEnding leftEndings = EndingsOf(leftText.Text);
			LineEnding rightEndings = EndingsOf(rightText.Text);
			if (!options.IgnoreLineEndings && leftEndings != rightEndings)
				notes.Add(new DiffNote("Str.CompareDiffEndings", Describe(leftEndings), Describe(rightEndings)));

			if (!options.IgnoreMissingFinalEol && EndsWithLineBreak(leftText.Text) != EndsWithLineBreak(rightText.Text))
				notes.Add(new DiffNote("Str.CompareDiffFinalEol"));

			return notes;
		}

		[Flags]
		private enum LineEnding
		{
			None = 0,
			Windows = 1,
			Unix = 2,
			Mac = 4,
		}

		/// <summary>The name of the encoding, with a mark when the file starts with a byte order mark.</summary>
		private static String EncodingOf(Byte[] data, DecodedText text)
		{
			Boolean mark = (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
				|| (data.Length >= 2 && ((data[0] == 0xFF && data[1] == 0xFE) || (data[0] == 0xFE && data[1] == 0xFF)));
			return mark ? text.EncodingName + " BOM" : text.EncodingName;
		}

		private static LineEnding EndingsOf(String text)
		{
			LineEnding endings = LineEnding.None;
			for (Int32 index = 0; index < text.Length; index++)
			{
				if (text[index] == '\r')
				{
					if (index + 1 < text.Length && text[index + 1] == '\n')
					{
						endings |= LineEnding.Windows;
						index++;
					}
					else
					{
						endings |= LineEnding.Mac;
					}
				}
				else if (text[index] == '\n')
				{
					endings |= LineEnding.Unix;
				}
			}

			return endings;
		}

		private static Boolean EndsWithLineBreak(String text)
		{
			return text.Length > 0 && text[^1] is '\r' or '\n';
		}

		private static String Describe(LineEnding endings)
		{
			if (endings == LineEnding.None)
				return "-";

			return String.Join(" + ", new[]
			{
				endings.HasFlag(LineEnding.Windows) ? "Windows (CRLF)" : null,
				endings.HasFlag(LineEnding.Unix) ? "Unix (LF)" : null,
				endings.HasFlag(LineEnding.Mac) ? "Mac (CR)" : null,
			}.OfType<String>());
		}
	}
}