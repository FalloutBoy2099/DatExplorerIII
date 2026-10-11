namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows;
	using Models;

	/// <summary>
	/// Access to the localized strings (Resources\Strings.*.xaml). The English strings are always loaded; the ones
	/// of the chosen language are merged over them, so a missing translation shows the English text.
	/// </summary>
	public static class Loc
	{
		private const String DefaultLanguage = "en";

		private static ResourceDictionary? _translation;

		/// <summary>
		/// The languages that can be chosen.
		/// </summary>
		public static IReadOnlyList<LanguageInfo> Languages { get; } =
		[
			new LanguageInfo("en", "English"),
			new LanguageInfo("de", "Deutsch"),
			new LanguageInfo("es", "Español"),
			new LanguageInfo("fr", "Français"),
			new LanguageInfo("it", "Italiano"),
			new LanguageInfo("pt", "Português"),
			new LanguageInfo("ru", "Русский"),
			new LanguageInfo("sv", "Svenska"),
			new LanguageInfo("pl", "Polski"),
			new LanguageInfo("cs", "Čeština"),
			new LanguageInfo("zh", "简体中文"),
		];

		public static String Language { get; private set; } = DefaultLanguage;

		/// <summary>
		/// The language of the operating system when it is one of ours, English otherwise.
		/// </summary>
		public static String DetectSystemLanguage()
		{
			return Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
		}

		/// <summary>
		/// Loads the English strings, then the ones of the given language over them.
		/// </summary>
		public static void Initialize(String code)
		{
			Application.Current.Resources.MergedDictionaries.Add(Load(DefaultLanguage));
			SetLanguage(code);
		}

		/// <summary>
		/// Changes the language of the strings. Everything bound with DynamicResource follows at once.
		/// </summary>
		public static void SetLanguage(String code)
		{
			Language = Normalize(code);

			if (_translation != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(_translation);
				_translation = null;
			}

			if (Language != DefaultLanguage)
			{
				_translation = Load(Language);
				Application.Current.Resources.MergedDictionaries.Add(_translation);
			}
		}

		public static String Get(String key)
		{
			return Application.Current?.TryFindResource(key) as String ?? key;
		}

		/// <summary>Looks a text up without falling back to its key.</summary>
		public static Boolean TryGet(String key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out String? text)
		{
			text = Application.Current?.TryFindResource(key) as String;
			return text != null;
		}

		public static String Format(String key, params Object[] arguments)
		{
			String format = Get(key);
			try
			{
				return String.Format(CultureInfo.CurrentCulture, format, arguments);
			}
			catch (FormatException)
			{
				// A text with a mistake in its braces (a translation) is shown as it is rather than bringing the program down.
				return format;
			}
		}

		private static String Normalize(String code)
		{
			String lower = code.ToLowerInvariant();
			return Languages.Any(language => language.Code == lower) ? lower : DefaultLanguage;
		}

		private static ResourceDictionary Load(String language)
		{
			Uri source = new($"pack://application:,,,/Resources/Strings.{language}.xaml", UriKind.Absolute);
			return new ResourceDictionary { Source = source };
		}
	}
}