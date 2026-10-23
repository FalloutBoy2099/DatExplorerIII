namespace DatExplorerIII.Services.ArcanumScripts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Core;
	using Models.Archives;

	/// <summary>
	/// The names of the scripts of Arcanum by their number. A script is a file <c>scr\01013dante.scr</c> whose name starts with the
	/// number (<c>1013</c>) that the other scripts use to call it: the name is looked for in the archives that are open.
	/// </summary>
	public static class ScrScriptNames
	{
		/// <summary>
		/// What gives the name of the script of a number (null: there is none). The archives are read the first time a name is asked
		/// for, so that a script that calls no other one costs nothing; the first archive that has a script of a number gives its name.
		/// </summary>
		public static Func<Int32, String?> For(IEnumerable<DatArchive> archives)
		{
			Dictionary<Int32, String>? names = null;

			return number =>
			{
				if (names == null)
				{
					names = [];
					foreach (DatArchive archive in archives)
					{
						foreach (DatEntry entry in archive.Entries)
						{
							if (!entry.IsDamaged && entry.Name.EndsWith(".scr", StringComparison.OrdinalIgnoreCase) && NumberOf(entry.Name) is Int32 found)
								names.TryAdd(found, entry.Name);
						}
					}
				}

				return names.TryGetValue(number, out String? name) ? name : null;
			};
		}

		/// <summary>The number a file name starts with (<c>01013dante.scr</c>: 1013); null when it does not start with one.</summary>
		public static Int32? NumberOf(String fileName)
		{
			Int32 digits = 0;
			while (digits < fileName.Length && fileName[digits] is >= '0' and <= '9')
			{
				digits++;
			}

			return digits > 0 && digits <= 9 && Int32.TryParse(fileName.AsSpan(0, digits), NumberStyles.None, CultureInfo.InvariantCulture, out Int32 number) ? number : null;
		}
	}
}