namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;
	using Models;
	using Services;
	using ViewModels;

	/// <summary>
	/// The main window: the folder tree on the left, the files of the selected folder on the right.
	/// Only user interface plumbing lives here (selection, window frame, placement); the behavior is in the view model, and the keyboard
	/// shortcuts and the drag and drop of files are key bindings and attached behaviors (<c>Views/Behaviors</c>) in the XAML.
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
			RestorePlacement();
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

		/// <summary>The files that are selected in the comparison are what the copies and the moves work on.</summary>
		private void OnComparisonSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			_viewModel.Comparison?.SetSelection(ComparisonGrid.SelectedItems.OfType<ComparisonRow>().ToList());
		}

		/// <summary>"Next difference" moves the selection: the list scrolls to it.</summary>
		private void OnDiffSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			if (DiffGrid.SelectedItem != null)
			{
				DiffGrid.ScrollIntoView(DiffGrid.SelectedItem);
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

		/// <summary>Puts the window where it was when the program ended last time.</summary>
		private void RestorePlacement()
		{
			Boolean useSize = _viewModel.SaveWindowSize;
			Boolean usePosition = _viewModel.SaveWindowPosition;
			if (_viewModel.LoadPlacement() is not { } saved || (!useSize && !usePosition))
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen;
				return;
			}

			// What the user does not want kept is the default: the size of the design, the middle of the screen.
			WindowPlacement placement = new WindowPlacement(
				saved.Left,
				saved.Top,
				useSize ? saved.Width : Width,
				useSize ? saved.Height : Height,
				useSize && saved.IsMaximized).FitInto(
				SystemParameters.VirtualScreenLeft,
				SystemParameters.VirtualScreenTop,
				SystemParameters.VirtualScreenWidth,
				SystemParameters.VirtualScreenHeight);

			Width = Math.Max(placement.Width, MinWidth);
			Height = Math.Max(placement.Height, MinHeight);
			if (usePosition)
			{
				WindowStartupLocation = WindowStartupLocation.Manual;
				Left = placement.Left;
				Top = placement.Top;
			}
			else
			{
				WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}

			if (placement.IsMaximized)
			{
				WindowState = WindowState.Maximized;
			}
		}

		/// <summary>The scroll bars that were moved by the program to follow the other version of the file (not by the user).</summary>
		private readonly Dictionary<ScrollViewer, (Double Vertical, Double Horizontal)> _followed = new();

		/// <summary>
		/// The two versions of a compared file scroll together: when one of the two viewers scrolls, the matching scroll bar of the
		/// other one goes to the same place.
		/// </summary>
		private void OnViewerScrollChanged(Object sender, ScrollChangedEventArgs e)
		{
			if (!_viewModel.IsCompareSyncScroll || e.OriginalSource is not ScrollViewer source || (e.VerticalChange == 0 && e.HorizontalChange == 0))
			{
				return;
			}

			// The scroll bar that was just moved by the program is not followed back.
			if (_followed.Remove(source, out (Double Vertical, Double Horizontal) expected)
				&& Math.Abs(source.VerticalOffset - expected.Vertical) < 1
				&& Math.Abs(source.HorizontalOffset - expected.Horizontal) < 1)
			{
				return;
			}

			Border own = (Border)sender;
			Border other = ReferenceEquals(own, LeftViewerHost) ? RightViewerHost : LeftViewerHost;
			Int32 index = ScrollViewersOf(own).IndexOf(source);
			List<ScrollViewer> candidates = ScrollViewersOf(other);
			if (index < 0 || index >= candidates.Count)
			{
				return;
			}

			ScrollViewer target = candidates[index];
			Double vertical = Math.Min(source.VerticalOffset, target.ScrollableHeight);
			Double horizontal = Math.Min(source.HorizontalOffset, target.ScrollableWidth);
			if (Math.Abs(target.VerticalOffset - vertical) < 0.5 && Math.Abs(target.HorizontalOffset - horizontal) < 0.5)
			{
				return;
			}

			_followed[target] = (vertical, horizontal);
			target.ScrollToVerticalOffset(vertical);
			target.ScrollToHorizontalOffset(horizontal);
		}

		private static List<ScrollViewer> ScrollViewersOf(DependencyObject root)
		{
			List<ScrollViewer> found = new();
			void Walk(DependencyObject node)
			{
				for (Int32 index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(node, index);
					if (child is ScrollViewer viewer)
					{
						found.Add(viewer);
					}

					Walk(child);
				}
			}

			Walk(root);
			return found;
		}

		private void SavePlacement()
		{
			Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
			_viewModel.SavePlacement(new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized));
		}

		private static T? FindAncestor<T>(DependencyObject? start)
			where T : DependencyObject
		{
			while (start != null)
			{
				if (start is T match)
				{
					return match;
				}

				start = start is Visual or System.Windows.Media.Media3D.Visual3D
					? VisualTreeHelper.GetParent(start)
					: LogicalTreeHelper.GetParent(start);
			}

			return null;
		}

		#region Window

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (!_viewModel.ConfirmExit())
			{
				e.Cancel = true;
				return;
			}

			SavePlacement();
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

		private void Execute(ICommand command)
		{
			if (command.CanExecute(null))
			{
				command.Execute(null);
			}
		}

		private void OnRevealRequested(ListEntry item)
		{
			FilesList.SelectedItems.Clear();
			FilesList.SelectedItem = item;
			FilesList.ScrollIntoView(item);
		}

		#endregion

		#region Folder tree

		private void OnTreeSelectedItemChanged(Object sender, RoutedPropertyChangedEventArgs<Object> e)
		{
			_viewModel.OnTreeSelectionChanged(e.NewValue as FolderNode);
		}

		private void OnTreePreviewRightMouseDown(Object sender, MouseButtonEventArgs e)
		{
			// Right click selects the node first, as in the file explorer.
			if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { DataContext: FolderNode node })
			{
				_viewModel.SelectFolder(node);
			}
		}

		#endregion

		#region File list

		private void OnListSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			_viewModel.SetSelectedItems(FilesList.SelectedItems.Cast<ListEntry>().ToList());
		}

		private void OnListDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject) != null)
			{
				Execute(_viewModel.OpenItemCommand);
			}
		}

		private void OnColumnHeaderClick(Object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is GridViewColumnHeader { Column: { } column } && FilesList.View is GridView view)
			{
				DatSortColumn sortColumn = view.Columns.IndexOf(column) switch
				{
					1 => DatSortColumn.Type,
					2 => DatSortColumn.Size,
					3 => DatSortColumn.PackedSize,
					_ => DatSortColumn.Name,
				};

				_viewModel.SortBy(sortColumn);
			}
		}

		#endregion
	}
}