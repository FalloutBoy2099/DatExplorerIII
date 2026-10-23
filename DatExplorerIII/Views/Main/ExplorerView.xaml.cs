namespace DatExplorerIII.Views.Main
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using Models.Interface;
	using ViewModels;
	using ViewModels.Main;

	/// <summary>
	/// The folders on the left, the files of the selected folder in the middle and the preview on the right. Only the width of the
	/// blocks lives here (the preview opens and closes, the widths are kept); the behavior is in the view model.
	/// </summary>
	public partial class ExplorerView : UserControl
	{
		private MainViewModel? _viewModel;

		/// <summary>The width of the preview pane compared with the one of the file list (both follow the window).</summary>
		private Double _previewShare = 0.6;

		public ExplorerView()
		{
			InitializeComponent();
			DataContextChanged += OnDataContextChanged;
		}

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (_viewModel != null)
			{
				_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
				_viewModel.RevealRequested -= OnRevealRequested;
			}

			_viewModel = e.NewValue as MainViewModel;
			if (_viewModel == null)
				return;

			_viewModel.PropertyChanged += OnViewModelPropertyChanged;
			_viewModel.RevealRequested += OnRevealRequested;
			RestoreBlockWidths();
			ApplyPreviewVisibility();
		}

		/// <summary>Keeps the widths of the blocks for the next run (the window is closing).</summary>
		public void SaveBlockWidths()
		{
			_viewModel?.SaveBlocks(TreeColumn.Width.IsAbsolute ? TreeColumn.Width.Value : TreeColumn.ActualWidth, CurrentPreviewShare());
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MainViewModel.IsPreviewVisible))
				ApplyPreviewVisibility();
		}

		/// <summary>
		/// Opens or closes the preview pane: its column shares the width of the window with the file list in the proportion
		/// the user left it. (A width in pixels would go beyond a window that is narrower than the columns.)
		/// </summary>
		private void ApplyPreviewVisibility()
		{
			Boolean visible = _viewModel!.IsPreviewVisible;
			if (!visible && PreviewColumn.Width.IsStar && ListColumn.Width.Value > 0)
				_previewShare = PreviewColumn.Width.Value / ListColumn.Width.Value;

			PreviewSplitterColumn.Width = visible ? GridLength.Auto : new GridLength(0);
			ListColumn.Width = new GridLength(1, GridUnitType.Star);
			PreviewColumn.Width = visible ? new GridLength(_previewShare, GridUnitType.Star) : new GridLength(0);
			PreviewColumn.MinWidth = visible ? 200 : 0;
		}

		/// <summary>The widths of the blocks come back as the user left them (when the settings keep them).</summary>
		private void RestoreBlockWidths()
		{
			if (_viewModel!.TreeWidth >= SettingsData.MinimumTreeWidth)
				TreeColumn.Width = new GridLength(_viewModel.TreeWidth);

			if (_viewModel.PreviewShare > 0)
				_previewShare = _viewModel.PreviewShare;
		}

		/// <summary>The share of the width of the preview compared with the list, as it is now (or as it was when the preview was hidden).</summary>
		private Double CurrentPreviewShare()
		{
			return _viewModel!.IsPreviewVisible && PreviewColumn.Width.IsStar && ListColumn.Width.Value > 0 ? PreviewColumn.Width.Value / ListColumn.Width.Value : _previewShare;
		}

		private void OnRevealRequested(ListEntry item)
		{
			FilesList.SelectedItems.Clear();
			FilesList.SelectedItem = item;
			FilesList.ScrollIntoView(item);
		}
	}
}