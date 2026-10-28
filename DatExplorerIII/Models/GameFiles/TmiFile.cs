namespace DatExplorerIII.Models.GameFiles
{
	using System;

	/// <summary>The information of a town map (.tmi): where it is, and how big its picture and its area (.tma) are.</summary>
	/// <param name="Version">The version of the format (2).</param>
	/// <param name="MapId">The number of the town map.</param>
	/// <param name="PictureWidth">The width of the picture of the map, in pixels.</param>
	/// <param name="PictureHeight">The height of the picture of the map, in pixels.</param>
	/// <param name="TilesWidth">The width of the area of the map, in tiles.</param>
	/// <param name="TilesHeight">The height of the area of the map, in tiles.</param>
	/// <param name="Flag">0, or -1 for the maps of a building.</param>
	/// <param name="OffsetX">First number that places the map.</param>
	/// <param name="OffsetY">Second number that places the map.</param>
	/// <param name="Scale">The scale of the picture (0.25 in all the files known).</param>
	/// <param name="Unknown">A number that is not understood.</param>
	/// <param name="Reserved">The second number of the file (0 in all the files known).</param>
	public sealed record TmiFile(
		Int32 Version,
		Int32 MapId,
		Int32 PictureWidth,
		Int32 PictureHeight,
		Int32 TilesWidth,
		Int32 TilesHeight,
		Int32 Flag,
		Int32 OffsetX,
		Int32 OffsetY,
		Single Scale,
		Int32 Unknown,
		Int32 Reserved);
}
