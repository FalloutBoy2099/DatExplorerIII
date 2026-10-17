namespace DatExplorerIII.Services
{
	using System;
	using System.IO;

	/// <summary>
	/// Which files are DAT archives by their name. The archives of Arcanum can also have the extensions .patch0 to .patch9
	/// (the patches of the game and of its modules, such as Arcanum.PATCH9). The content, not the name, tells the format.
	/// </summary>
	public static class DatFiles
	{
		/// <summary>True for a file the program opens as an archive: a DAT file (see <see cref="IsDatFile"/>) or a ZIP. Only the DAT files are associated with the program.</summary>
		public static Boolean IsArchiveFile(String path)
		{
			return IsDatFile(path) || Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>True for a file whose extension is .dat, or .patch0 to .patch9 (upper or lower case).</summary>
		public static Boolean IsDatFile(String path)
		{
			String extension = Path.GetExtension(path);
			if (extension.Equals(".dat", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			return extension.Length == 7 && extension.StartsWith(".patch", StringComparison.OrdinalIgnoreCase) && Char.IsAsciiDigit(extension[6]);
		}
	}
}