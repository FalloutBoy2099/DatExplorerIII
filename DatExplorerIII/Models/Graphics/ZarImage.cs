namespace DatExplorerIII.Models.Graphics
{
	using System;

	/// <summary>
	/// A picture of Fallout Tactics (.zar, or a frame of a .til or .spr file): the colors are already chosen, with a transparency
	/// for each pixel.
	/// </summary>
	/// <param name="Width">Width in pixels (0 for the empty picture some sprites hold).</param>
	/// <param name="Height">Height in pixels.</param>
	/// <param name="Bgra">The pixels (4 bytes each: blue, green, red, alpha, not premultiplied), line after line.</param>
	public sealed record ZarImage(Int32 Width, Int32 Height, Byte[] Bgra);
}
