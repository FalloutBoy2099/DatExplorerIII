namespace DatExplorerIII.Views.Main
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Input;
	using ViewModels.Main;

	/// <summary>
	/// The main window: the folder tree on the left, the files of the selected folder on the right. The window only holds its own
	/// frame; its parts are the controls of this folder (title bar, menu, tool bar, explorer, comparison, status bar), the behavior
	/// is in the view model, and the keyboard shortcuts, the selections, the drag and drop of files, the sort of the list, the
	/// placement of the window and the scrolling together of two viewers are key bindings and attached behaviors
	/// (<c>Views/Behaviors</c>) in the XAML.
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly MainViewModel _viewModel;

		public MainWindow(MainViewModel viewModel)
		{
			_viewModel = viewModel;
			DataContext = viewModel;
			InitializeComponent();
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
			ApplyWindowFrame();
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MainViewModel.IsWindowFrameVisible))
				ApplyWindowFrame();
		}

		/// <summary>The back and forward buttons of the mouse go to the previous and to the next file of the folder.</summary>
		private void OnPreviewMouseDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton is not (MouseButton.XButton1 or MouseButton.XButton2))
				return;

			_viewModel.StepFile(e.ChangedButton == MouseButton.XButton2);
			e.Handled = true;
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (!_viewModel.ConfirmExit())
			{
				e.Cancel = true;
				return;
			}

			Explorer.SaveBlockWidths();
		}

		/// <summary>The window is closed for good (after the placement was kept by its behavior): the program lets go of what it holds.</summary>
		private void OnClosed(Object? sender, EventArgs e)
		{
			_viewModel.Shutdown();
		}

		private void OnStateChanged(Object? sender, EventArgs e)
		{
			// A maximized window with a custom frame extends beyond the screen by the size of its resize border.
			Boolean maximized = WindowState == WindowState.Maximized;
			Frame.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameVerticalBorderWidth) : new Thickness(0);
			ApplyWindowFrame();
		}

		/// <summary>Draws the frame of the window when the user wants it (a maximized window has its edges at the edges of the screen).</summary>
		private void ApplyWindowFrame()
		{
			WindowBorder.BorderThickness = new Thickness(_viewModel.IsWindowFrameVisible && WindowState != WindowState.Maximized ? 1 : 0);
		}
	}
}