namespace DatExplorerIII.Models.Graphics
{
	using System;
	using System.Collections.Generic;

	/// <summary>One picture of an animation: the index of the palette color of every pixel, row by row. Index 0 is transparent.</summary>
	public sealed record FrmFrame(Int32 Width, Int32 Height, Int32 OffsetX, Int32 OffsetY, Byte[] Pixels);

	/// <summary>The frames seen from one direction of the six that a Fallout sprite can have.</summary>
	public sealed record FrmDirection(Int32 ShiftX, Int32 ShiftY, IReadOnlyList<FrmFrame> Frames);

	/// <summary>
	/// A Fallout animation or picture (.frm): one to six directions of one or more frames.
	/// </summary>
	public sealed record FrmImage(Int32 Version, Int32 FramesPerSecond, Int32 ActionFrame, Int32 FramesPerDirection, IReadOnlyList<FrmDirection> Directions);
}