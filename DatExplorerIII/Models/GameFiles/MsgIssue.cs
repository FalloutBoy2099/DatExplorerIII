namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>How serious a problem of a message file (.msg) is: a critical one breaks the file for the game.</summary>
	public enum MsgIssueSeverity
	{
		Critical,
		High,
		Medium,
	}

	/// <summary>What is wrong in a message file. The severity of each kind is given by <see cref="MsgIssue.SeverityOf"/>.</summary>
	public enum MsgIssueKind
	{
		/// <summary>The number of a message is not a whole number of digits (a letter, a sign, nothing at all, too big).</summary>
		InvalidId,

		/// <summary>A closing brace is missing: the field is still open at the end of the file.</summary>
		UnclosedBrace,

		/// <summary>An opening brace in a field: the closing brace of the field before it is missing.</summary>
		UnexpectedOpenBrace,

		/// <summary>A closing brace that closes nothing.</summary>
		StrayCloseBrace,

		/// <summary>A message does not have its three pairs of braces (number, sound, text). <see cref="MsgIssue.Value"/> is how many it has.</summary>
		WrongFieldCount,

		/// <summary>The last line of a text holds only spaces or line breaks.</summary>
		BlankLastLine,

		/// <summary>The number of a message is already used (<see cref="MsgIssue.Value"/>, first used on the line <see cref="MsgIssue.Other"/>).</summary>
		DuplicateId,

		/// <summary>A text is longer than the game allows (<see cref="MsgIssue.Value"/> characters).</summary>
		TextTooLong,

		/// <summary>A word that cannot be broken is longer than a line of the game (<see cref="MsgIssue.Value"/> characters).</summary>
		WordTooLong,

		/// <summary>The file does not end with an empty line.</summary>
		NoFinalEmptyLine,
	}

	/// <summary>A problem found in a message file.</summary>
	/// <param name="Severity">How serious it is.</param>
	/// <param name="Kind">What is wrong.</param>
	/// <param name="Line">Line of the problem, from 1.</param>
	/// <param name="Column">Column of the problem, from 1.</param>
	/// <param name="Value">A number that goes with the kind of problem (a length, a number of pairs of braces, a message number).</param>
	/// <param name="Other">A second number (the line of the first use of a number).</param>
	/// <param name="Excerpt">The line of the problem, shortened.</param>
	public sealed record MsgIssue(MsgIssueSeverity Severity, MsgIssueKind Kind, Int32 Line, Int32 Column, Int64 Value, Int64 Other, String Excerpt)
	{
		public static MsgIssueSeverity SeverityOf(MsgIssueKind kind)
		{
			return kind switch
			{
				MsgIssueKind.InvalidId or MsgIssueKind.UnclosedBrace or MsgIssueKind.UnexpectedOpenBrace or MsgIssueKind.StrayCloseBrace
					or MsgIssueKind.WrongFieldCount or MsgIssueKind.BlankLastLine => MsgIssueSeverity.Critical,
				MsgIssueKind.DuplicateId or MsgIssueKind.TextTooLong => MsgIssueSeverity.High,
				_ => MsgIssueSeverity.Medium,
			};
		}
	}

	/// <summary>What was found in one message file.</summary>
	/// <param name="Path">Where the file is (a path of an archive, of the disk).</param>
	/// <param name="Issues">The problems, in the order of the file.</param>
	/// <param name="ReadError">What went wrong when the file could not be read (then there is nothing to say of its content).</param>
	public sealed record MsgFileReport(String Path, IReadOnlyList<MsgIssue> Issues, String? ReadError = null)
	{
		public Boolean HasIssues => Issues.Count > 0;

		/// <summary>The report without the problems of the kinds that are turned off.</summary>
		public MsgFileReport Without(IReadOnlyCollection<MsgIssueKind> disabled)
		{
			return disabled.Count == 0 || Issues.All(issue => !disabled.Contains(issue.Kind)) ? this : this with { Issues = Issues.Where(issue => !disabled.Contains(issue.Kind)).ToList() };
		}

		public Int32 Count(MsgIssueSeverity severity) => Issues.Count(issue => issue.Severity == severity);

		/// <summary>The most serious problem of the file; null when it has none.</summary>
		public MsgIssueSeverity? Worst => Issues.Count == 0 ? null : Issues.Min(issue => issue.Severity);
	}
}