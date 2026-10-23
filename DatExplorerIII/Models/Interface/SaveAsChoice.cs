namespace DatExplorerIII.Models.Interface
{
	using System;
	using Models.Archives;

	/// <summary>Where the user chose to save an archive under another name, and in which format.</summary>
	public sealed record SaveAsChoice(String Path, DatFormat Format);
}