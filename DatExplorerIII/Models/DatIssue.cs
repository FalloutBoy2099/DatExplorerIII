namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A problem found while reading or verifying an archive.
	/// </summary>
	/// <param name="Kind">What is wrong.</param>
	/// <param name="Path">The file concerned, when there is one.</param>
	/// <param name="Value">A number that completes the description (see <see cref="DatIssueKind"/>).</param>
	public sealed record DatIssue(DatIssueKind Kind, String? Path = null, Int64 Value = 0);
}