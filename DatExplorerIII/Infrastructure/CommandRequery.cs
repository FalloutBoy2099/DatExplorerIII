namespace DatExplorerIII.Infrastructure
{
	using System.Windows.Input;

	/// <summary>Asks every command to tell again whether it can run (what the buttons and the menus show follows).</summary>
	public static class CommandRequery
	{
		public static void Invalidate()
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}
}