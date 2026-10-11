namespace DatExplorerIII.Services
{
	using System;
	using System.Windows;
	using Microsoft.Win32;
	using Models;
	using Views;

	/// <summary>
	/// Applies the theme preference to the application and remembers it.
	/// </summary>
	public static class ThemeManager
	{
		private static SettingsService? _settings;

		public static ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

		/// <summary>
		/// Reads the remembered preference and applies it.
		/// </summary>
		public static void Initialize(SettingsService settings)
		{
			_settings = settings;
			Preference = Parse(settings.Data.Theme);
			ApplyToApplication();

			// With the automatic theme, the title bars follow Windows when the user changes its theme.
			SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
			Application.Current.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
		}

		/// <summary>
		/// Changes the theme of the running application and remembers the choice.
		/// </summary>
		public static void SetPreference(ThemePreference preference)
		{
			Preference = preference;
			if (_settings != null)
			{
				_settings.Data.Theme = preference.ToString();
				_settings.Save();
			}

			ApplyToApplication();

			foreach (Window window in Application.Current.Windows)
			{
				TitleBarTheme.Apply(window);
			}
		}

		/// <summary>
		/// Whether the application is drawn with the dark theme, which depends on the preference and on Windows.
		/// </summary>
		public static Boolean IsDark()
		{
			return Preference switch
			{
				ThemePreference.Light => false,
				ThemePreference.Dark => true,
				_ => IsWindowsDark(),
			};
		}

		private static void OnUserPreferenceChanged(Object sender, UserPreferenceChangedEventArgs e)
		{
			if (Preference != ThemePreference.Automatic || e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
			{
				return;
			}

			Application.Current?.Dispatcher.BeginInvoke(() =>
			{
				foreach (Window window in Application.Current.Windows)
				{
					TitleBarTheme.Apply(window);
				}
			});
		}

		internal static ThemePreference Parse(String? text)
		{
			return Enum.TryParse(text, true, out ThemePreference preference) && Enum.IsDefined(preference) ? preference : ThemePreference.Automatic;
		}

		private static Boolean IsWindowsDark()
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
			return key?.GetValue("AppsUseLightTheme") is Int32 light && light == 0;
		}

		private static void ApplyToApplication()
		{
			Application.Current.ThemeMode = Preference switch
			{
				ThemePreference.Light => ThemeMode.Light,
				ThemePreference.Dark => ThemeMode.Dark,
				_ => ThemeMode.System,
			};
		}
	}
}