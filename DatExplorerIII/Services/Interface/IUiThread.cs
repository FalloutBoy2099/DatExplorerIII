namespace DatExplorerIII.Services.Interface
{
	using System;

	/// <summary>
	/// What the view models need from the thread of the user interface, so that they do not depend on a dispatcher.
	/// </summary>
	public interface IUiThread
	{
		/// <summary>Runs <paramref name="action"/> on the thread of the user interface and waits for it (at once when already there).</summary>
		void Invoke(Action action);

		/// <summary>Makes a timer whose ticks run on the thread of the user interface.</summary>
		IUiTimer CreateTimer();

		/// <summary>Puts a text on the clipboard (which belongs to the thread of the user interface). Does nothing when another program holds it.</summary>
		void SetClipboardText(String text);
	}
}