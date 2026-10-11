namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Outcome of an extraction.
	/// </summary>
	/// <param name="Succeeded">Number of files written to disk.</param>
	/// <param name="Failures">Archive paths of the files that could not be extracted.</param>
	public sealed record DatExtractResult(Int32 Succeeded, IReadOnlyList<String> Failures);
}