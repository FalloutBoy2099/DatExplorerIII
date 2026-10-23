namespace DatExplorerIII.Views.Main
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Media;
	using Models.Interface;
	using Services.Interface;
	using ViewModels.Main;

	/// <summary>The title bar of the main window, in the theme of the program: the buttons of the theme, of the language and of the window.</summary>
	public partial class MainTitleBar : UserControl
	{
		public MainTitleBar()
		{
			InitializeComponent();
			Loaded += OnLoaded;
		}

		private MainViewModel ViewModel => (MainViewModel)DataContext;

		private Window? Host => Window.GetWindow(this);

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (Host is { } window)
			{
				window.StateChanged -= OnStateChanged;
				window.StateChanged += OnStateChanged;
				ShowMaximizeGlyph(window);
			}
		}

		private void OnStateChanged(Object? sender, EventArgs e)
		{
			if (Host is { } window)
				ShowMaximizeGlyph(window);
		}

		/// <summary>The button shows what it does: it maximizes the window, or it restores it.</summary>
		private void ShowMaximizeGlyph(Window window)
		{
			MaximizeButton.Content = window.WindowState == WindowState.Maximized ? "" : "";
		}

		private void OnMinimizeClick(Object sender, RoutedEventArgs e)
		{
			if (Host is { } window)
				SystemCommands.MinimizeWindow(window);
		}

		private void OnMaximizeClick(Object sender, RoutedEventArgs e)
		{
			if (Host is not { } window)
				return;

			if (window.WindowState == WindowState.Maximized)
			{
				SystemCommands.RestoreWindow(window);
			}
			else
			{
				SystemCommands.MaximizeWindow(window);
			}
		}

		private void OnCloseClick(Object sender, RoutedEventArgs e)
		{
			if (Host is { } window)
				SystemCommands.CloseWindow(window);
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
				item.Command = ViewModel.ChangeLanguageCommand;
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
	}
}