namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;

	/// <summary>A light of a sector of Arcanum.</summary>
	/// <param name="X">The tile of the light (in the whole map).</param>
	/// <param name="Y">The tile of the light (in the whole map).</param>
	/// <param name="OffsetX">The shift from the center of the tile, in pixels.</param>
	/// <param name="OffsetY">The shift from the center of the tile, in pixels.</param>
	/// <param name="Flags">The flags of the light (not understood).</param>
	/// <param name="Art">The number of the picture of the light.</param>
	/// <param name="Color0">The first color of the light (the day), 0x00RRGGBB.</param>
	/// <param name="Color1">The second color of the light (the night), 0x00RRGGBB.</param>
	public sealed record SecLight(Int32 X, Int32 Y, Int32 OffsetX, Int32 OffsetY, Int32 Flags, Int32 Art, Int32 Color0, Int32 Color1);

	/// <summary>
	/// A sector of a map of Arcanum (.sec): a square of 64 by 64 tiles with its lights, its roof, its scripts and its objects. The objects
	/// are counted, not read.
	/// </summary>
	/// <param name="Version">The version of the file (0 to 4: the later ones hold more fields).</param>
	/// <param name="Lights">The lights of the sector.</param>
	/// <param name="Tiles">The 4096 tiles (the number of the picture of each), row after row.</param>
	/// <param name="Roof">The 256 roofs (16 by 16, each over 4 by 4 tiles), or null when the file holds none.</param>
	/// <param name="TileScripts">The scripts of the tiles, each as its 6 numbers.</param>
	/// <param name="ScriptId">The script of the sector (0 for none).</param>
	/// <param name="TownMapInfo">The number of the town map of the sector (version 3 and later).</param>
	/// <param name="AptitudeAdjustment">The adjustment of the aptitude of the sector (version 3 and later).</param>
	/// <param name="LightScheme">The scheme of light of the sector (version 3 and later).</param>
	/// <param name="ObjectCount">How many objects the sector holds (as the end of the file announces).</param>
	/// <param name="ObjectBytes">The size of the objects in the file.</param>
	public sealed record SecFile(
		Int32 Version,
		IReadOnlyList<SecLight> Lights,
		UInt32[] Tiles,
		UInt32[]? Roof,
		IReadOnlyList<Int32[]> TileScripts,
		Int32 ScriptId,
		Int32 TownMapInfo,
		Int32 AptitudeAdjustment,
		Int32 LightScheme,
		Int32 ObjectCount,
		Int32 ObjectBytes);
}
