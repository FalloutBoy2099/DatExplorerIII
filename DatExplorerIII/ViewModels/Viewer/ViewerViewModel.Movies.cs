namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats.GameFiles;
	using Models.Archives;
	using Models.GameFiles;
	using Services.Interface;

	/// <summary>The movies: their subtitles.</summary>
	public sealed partial class ViewerViewModel
	{
		/// <summary>
		/// The subtitles of a movie: the .sve file that has its name, in the archive of the movie first, then in the other open
		/// ones. When there are several (one for each language of the game), the one of the language of the interface is taken,
		/// else the English one, else the first.
		/// </summary>
		private (SveFile File, String Path)? FindSubtitles(DatArchive archive, DatEntry movie)
		{
			String wanted = Path.GetFileNameWithoutExtension(movie.Name) + ".sve";
			String language = Loc.Language switch
			{
				"fr" => "french",
				"de" => "german",
				"es" => "spanish",
				"it" => "italian",
				_ => "english",
			};

			List<(DatArchive Archive, DatEntry Entry, Int32 Rank)> found = [];
			try
			{
				foreach (DatArchive candidate in GameArchives(archive))
				{
					foreach (DatEntry entry in candidate.Entries)
					{
						if (!entry.IsDamaged && entry.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
						{
							String folder = Path.DirectorySeparatorChar + entry.DirectoryKey.Trim(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
							String own = Path.DirectorySeparatorChar + language + Path.DirectorySeparatorChar;
							String english = Path.DirectorySeparatorChar + "english" + Path.DirectorySeparatorChar;
							found.Add((candidate, entry, folder.Contains(own, StringComparison.Ordinal) ? 0 : folder.Contains(english, StringComparison.Ordinal) ? 1 : 2));
						}
					}
				}

				if (found.Count == 0)
					return null;

				(DatArchive source, DatEntry chosen, _) = found.OrderBy(item => item.Rank).First();
				return (SveReader.Read(source.ReadData(chosen)), chosen.FullPath);
			}
			catch (Exception exception) when (exception is DatException or IOException or InvalidOperationException or InvalidDataException)
			{
				// Subtitles are a bonus: a movie is shown without them when they cannot be read.
				return null;
			}
		}
	}
}