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

		/// <summary>The width of the block of the folders when it is shown next to another block.</summary>
		private Double _treeWidth = 280;

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
			ApplyLayout();
		}

		/// <summary>Keeps the widths of the blocks for the next run (the window is closing).</summary>
		public void SaveBlockWidths()
		{
			_viewModel?.SaveBlocks(TreeColumn.Width.IsAbsolute && TreeColumn.Width.Value >= SettingsData.MinimumTreeWidth ? TreeColumn.Width.Value : _treeWidth, CurrentPreviewShare());
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(MainViewModel.IsPreviewVisible) or nameof(MainViewModel.IsTreeVisible) or nameof(MainViewModel.IsFilesVisible))
				ApplyLayout();
		}

		/// <summary>
		/// Shows or hides the three blocks (the folders, the files, the preview). The files and the preview share the width the folders
		/// leave, in the proportion the user left them (a width in pixels would go beyond a window that is narrower than the columns); when
		/// the files are hidden the preview takes their place, and the folders keep their width, or take all of it when they are alone.
		/// </summary>
		private void ApplyLayout()
		{
			Boolean tree = _viewModel!.IsTreeVisible;
			Boolean files = _viewModel.IsFilesVisible;
			Boolean preview = _viewModel.IsPreviewVisible;

			// What the user set with a splitter is kept before the columns change.
			if (TreeColumn.Width.IsAbsolute && TreeColumn.Width.Value >= SettingsData.MinimumTreeWidth)
				_treeWidth = TreeColumn.Width.Value;

			if (PreviewColumn.Width.IsStar && ListColumn.Width.IsStar && ListColumn.Width.Value > 0 && PreviewColumn.Width.Value > 0)
				_previewShare = PreviewColumn.Width.Value / ListColumn.Width.Value;

			FolderTree.Visibility = tree ? Visibility.Visible : Visibility.Collapsed;
			TreeColumn.MinWidth = tree ? 160 : 0;
			TreeColumn.Width = !tree ? new GridLength(0) : !files && !preview ? new GridLength(1, GridUnitType.Star) : new GridLength(_treeWidth);

			Boolean treeSplit = tree && files;
			TreeSplitter.Visibility = treeSplit ? Visibility.Visible : Visibility.Collapsed;
			TreeSplitterColumn.Width = treeSplit ? GridLength.Auto : new GridLength(0);

			FilesList.Visibility = files ? Visibility.Visible : Visibility.Collapsed;
			ListColumn.MinWidth = files ? 240 : 0;
			ListColumn.Width = files ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

			Boolean previewSplit = files && preview;
			PreviewSplitter.Visibility = previewSplit ? Visibility.Visible : Visibility.Collapsed;
			PreviewSplitterColumn.Width = previewSplit ? GridLength.Auto : new GridLength(0);
			PreviewColumn.Width = !preview ? new GridLength(0) : files ? new GridLength(_previewShare, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
			PreviewColumn.MinWidth = preview ? 200 : 0;
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
			return _viewModel!.IsPreviewVisible && _viewModel.IsFilesVisible && PreviewColumn.Width.IsStar && ListColumn.Width.Value > 0 ? PreviewColumn.Width.Value / ListColumn.Width.Value : _previewShare;
		}

		private void OnRevealRequested(ListEntry item)
		{
			FilesList.SelectedItems.Clear();
			FilesList.SelectedItem = item;
			FilesList.ScrollIntoView(item);
		}
	}
}