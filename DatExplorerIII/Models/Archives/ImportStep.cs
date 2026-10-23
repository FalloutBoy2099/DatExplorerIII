namespace DatExplorerIII.Models.Archives
{
	using System;

	/// <summary>A file to import with its place in the archive.</summary>
	internal sealed record ImportStep(String SourceFile, String Directory, String Name);
}