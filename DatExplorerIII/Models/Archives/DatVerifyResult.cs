namespace DatExplorerIII.Models.Archives
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Outcome of the verification of every file of an archive.
	/// </summary>
	/// <param name="Checked">Number of files that were examined.</param>
	/// <param name="Damaged">The files whose data is unusable.</param>
	public sealed record DatVerifyResult(Int32 Checked, IReadOnlyList<DatEntry> Damaged);
}