namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using Models;

	/// <summary>
	/// The encodings offered to read a text, named in the language of the interface.
	/// </summary>
	public static class TextEncodings
	{
		/// <summary>The list for a combo box, starting with the automatic choice.</summary>
		public static IReadOnlyList<TextEncodingOption> CreateOptions()
		{
			String western = Loc.Get("Str.EncodingWestern");
			String central = Loc.Get("Str.EncodingCentral");
			String cyrillic = Loc.Get("Str.EncodingCyrillic");
			String dos = Loc.Get("Str.EncodingDos");

			return
			[
				new TextEncodingOption(TextEncodingOption.Automatic, Loc.Get("Str.EncodingAuto")),
				new TextEncodingOption(65001, "UTF-8"),
				new TextEncodingOption(1200, "UTF-16"),
				new TextEncodingOption(1252, $"Windows-1252 ({western})"),
				new TextEncodingOption(1250, $"Windows-1250 ({central})"),
				new TextEncodingOption(1251, $"Windows-1251 ({cyrillic})"),
				new TextEncodingOption(437, $"CP437 ({dos})"),
				new TextEncodingOption(850, $"CP850 ({dos}, {western})"),
				new TextEncodingOption(866, $"CP866 ({dos}, {cyrillic})"),
			];
		}
	}
}