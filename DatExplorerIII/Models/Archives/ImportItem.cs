namespace DatExplorerIII.Models.Archives
{
	using System;

	/// <summary>A file to import and the folder, relative to the destination, it belongs to.</summary>
	internal sealed record ImportItem(String SourceFile, String RelativeDirectory);
}