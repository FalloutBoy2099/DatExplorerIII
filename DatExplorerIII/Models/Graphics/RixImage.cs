namespace DatExplorerIII.Models.Graphics
{
	using System;

	/// <summary>
	/// A Fallout full screen picture (.rix) with its own palette.
	/// </summary>
	public sealed record RixImage(Int32 Width, Int32 Height, Palette Palette, Byte[] Pixels);
}