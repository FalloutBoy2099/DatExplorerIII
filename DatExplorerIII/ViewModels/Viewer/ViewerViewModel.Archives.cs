namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Models.Archives;

	/// <summary>The archives that are open: a file of the game is looked for in all of them.</summary>
	public sealed partial class ViewerViewModel
	{
		/// <summary>
		/// The archive of a file, then the other archives that are open: the game reads all its archives as one, so what one file needs
		/// (a prototype, a subtitle, a script) may be in another. A copy of the list, which does not change while it is used.
		/// </summary>
		private IReadOnlyList<DatArchive> GameArchives(DatArchive archive)
		{
			return new[] { archive }.Concat(OtherArchives?.Invoke() ?? Array.Empty<DatArchive>()).Distinct().ToList();
		}

		/// <summary>A file of the game: in the archive of the map first, then in the other open archives.</summary>
		private Byte[]? ReadGameFile(DatArchive archive, String path)
		{
			foreach (DatArchive candidate in GameArchives(archive))
			{
				try
				{
					DatEntry? entry = candidate.Find(path);
					if (entry == null || entry.IsDamaged)
						continue;

					return candidate.ReadData(entry);
				}
				catch (Exception exception) when (exception is DatException or IOException or InvalidOperationException)
				{
					// This copy cannot be read (or the archive is being changed at this moment): another archive may hold one.
				}
			}

			return null;
		}
	}
}