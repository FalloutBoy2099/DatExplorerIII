namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// Writes a size in bytes the way people read it: 532 B, 1.5 KB, 12 MB, 1.25 GB (a unit is 1024 times the one before it,
	/// and the names of the units are those of the language of the interface: o, Ko, Mo, Go in French).
	/// </summary>
	public static class SizeFormatter
	{
		private static readonly String[] UnitKeys = ["Str.UnitByte", "Str.UnitKilo", "Str.UnitMega", "Str.UnitGiga", "Str.UnitTera"];

		/// <summary>A size in the units of the current language and the number format of the current culture.</summary>
		public static String Format(Int64 bytes)
		{
			String[] units = new String[UnitKeys.Length];
			for (Int32 i = 0; i < units.Length; i++)
			{
				units[i] = Loc.Get(UnitKeys[i]);
			}

			return Format(bytes, units, CultureInfo.CurrentCulture);
		}

		/// <param name="bytes">The size.</param>
		/// <param name="units">The names of the units: byte, kilo, mega, giga and tera.</param>
		/// <param name="culture">Decides the decimal separator.</param>
		public static String Format(Int64 bytes, IReadOnlyList<String> units, CultureInfo culture)
		{
			if (bytes < 1024)
			{
				return bytes.ToString("N0", culture) + " " + units[0];
			}

			Double value = bytes;
			Int32 unit = 0;

			// The next unit as soon as the number would have four digits: 1000 KB is written 0.98 MB.
			while (value >= 1000 && unit < units.Count - 1)
			{
				value /= 1024;
				unit++;
			}

			// Three digits that mean something: 1.25, 12.5, 125.
			String format = value < 10 ? "0.##" : value < 100 ? "0.#" : "0";
			return value.ToString(format, culture) + " " + units[unit];
		}
	}
}