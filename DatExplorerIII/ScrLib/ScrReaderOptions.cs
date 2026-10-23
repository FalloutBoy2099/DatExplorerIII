namespace DatExplorerIII.ScrLib
{
	using System;
	using System.Text;

	public sealed record ScrReaderOptions
	{
		/// <summary>Western Arcanum descriptions use Windows-1252. Does not alter global encoding providers.</summary>
		public Encoding DescriptionEncoding { get; init; } = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;

		/// <summary>Allocation limit based on actual lines. Editor capacity never controls allocation.</summary>
		public Int32 MaxLineCount { get; init; } = 100_000;

		/// <summary>Reject data after the final line. Disable to read a script embedded in a larger stream.</summary>
		public Boolean RequireEndOfStream { get; init; } = true;
	}
}