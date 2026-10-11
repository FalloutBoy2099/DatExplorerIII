namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A text read from bytes, with the encoding that was used.
	/// </summary>
	/// <param name="Text">The characters.</param>
	/// <param name="EncodingName">Name of the encoding ("UTF-8", "Windows-1252"...).</param>
	/// <param name="IsDetected">True when the encoding was guessed from the bytes, false when it was chosen.</param>
	/// <param name="LooksBinary">True when the bytes contain so many control characters that this is probably not a text.</param>
	public sealed record DecodedText(String Text, String EncodingName, Boolean IsDetected, Boolean LooksBinary);
}