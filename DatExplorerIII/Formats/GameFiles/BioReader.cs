namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using Models.GameFiles;
	using Services.Texts;

	/// <summary>
	/// Reads the biography of a premade character (.bio): a plain text of the language of the game, with lines ended by
	/// carriage return and line feed, shown in the character selection.
	/// </summary>
	public static class BioReader
	{
		/// <summary>Decodes a biography, detecting its encoding unless a code page is given.</summary>
		public static DecodedText Read(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			return TextDecoder.Decode(data, codePage);
		}
	}
}