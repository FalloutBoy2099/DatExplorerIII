namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>One message of a message file: a number and its text.</summary>
	/// <param name="Number">The number the game asks for the message with.</param>
	/// <param name="Text">The text, as it is written between the braces (it can hold several lines).</param>
	public sealed record MesEntry(Int32 Number, String Text);

	/// <summary>
	/// A message file (.mes) of Arcanum: the texts of the game, found by number.
	/// </summary>
	/// <param name="Entries">The messages, in the order of the file.</param>
	/// <param name="SkippedCount">How many pairs of braces were not messages (a number that is not a number, a text without number).</param>
	/// <param name="EncodingName">The encoding the file was read with.</param>
	public sealed record MesFile(IReadOnlyList<MesEntry> Entries, Int32 SkippedCount, String EncodingName);
}