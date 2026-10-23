namespace DatExplorerIII.Infrastructure
{
	using System;
	using Models.Interface;

	/// <summary>Where a window keeps its size and its position between two runs of the program, and what the user wants kept.</summary>
	public interface IWindowPlacementStore
	{
		/// <summary>Whether the size of the window is kept.</summary>
		Boolean SaveWindowSize { get; }

		/// <summary>Whether the position of the window is kept.</summary>
		Boolean SaveWindowPosition { get; }

		/// <summary>The placement kept last time, or null when there is none.</summary>
		WindowPlacement? LoadPlacement();

		void SavePlacement(WindowPlacement placement);
	}
}