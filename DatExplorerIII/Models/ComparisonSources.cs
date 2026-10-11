namespace DatExplorerIII.Models
{
	using System;

	/// <summary>The two archives (a DAT file or a folder each) the user chose to compare.</summary>
	public sealed record ComparisonSources(String Left, String Right);
}