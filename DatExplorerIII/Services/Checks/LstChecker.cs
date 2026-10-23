namespace DatExplorerIII.Services.Checks
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Core;
	using Models.Archives;

	/// <summary>A line of a list that names a file that is not there.</summary>
	/// <param name="Line">The line of the list, from 1.</param>
	/// <param name="Name">What the line names (a file name, or the beginning of the names of the files of a critter).</param>
	public sealed record LstMissingFile(Int32 Line, String Name);

	/// <summary>What the check of a list of files found.</summary>
	/// <param name="Missing">The lines that name a file that is not in the folder of the list.</param>
	/// <param name="Unlisted">The files of the folder that the list does not name (only for the list of a folder of the art or of the prototypes).</param>
	public sealed record LstReport(IReadOnlyList<LstMissingFile> Missing, IReadOnlyList<String> Unlisted)
	{
		public Boolean HasProblems => Missing.Count > 0 || Unlisted.Count > 0;

		/// <summary>The lines of the list that name a file that is not there (from 1).</summary>
		public IReadOnlySet<Int32> MissingLines => Missing.Select(file => file.Line).ToHashSet();
	}

	/// <summary>
	/// The lists of files of Fallout (<c>.lst</c>): a line names a file of the folder of the list (the first word of the line, the
	/// rest is data), and the place of the line is the number that the game uses to find the file. The check says which files a list
	/// names that are not in its folder, and which files of the folder it does not name.
	/// </summary>
	public static class LstChecker
	{
		/// <summary>The name on a line of a list: up to a space, a comma, a semicolon (a comment) or a tab.</summary>
		private static readonly Regex Name = new(@"^[^ ,;\t]+", RegexOptions.CultureInvariant);

		/// <summary>The lists of the sounds have another format (a count, then four lines for each sound).</summary>
		private const String SoundList = "sndlist.lst";

		/// <summary>
		/// Checks a list against the files of its folder.
		/// </summary>
		/// <param name="directory">The folder of the list in its archive.</param>
		/// <param name="fileName">The name of the list.</param>
		/// <param name="text">The content of the list.</param>
		/// <param name="archives">The archives whose files count (the game reads the files of all its archives as one).</param>
		/// <returns>The result, or null for a list that does not name files of its folder (the lists of the sounds, of patterns...).</returns>
		public static LstReport? Check(String directory, String fileName, String text, IEnumerable<DatArchive> archives)
		{
			if (fileName.Equals(SoundList, StringComparison.OrdinalIgnoreCase))
				return null;

			String folder = directory.Replace('/', '\\').Trim('\\');
			List<(Int32 Line, String Name)> entries = ReadNames(text);

			// The pictures of the critters and of the heads are named by the beginning of their file names (hmwarr for hmwarraa.frm...).
			Boolean byPrefix = folder.Equals("art\\critters", StringComparison.OrdinalIgnoreCase) || folder.Equals("art\\heads", StringComparison.OrdinalIgnoreCase);
			if (!byPrefix && !entries.Any(entry => entry.Name.Contains('.', StringComparison.Ordinal)))
				return null;

			List<String> files = [];
			foreach (DatArchive archive in archives)
			{
				foreach (DatEntry entry in archive.Entries)
				{
					if (!entry.IsDamaged && entry.Directory.Replace('/', '\\').Trim('\\').Equals(folder, StringComparison.OrdinalIgnoreCase))
						files.Add(entry.Name);
				}
			}

			HashSet<String> names = new(files, StringComparer.OrdinalIgnoreCase);
			List<LstMissingFile> missing = [];
			foreach ((Int32 line, String name) in entries)
			{
				Boolean there = name.Contains('.', StringComparison.Ordinal)
					? names.Contains(name)
					: !byPrefix || files.Any(file => file.StartsWith(name, StringComparison.OrdinalIgnoreCase));
				if (!there)
					missing.Add(new LstMissingFile(line, name));
			}

			return new LstReport(missing, IsListOfItsFolder(folder, fileName) ? FindUnlisted(files, entries, byPrefix) : Array.Empty<String>());
		}

		/// <summary>The first word of each line that has one, with the number of the line.</summary>
		public static List<(Int32 Line, String Name)> ReadNames(String text)
		{
			List<(Int32, String)> names = [];
			String[] lines = text.TrimStart('﻿').Split('\n');
			for (Int32 index = 0; index < lines.Length; index++)
			{
				Match match = Name.Match(lines[index].TrimEnd('\r'));
				if (match.Success)
					names.Add((index + 1, match.Value));
			}

			return names;
		}

		/// <summary>Whether the list is the one of its folder (items.lst in art\items), which names all the files of the folder.</summary>
		private static Boolean IsListOfItsFolder(String folder, String fileName)
		{
			Boolean known = folder.StartsWith("art\\", StringComparison.OrdinalIgnoreCase) || folder.StartsWith("proto\\", StringComparison.OrdinalIgnoreCase);
			String last = folder[(folder.LastIndexOf('\\') + 1)..];
			return known && System.IO.Path.GetFileNameWithoutExtension(fileName).Equals(last, StringComparison.OrdinalIgnoreCase);
		}

		private static IReadOnlyList<String> FindUnlisted(List<String> files, List<(Int32 Line, String Name)> entries, Boolean byPrefix)
		{
			IEnumerable<String> candidates;
			if (byPrefix)
			{
				String[] bases = entries.Select(entry => entry.Name).ToArray();
				candidates = files
					.Where(file => System.IO.Path.GetExtension(file).StartsWith(".fr", StringComparison.OrdinalIgnoreCase))
					.Where(file => !bases.Any(name => file.StartsWith(name, StringComparison.OrdinalIgnoreCase)));
			}
			else
			{
				HashSet<String> listed = new(entries.Select(entry => entry.Name), StringComparer.OrdinalIgnoreCase);
				HashSet<String> extensions = new(entries.Select(entry => System.IO.Path.GetExtension(entry.Name)).Where(extension => extension.Length > 0), StringComparer.OrdinalIgnoreCase);
				candidates = files.Where(file => extensions.Contains(System.IO.Path.GetExtension(file)) && !listed.Contains(file));
			}

			return candidates
				.Where(file => !file.EndsWith(".lst", StringComparison.OrdinalIgnoreCase))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
	}
}