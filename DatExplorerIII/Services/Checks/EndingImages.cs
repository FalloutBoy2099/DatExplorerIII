namespace DatExplorerIII.Services.Checks
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Core;
	using Models.Archives;
	using Services.Texts;

	/// <summary>Why a picture can be an image of the ending of Fallout 2, or why it cannot.</summary>
	public enum EndingKind
	{
		/// <summary>A slide: the first frame is 640 × 480.</summary>
		Standard,

		/// <summary>The picture that is panned: 480 high and wider than 640 (the game moves a window of 640 × 480 across it).</summary>
		Panning,

		/// <summary>The picture is not in art\intrface, where the game reads the pictures of the endings.</summary>
		WrongFolder,

		/// <summary>The first frame is not 640 × 480 (nor a panning picture).</summary>
		WrongSize,
	}

	/// <summary>What the check of a picture found.</summary>
	/// <param name="Kind">Whether it can be an image of the ending, and if not why.</param>
	/// <param name="Width">The width of its first frame.</param>
	/// <param name="Height">The height of its first frame.</param>
	/// <param name="Folder">The folder of the picture in its archive.</param>
	/// <param name="IsDeathScreen">The picture of the death screen (death.frm): its text is wider.</param>
	public sealed record EndingImageCheck(EndingKind Kind, Int32 Width, Int32 Height, String Folder, Boolean IsDeathScreen)
	{
		public Boolean IsUsable => Kind is EndingKind.Standard or EndingKind.Panning;

		/// <summary>The width, in pixels of the picture, that the text of the game is wrapped to.</summary>
		public Int32 TextWidth => IsDeathScreen ? EndingImages.DeathTextWidth : EndingImages.SlideTextWidth;
	}

	/// <summary>
	/// The pictures of the ending of Fallout 2 (the slides, and the death screen): a picture of art\intrface that has its own palette
	/// and a first frame of 640 × 480, the game copies that area. The texts shown over them are in text\language\cuts.
	/// </summary>
	public static class EndingImages
	{
		public const Int32 ScreenWidth = 640;

		public const Int32 ScreenHeight = 480;

		/// <summary>The width the subtitles of a slide are wrapped to.</summary>
		public const Int32 SlideTextWidth = 540;

		/// <summary>The width the text of the death screen is wrapped to.</summary>
		public const Int32 DeathTextWidth = 560;

		/// <summary>The folder of the game where the pictures of the endings (and their palettes) are read.</summary>
		public const String Folder = "art\\intrface";

		/// <summary>What is shown when the archives have no text of the endings (the first words of the introduction).</summary>
		public const String SampleText = "War. War never changes.";

		private static readonly Regex CutsFolder = new(@"^text\\(?<language>[^\\]+)\\cuts$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>Checks a picture that has its own palette: whether the game can use it as an image of the ending.</summary>
		/// <param name="directory">The folder of the picture in its archive.</param>
		/// <param name="name">The name of the file.</param>
		/// <param name="width">The width of its first frame.</param>
		/// <param name="height">The height of its first frame.</param>
		public static EndingImageCheck Check(String directory, String name, Int32 width, Int32 height)
		{
			String folder = directory.Replace('/', '\\').Trim('\\');
			EndingKind kind;
			if (!folder.Equals(Folder, StringComparison.OrdinalIgnoreCase))
			{
				kind = EndingKind.WrongFolder;
			}
			else if (height == ScreenHeight && width == ScreenWidth)
			{
				kind = EndingKind.Standard;
			}
			else if (height == ScreenHeight && width > ScreenWidth)
			{
				kind = EndingKind.Panning;
			}
			else
			{
				kind = EndingKind.WrongSize;
			}

			return new EndingImageCheck(kind, width, height, folder, System.IO.Path.GetFileNameWithoutExtension(name).Equals("death", StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>The name of the folder of a language in the texts of the game ("french"), for the code of a language of the program.</summary>
		public static String LanguageFolder(String code)
		{
			return code switch
			{
				"fr" => "french",
				"de" => "german",
				"es" => "spanish",
				"it" => "italian",
				_ => "english",
			};
		}

		/// <summary>The texts of a file of subtitles: each line is "number:text", the game ignores what comes before the first colon.</summary>
		public static IReadOnlyList<String> Records(String text)
		{
			List<String> records = [];
			foreach (String line in text.Split('\n'))
			{
				String record = line.Trim();
				Int32 colon = record.IndexOf(':', StringComparison.Ordinal);
				if (colon >= 0)
					record = record[(colon + 1)..].Trim();

				if (record.Length > 0)
					records.Add(record);
			}

			return records;
		}

		/// <summary>The files of subtitles of the endings, in the language that is asked when the archives have it (else English, else any).</summary>
		/// <param name="death">The texts of the death screen (nar_dth...) instead of those of the slides.</param>
		public static IReadOnlyList<(DatArchive Archive, DatEntry Entry)> TextFiles(IEnumerable<DatArchive> archives, String language, Boolean death)
		{
			List<(DatArchive Archive, DatEntry Entry, String Language)> found = [];
			foreach (DatArchive archive in archives)
			{
				foreach (DatEntry entry in archive.Entries)
				{
					Match folder = CutsFolder.Match(entry.Directory.Trim('\\'));
					if (!entry.IsDamaged
						&& folder.Success
						&& entry.Name.StartsWith("nar_", StringComparison.OrdinalIgnoreCase)
						&& entry.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
						&& entry.Name.StartsWith("nar_dth", StringComparison.OrdinalIgnoreCase) == death)
					{
						found.Add((archive, entry, folder.Groups["language"].Value));
					}
				}
			}

			String wanted = LanguageFolder(language);
			foreach (String folder in new[] { wanted, "english" })
			{
				List<(DatArchive Archive, DatEntry Entry)> inLanguage = found
					.Where(file => file.Language.Equals(folder, StringComparison.OrdinalIgnoreCase))
					.Select(file => (file.Archive, file.Entry))
					.ToList();
				if (inLanguage.Count > 0)
					return inLanguage;
			}

			return found.Select(file => (file.Archive, file.Entry)).ToList();
		}

		/// <summary>
		/// A text of the endings, as the game would show it: a death screen shows a whole file (its lines one after the other), a
		/// slide shows one line of a file at a time.
		/// </summary>
		/// <returns>The text, or null when the archives have no text of the endings.</returns>
		public static String? Pick(IEnumerable<DatArchive> archives, String language, Boolean death, Random random)
		{
			IReadOnlyList<(DatArchive Archive, DatEntry Entry)> files = TextFiles(archives, language, death);
			if (files.Count == 0)
				return null;

			(DatArchive archive, DatEntry entry) = files[random.Next(files.Count)];
			IReadOnlyList<String> records = Records(TextDecoder.Decode(archive.ReadData(entry)).Text);
			if (records.Count == 0)
				return null;

			return death ? String.Join(' ', records) : records[random.Next(records.Count)];
		}
	}
}