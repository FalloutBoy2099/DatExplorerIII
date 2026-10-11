namespace DatExplorerIII.Services
{
	using System;
	using System.IO;
	using Formats;
	using Models;

	/// <summary>
	/// The palette of the game (the color.pal of Fallout 1 and 2), kept in the program: every sprite of the game is drawn
	/// with it (except the few that come with a palette of their own), so an archive that does not hold it can still be shown
	/// with the right colors.
	/// </summary>
	public static class DefaultPalette
	{
		private const String ResourceName = "DatExplorerIII.Fallout.pal";

		private static readonly Lazy<Palette> Instance = new(Load);

		public static Palette Palette => Instance.Value;

		private static Palette Load()
		{
			using Stream stream = typeof(DefaultPalette).Assembly.GetManifestResourceStream(ResourceName)
				?? throw new InvalidOperationException("The palette of the game is missing from the program.");
			using MemoryStream memory = new();
			stream.CopyTo(memory);
			return PaletteReader.Read(memory.ToArray());
		}
	}
}