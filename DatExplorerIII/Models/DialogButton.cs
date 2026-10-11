namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A button of a message dialog. <see cref="Id"/> is what the dialog returns when it is pressed.
	/// </summary>
	public sealed record DialogButton(String Id, String Label, Boolean IsDefault = false, Boolean IsCancel = false);
}