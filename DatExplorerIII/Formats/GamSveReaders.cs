namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Models;
	using Services;

	/// <summary>
	/// Reads the .gam files of Fallout: a header line (<c>GAME_GLOBAL_VARS:</c> or <c>MAP_GLOBAL_VARS:</c>), then a line for each
	/// variable, <c>NAME := value;  // (number)  // comment</c> (the semicolon may be missing). A line that starts with <c>//</c> is a comment. The scripts
	/// call a variable by its place in the list, from 0 (the number in the comment is only there for the people who read it).
	/// </summary>
	public static class GamReader
	{
		private static readonly Regex HeaderLine = new(@"^\s*([A-Za-z_]+)\s*:\s*(//.*)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		// The semicolon is optional: a few lines of the files of the game forget it, and they are variables all the same.
		private static readonly Regex VariableLine = new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*:=\s*([^;/]*?)\s*(?:;(.*)|(//.*))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private static readonly Regex NumberComment = new(@"^\s*\(\s*\d+\s*\)\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static GamFile Read(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			DecodedText decoded = TextDecoder.Decode(data, codePage);
			return Parse(decoded.Text, decoded.EncodingName);
		}

		public static GamFile Parse(String text, String encodingName = "")
		{
			String header = String.Empty;
			List<GamVariable> variables = new();
			Int32 skipped = 0;

			foreach (String line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
			{
				String trimmed = line.Trim();
				if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
				{
					continue;
				}

				Match variable = VariableLine.Match(line);
				if (variable.Success)
				{
					variables.Add(new GamVariable(variables.Count, variable.Groups[1].Value, variable.Groups[2].Value.Trim(), CommentOf(variable.Groups[3].Success ? variable.Groups[3].Value : variable.Groups[4].Value)));
					continue;
				}

				Match start = HeaderLine.Match(line);
				if (start.Success && header.Length == 0 && variables.Count == 0)
				{
					header = start.Groups[1].Value;
					continue;
				}

				skipped++;
			}

			return new GamFile(header, variables, skipped, encodingName);
		}

		/// <summary>What follows the semicolon, without the slashes and the number of the place: the words of the comment.</summary>
		private static String CommentOf(String rest)
		{
			String comment = rest.Trim();
			while (comment.StartsWith("//", StringComparison.Ordinal))
			{
				comment = NumberComment.Replace(comment[2..].TrimStart(), String.Empty);
			}

			return comment.Trim();
		}
	}

	/// <summary>
	/// Reads the .sve files of Fallout: the subtitles of a movie, a line each, <c>picture:text</c>. A subtitle shows when the
	/// movie gets to its picture and is replaced by the next one.
	/// </summary>
	public static class SveReader
	{
		private static readonly Regex Line = new(@"^\s*(\d+)\s*:(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public static SveFile Read(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			DecodedText decoded = TextDecoder.Decode(data, codePage);
			return Parse(decoded.Text, decoded.EncodingName);
		}

		public static SveFile Parse(String text, String encodingName = "")
		{
			List<SveEntry> entries = new();
			Int32 skipped = 0;

			foreach (String line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
			{
				if (line.Trim().Length == 0)
				{
					continue;
				}

				Match match = Line.Match(line);
				if (match.Success && Int32.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out Int32 frame))
				{
					entries.Add(new SveEntry(frame, match.Groups[2].Value.Trim()));
				}
				else
				{
					skipped++;
				}
			}

			// Ordered by picture (the order of the file is kept for two subtitles on the same picture).
			return new SveFile(entries.OrderBy(entry => entry.Frame).ToList(), skipped, encodingName);
		}
	}
}