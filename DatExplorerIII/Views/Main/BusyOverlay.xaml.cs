namespace DatExplorerIII.Views.Main
{
	using System.Windows.Controls;

	/// <summary>Shown while a long operation runs: blocks the window and reports the progress.</summary>
	public partial class BusyOverlay : UserControl
	{
		public BusyOverlay()
		{
			InitializeComponent();
		}
	}
}