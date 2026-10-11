namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>One character of a font: its size, and one byte per pixel (0 is empty; 1 to 9 for an AAF font, 1 for a FON font).</summary>
	public sealed record FontGlyph(Int32 Width, Int32 Height, Byte[] Pixels);

	/// <summary>The kind of a font file.</summary>
	public enum FontFormat
	{
		/// <summary>An AAF font: nine levels of brightness (smooth edges).</summary>
		Aaf,

		/// <summary>A FON font: one bit per pixel.</summary>
		Fon,
	}

	/// <summary>
	/// A font of Fallout (a .aaf or a .fon file): the pictures of its characters, indexed by character code.
	/// </summary>
	/// <param name="Format">Which kind of file it was.</param>
	/// <param name="Height">The height of the font: the tallest character, with its ascenders and descenders.</param>
	/// <param name="Spacing">The space between two characters.</param>
	/// <param name="SpaceWidth">The width of the space character (AAF only; 0 for a FON).</param>
	/// <param name="LineSpacing">The space between two lines (AAF only; 0 for a FON).</param>
	/// <param name="Glyphs">The characters, starting with the code 0.</param>
	public sealed record FontFile(FontFormat Format, Int32 Height, Int32 Spacing, Int32 SpaceWidth, Int32 LineSpacing, IReadOnlyList<FontGlyph> Glyphs);
}