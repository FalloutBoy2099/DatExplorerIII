namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// An encoding the user can choose to read a text with.
	/// </summary>
	/// <param name="CodePage">The code page; <see cref="Automatic"/> to detect it.</param>
	/// <param name="Display">The text shown in the list.</param>
	public sealed record TextEncodingOption(Int32 CodePage, String Display)
	{
		/// <summary>The code page of the option that detects the encoding.</summary>
		public const Int32 Automatic = -1;

		public Boolean IsAutomatic => CodePage == Automatic;

		public override String ToString() => Display;
	}
}