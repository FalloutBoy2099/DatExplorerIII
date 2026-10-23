namespace DatExplorerIII.ViewModels.Viewer
{
	using System;

	/// <summary>
	/// A part of the text that the view has to select and scroll to (a search found it, or the user went to a line).
	/// <paramref name="Serial"/> changes each time, so that asking for the same part twice is seen.
	/// </summary>
	public sealed record TextReveal(Int32 Start, Int32 Length, Int32 Serial);
}