namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Media;
	using Models;
	using Services;
	using ViewModels;

	/// <summary>
	/// The main window: the folder tree on the left, the files of the selected folder on the right.
	/// Only plumbing of the window itself lives here (its frame, its menus, the preview pane); the behavior is in the view model, and the
	/// keyboard shortcuts, the selections, the drag and drop of files, the sort of the list, the placement of the window and the scrolling
	/// together of two viewers are key bindings and attached behaviors (<c>Views/Behaviors</c>) in the XAML.
	/// </summary>
	public partial class MainWindow : Window
	{
		private const String RecentTag = "recent";

		private readonly MainViewModel _viewModel;

		/// <summary>The width of the preview pane compared with the one of the file list (both follow the window).</summary>
		private Double _previewShare = 0.6;

		public MainWindow(MainViewModel viewModel)
		{
			_viewModel = viewModel;
			DataContext = viewModel;
			InitializeComponent();
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
			RestoreBlockWidths();
			ApplyPreviewVisibility();
			ApplyWindowFrame();

			viewModel.RevealRequested += OnRevealRequested;
		}

		/// <summary>
		/// The recent files are added to the File menu each time it opens: a separator, a line for each file, and the line that
		/// clears the list; then comes the separator before "Close". The lines of the last time are removed first.
		/// </summary>
		private void OnFileMenuOpened(Object sender, RoutedEventArgs e)
		{
			// The event also comes from the submenus of the menu.
			if (!ReferenceEquals(e.OriginalSource, FileMenu))
			{
				return;
			}

			for (Int32 i = FileMenu.Items.Count - 1; i >= 0; i--)
			{
				if (FileMenu.Items[i] is FrameworkElement { Tag: RecentTag })
				{
					FileMenu.Items.RemoveAt(i);
				}
			}

			IReadOnlyList<String> files = _viewModel.RecentFiles;
			if (files.Count == 0)
			{
				return;
			}

			List<Control> lines = new() { new Separator { Tag = RecentTag } };
			for (Int32 i = 0; i < files.Count; i++)
			{
				// The digit is the access key of the line (1 to 9, and 0 for the tenth); an underscore of the path is not one.
				String number = i < 9 ? "_" + (i + 1) : "1_0";
				lines.Add(new MenuItem
				{
					Header = number + " " + RecentFiles.Shorten(files[i]).Replace("_", "__", StringComparison.Ordinal),
					ToolTip = files[i],
					Command = _viewModel.OpenRecentCommand,
					CommandParameter = files[i],
					Tag = RecentTag,
				});
			}

			lines.Add(new MenuItem
			{
				Header = (String)FindResource("Str.ClearRecent"),
				Command = _viewModel.ClearRecentCommand,
				Tag = RecentTag,
			});

			// The list ends before the separator that comes before "Close".
			Int32 index = FileMenu.Items.IndexOf(CloseSeparator);
			foreach (Control line in lines)
			{
				FileMenu.Items.Insert(index++, line);
			}
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MainViewModel.IsPreviewVisible))
			{
				ApplyPreviewVisibility();
			}
			else if (e.PropertyName == nameof(MainViewModel.IsWindowFrameVisible))
			{
				ApplyWindowFrame();
			}
		}

		/// <summary>
		/// Opens or closes the preview pane: its column shares the width of the window with the file list in the proportion
		/// the user left it. (A width in pixels would go beyond a window that is narrower than the columns.)
		/// </summary>
		private void ApplyPreviewVisibility()
		{
			Boolean visible = _viewModel.IsPreviewVisible;
			if (!visible && PreviewColumn.Width.IsStar && ListColumn.Width.Value > 0)
			{
				_previewShare = PreviewColumn.Width.Value / ListColumn.Width.Value;
			}

			PreviewSplitterColumn.Width = visible ? GridLength.Auto : new GridLength(0);
			ListColumn.Width = new GridLength(1, GridUnitType.Star);
			PreviewColumn.Width = visible ? new GridLength(_previewShare, GridUnitType.Star) : new GridLength(0);
			PreviewColumn.MinWidth = visible ? 200 : 0;
		}

		/// <summary>The widths of the blocks come back as the user left them (when the settings keep them).</summary>
		private void RestoreBlockWidths()
		{
			if (_viewModel.TreeWidth >= SettingsData.MinimumTreeWidth)
			{
				TreeColumn.Width = new GridLength(_viewModel.TreeWidth);
			}

			if (_viewModel.PreviewShare > 0)
			{
				_previewShare = _viewModel.PreviewShare;
			}
		}

		/// <summary>The share of the width of the preview compared with the list, as it is now (or as it was when the preview was hidden).</summary>
		private Double CurrentPreviewShare()
		{
			return _viewModel.IsPreviewVisible && PreviewColumn.Width.IsStar && ListColumn.Width.Value > 0 ? PreviewColumn.Width.Value / ListColumn.Width.Value : _previewShare;
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (!_viewModel.ConfirmExit())
			{
				e.Cancel = true;
				return;
			}

			_viewModel.SaveBlocks(TreeColumn.Width.IsAbsolute ? TreeColumn.Width.Value : TreeColumn.ActualWidth, CurrentPreviewShare());
		}

		/// <summary>The window is closed for good (after the placement was kept by its behavior): the program lets go of what it holds.</summary>
		private void OnClosed(Object? sender, EventArgs e)
		{
			_viewModel.Shutdown();
		}

		private void OnExit(Object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void OnStateChanged(Object? sender, EventArgs e)
		{
			// A maximized window with a custom frame extends beyond the screen by the size of its resize border.
			Boolean maximized = WindowState == WindowState.Maximized;
			Frame.Margin = maximized ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + SystemParameters.FixedFrameVerticalBorderWidth) : new Thickness(0);
			MaximizeButton.Content = maximized ? "" : "";
			ApplyWindowFrame();
		}

		/// <summary>Draws the frame of the window when the user wants it (a maximized window has its edges at the edges of the screen).</summary>
		private void ApplyWindowFrame()
		{
			WindowBorder.BorderThickness = new Thickness(_viewModel.IsWindowFrameVisible && WindowState != WindowState.Maximized ? 1 : 0);
		}

		private void OnMinimizeClick(Object sender, RoutedEventArgs e)
		{
			SystemCommands.MinimizeWindow(this);
		}

		private void OnMaximizeClick(Object sender, RoutedEventArgs e)
		{
			if (WindowState == WindowState.Maximized)
			{
				SystemCommands.RestoreWindow(this);
			}
			else
			{
				SystemCommands.MaximizeWindow(this);
			}
		}

		private void OnCloseClick(Object sender, RoutedEventArgs e)
		{
			SystemCommands.CloseWindow(this);
		}

		private void OnThemeButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(ThemeButton);
			foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
			{
				MenuItem item = CreateMenuItem(Loc.Get("Str.Theme" + preference), preference == ThemeManager.Preference);
				item.Click += (_, _) => ThemeManager.SetPreference(preference);
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private void OnLanguageButtonClick(Object sender, RoutedEventArgs e)
		{
			ContextMenu menu = CreateMenu(LanguageButton);
			foreach (LanguageInfo language in Loc.Languages)
			{
				MenuItem item = CreateMenuItem(language.NativeName, language.Code == Loc.Language);
				item.Command = _viewModel.ChangeLanguageCommand;
				item.CommandParameter = language.Code;
				menu.Items.Add(item);
			}

			menu.IsOpen = true;
		}

		private ContextMenu CreateMenu(Button target)
		{
			// The menu inherits the font of its target, which is the symbol font of the button: give it the font of the window.
			return new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom, FontFamily = FontFamily, FontSize = FontSize };
		}

		private MenuItem CreateMenuItem(String header, Boolean isCurrent)
		{
			// A check mark (a glyph of the symbol font) shows the choice in use.
			return new MenuItem
			{
				Header = header,
				Icon = isCurrent ? new TextBlock { Text = "", FontFamily = (FontFamily)FindResource("IconFont") } : null,
			};
		}

		private void OnRevealRequested(ListEntry item)
		{
			FilesList.SelectedItems.Clear();
			FilesList.SelectedItem = item;
			FilesList.ScrollIntoView(item);
		}
	}
}