namespace DatExplorerIII.Views.Viewer
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using ViewModels.Viewer;

	/// <summary>A picture: the wheel of the mouse magnifies it, the button of the wheel shows it at its real size.</summary>
	public partial class ImageView : UserControl
	{
		public ImageView()
		{
			InitializeComponent();
		}

		private void OnImageWheel(Object sender, MouseWheelEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is ImageViewerViewModel image && e.Delta != 0)
			{
				image.Wheel(e.Delta);
				e.Handled = true;
			}
		}

		private void OnImageMouseDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Middle && ((FrameworkElement)sender).DataContext is ImageViewerViewModel image)
			{
				image.ActualSize();
				e.Handled = true;
			}
		}
	}
}