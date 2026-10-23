namespace DatExplorerIII.Views.Viewer
{
	using System.Windows.Controls;

	/// <summary>
	/// Shows a file as a text, a picture, a sound, a movie, a sheet or a table: the control of the kind of file that the viewer
	/// holds. It binds to a <see cref="ViewModels.Viewer.ViewerViewModel"/>.
	/// </summary>
	public partial class ViewerControl : UserControl
	{
		public ViewerControl()
		{
			InitializeComponent();
		}
	}
}