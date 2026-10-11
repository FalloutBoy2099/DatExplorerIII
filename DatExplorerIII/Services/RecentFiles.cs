namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// The list of the archives opened last, the most recent first.
	/// </summary>
	public static class RecentFiles
	{
		/// <summary>How many files the list keeps.</summary>
		public const Int32 MaxCount = 10;

		/// <summary>
		/// Puts a file at the top of the list: it is removed from where it was (the same file written with other upper and
		/// lower case letters is the same file), and the oldest files that do not fit any more are dropped.
		/// </summary>
		public static void Add(IList<String> list, String path, Int32 maximum = MaxCount)
		{
			Remove(list, path);
			list.Insert(0, path);

			while (list.Count > maximum)
			{
				list.RemoveAt(list.Count - 1);
			}
		}

		public static void Remove(IList<String> list, String path)
		{
			for (Int32 i = list.Count - 1; i >= 0; i--)
			{
				if (String.Equals(list[i], path, StringComparison.OrdinalIgnoreCase))
				{
					list.RemoveAt(i);
				}
			}
		}

		/// <summary>
		/// A path that fits in a menu: a long one keeps its drive, the folder of the file and its name, with an ellipsis
		/// for the folders between.
		/// </summary>
		public static String Shorten(String path, Int32 maxLength = 60)
		{
			if (path.Length <= maxLength)
			{
				return path;
			}

			String root = Path.GetPathRoot(path) ?? String.Empty;
			String name = Path.GetFileName(path);
			String? folder = Path.GetFileName(Path.GetDirectoryName(path));

			String shortened = folder is { Length: > 0 } ? root + "…\\" + folder + "\\" + name : root + "…\\" + name;
			if (shortened.Length > maxLength)
			{
				shortened = root + "…\\" + name;
			}

			if (shortened.Length > maxLength)
			{
				// Even the name is too long: its end (the extension) is the useful part.
				shortened = "…" + name[^Math.Min(name.Length, maxLength - 1)..];
			}

			return shortened;
		}
	}
}