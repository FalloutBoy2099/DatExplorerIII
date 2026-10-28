namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;
	using Graphics;

	/// <summary>
	/// What is read of a mission of Fallout Tactics (.mis): its version, the teams, the text file of the mission and the picture
	/// of the map. The rest of the mission (the objects, the scripts) is not read.
	/// </summary>
	/// <param name="Version">The version of the file ("68", "69").</param>
	/// <param name="Teams">The names of the teams.</param>
	/// <param name="TextFile">The path of the text of the mission (empty when the file names none).</param>
	/// <param name="Preview">The picture of the map that is saved in the mission (null when it holds none).</param>
	/// <param name="UncompressedSize">The size of the data once inflated.</param>
	/// <param name="TilePrototypes">How many kinds of tile the map uses (-1 when the map cannot be read).</param>
	/// <param name="TileInstances">How many tiles are placed on the map (-1 when the map cannot be read).</param>
	public sealed record MisFile(String Version, IReadOnlyList<String> Teams, String TextFile, ZarImage? Preview, Int32 UncompressedSize, Int32 TilePrototypes, Int32 TileInstances);
}
