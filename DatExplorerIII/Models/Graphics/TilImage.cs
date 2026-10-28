namespace DatExplorerIII.Models.Graphics
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A tile of Fallout Tactics (.til): the size of the tile and its pictures, each with a palette of its own.
	/// </summary>
	public sealed record TilImage(Int32 Width, Int32 Height, IReadOnlyList<ZarImage> Frames);
}
