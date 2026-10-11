namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// Progress notification of a long running archive operation.
	/// </summary>
	/// <param name="Name">Item being processed.</param>
	/// <param name="Current">Number of processed items.</param>
	/// <param name="Total">Number of items to process.</param>
	public readonly record struct DatProgress(String Name, Int32 Current, Int32 Total);
}