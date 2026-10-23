namespace DatExplorerIII.Models.Graphics
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A picture or an animation of Arcanum (.art): the same indexed pictures as a Fallout sprite, with the palettes that are
	/// in the file (a sprite can come in up to four colorings).
	/// </summary>
	/// <param name="Image">
	/// The pictures: one direction for a static picture, up to eight rotations for a character. Index 0 of the palette is
	/// transparent.
	/// </param>
	/// <param name="Palettes">The palettes of the file, from one to four.</param>
	/// <param name="IsStatic">True for art that has no rotations (tiles, items, interface).</param>
	public sealed record ArtImage(FrmImage Image, IReadOnlyList<Palette> Palettes, Boolean IsStatic);
}