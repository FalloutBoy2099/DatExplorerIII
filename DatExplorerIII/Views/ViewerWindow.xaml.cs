namespace DatExplorerIII.Views
{
	using System;
	using System.Windows;
	using ViewModels;

	/// <summary>
	/// A window that shows one file. Several of them can be open at the same time.
	/// </summary>
	public partial class ViewerWindow : Window
	{
		public ViewerWindow(ViewerViewModel viewModel)
		{
			DataContext = viewModel;
			InitializeComponent();
			TitleBarTheme.Attach(this);
		}

		protected override void OnClosed(EventArgs e)
		{
			// Nothing must go on playing after the window is gone.
			(DataContext as ViewerViewModel)?.Dispose();
			base.OnClosed(e);
		}
	}
}