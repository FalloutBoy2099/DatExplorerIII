namespace DatExplorerIII.Views.Viewer
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using ViewModels.Viewer;

	/// <summary>A sound: the button, the channels, the volume and the position.</summary>
	public partial class AudioView : UserControl
	{
		public AudioView()
		{
			InitializeComponent();
		}

		private void OnSeekStarted(Object sender, DragStartedEventArgs e)
		{
			(((FrameworkElement)sender).DataContext as ISeekable)?.BeginSeek();
		}

		private void OnSeekCompleted(Object sender, DragCompletedEventArgs e)
		{
			(((FrameworkElement)sender).DataContext as ISeekable)?.EndSeek();
		}
	}
}