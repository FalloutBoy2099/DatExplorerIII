namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// Where the main window was when the program ended, to put it back there.
	/// </summary>
	public readonly record struct WindowPlacement(Double Left, Double Top, Double Width, Double Height, Boolean IsMaximized)
	{
		/// <summary>
		/// What to keep of the place of the window: the size (and the maximized state) and the position are each kept only when
		/// the user wants them to be; the other part stays as it was saved before.
		/// </summary>
		public static WindowPlacement? ForSaving(WindowPlacement? saved, WindowPlacement current, Boolean keepSize, Boolean keepPosition)
		{
			if (saved is not { } before)
			{
				return keepSize || keepPosition ? current : null;
			}

			return new WindowPlacement(
				keepPosition ? current.Left : before.Left,
				keepPosition ? current.Top : before.Top,
				keepSize ? current.Width : before.Width,
				keepSize ? current.Height : before.Height,
				keepSize ? current.IsMaximized : before.IsMaximized);
		}

		/// <summary>
		/// The same placement, moved and shrunk if needed so that it is visible on the screen area
		/// (a monitor may have been unplugged since).
		/// </summary>
		public WindowPlacement FitInto(Double screenLeft, Double screenTop, Double screenWidth, Double screenHeight)
		{
			Double width = Math.Min(Width, screenWidth);
			Double height = Math.Min(Height, screenHeight);
			Double left = Math.Clamp(Left, screenLeft, screenLeft + screenWidth - width);
			Double top = Math.Clamp(Top, screenTop, screenTop + screenHeight - height);
			return new WindowPlacement(left, top, width, height, IsMaximized);
		}
	}
}