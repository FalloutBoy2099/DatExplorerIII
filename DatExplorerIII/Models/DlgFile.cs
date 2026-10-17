namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// One line of a dialog: what a character says, or an answer the player can choose.
	/// </summary>
	/// <param name="Number">The number of the line: the lines are found by number.</param>
	/// <param name="Text">What is said to a male character, or the text of the answer.</param>
	/// <param name="Text2">For what is said: the version for a female character (empty: the same text). For an answer: the gender it is for (0 female, 1 male, empty both).</param>
	/// <param name="Iq">Empty for what a character says. For an answer: the intelligence needed (a negative number: the most it can be).</param>
	/// <param name="Test">For an answer: the conditions that must all be true to offer it (and, first, the number of its voice).</param>
	/// <param name="Goto">For what is said: the line whose answers follow. For an answer: where the dialog goes (0 ends it).</param>
	/// <param name="Effect">What happens when the line is reached or the answer chosen.</param>
	public sealed record DlgEntry(Int32 Number, String Text, String Text2, String Iq, String Test, String Goto, String Effect)
	{
		/// <summary>The comment (after two slashes) at the end of the line; empty when there is none.</summary>
		public String Comment { get; init; } = String.Empty;

		/// <summary>True for an answer of the player, false for what a character says.</summary>
		public Boolean IsPlayerOption => Iq.Trim().Length > 0;
	}

	/// <summary>
	/// A dialog file (.dlg) of Arcanum.
	/// </summary>
	/// <param name="Entries">The lines, in the order of the file.</param>
	/// <param name="SkippedCount">How many groups of braces did not make a line (a missing field, a number that is not a number).</param>
	/// <param name="EncodingName">The encoding the file was read with.</param>
	public sealed record DlgFile(IReadOnlyList<DlgEntry> Entries, Int32 SkippedCount, String EncodingName)
	{
		/// <summary>The comments on lines of their own, in the order of the file.</summary>
		public IReadOnlyList<FileComment> Comments { get; init; } = [];
	}
}