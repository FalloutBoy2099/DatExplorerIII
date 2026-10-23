namespace DatExplorerIII.ScrLib
{
	using System;
	using System.IO;

	/// <summary>A malformed or truncated SCR; Offset is relative to the script's start.</summary>
	public sealed class ScrFormatException : IOException
	{
		public ScrFormatException(String message, Int64 offset) : base($"{message} (offset 0x{offset:X}).")
		{
			Offset = offset;
		}

		public Int64 Offset { get; }
	}
}