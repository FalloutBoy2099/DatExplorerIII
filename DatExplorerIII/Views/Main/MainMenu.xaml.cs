namespace DatExplorerIII.Views.Main
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using Services;
	using ViewModels.Main;

	/// <summary>The menu of the main window. The recent files are added to the File menu each time it opens.</summary>
	public partial class MainMenu : UserControl
	{
		private const String RecentTag = "recent";

		public MainMenu()
		{
			InitializeComponent();
		}

		private MainViewModel ViewModel => (MainViewModel)DataContext;

		/// <summary>
		/// The recent files are added to the File menu each time it opens: a separator, the line that
		/// clears the list, a separator and a line for each file; then comes the separator before "Close". The lines of the last time are removed first.
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

			IReadOnlyList<String> files = ViewModel.RecentFiles;
			if (files.Count == 0)
			{
				return;
			}

			// The line that clears the list comes first, and a separator sets it apart from the files.
			List<Control> lines = new()
			{
				new Separator { Tag = RecentTag },
				new MenuItem { Header = (String)FindResource("Str.ClearRecent"), Command = ViewModel.ClearRecentCommand, Tag = RecentTag },
				new Separator { Tag = RecentTag },
			};
			for (Int32 i = 0; i < files.Count; i++)
			{
				// The digit is the access key of the line (1 to 9, and 0 for the tenth); an underscore of the path is not one.
				String number = i < 9 ? "_" + (i + 1) : "1_0";
				lines.Add(new MenuItem
				{
					Header = number + " " + RecentFiles.Shorten(files[i]).Replace("_", "__", StringComparison.Ordinal),
					ToolTip = files[i],
					Command = ViewModel.OpenRecentCommand,
					CommandParameter = files[i],
					Tag = RecentTag,
				});
			}

			// The list ends before the separator that comes before "Close".
			Int32 index = FileMenu.Items.IndexOf(CloseSeparator);
			foreach (Control line in lines)
			{
				FileMenu.Items.Insert(index++, line);
			}
		}

		private void OnExit(Object sender, RoutedEventArgs e)
		{
			Window.GetWindow(this)?.Close();
		}
	}
}