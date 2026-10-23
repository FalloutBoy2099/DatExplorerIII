namespace DatExplorerIII.ViewModels.Viewer
{
	/// <summary>A viewer with a position bar that the user takes and moves: what plays waits while the bar is held.</summary>
	public interface ISeekable
	{
		/// <summary>The user takes the bar.</summary>
		void BeginSeek();

		/// <summary>The user lets go of the bar: what was playing goes on from there.</summary>
		void EndSeek();
	}
}