namespace DatExplorerIII.Services.Interface
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows;
	using Models.Interface;

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

		/// <summary>
		/// Which form of a plural text goes with a number, in a language. The forms of a plural text are written
		/// in this order, separated by a bar: for English, German, Spanish, Italian and Swedish "one|other"; for French and
		/// Portuguese (where 0 is singular too) "one|other"; for Russian "one|few|many" (1, 21, 31... | 2 to 4, 22 to 24... | the
		/// rest, and 11 to 14); for Polish the same, with 1 alone as the first form; for Czech "one|few|other" (1 | 2 to 4 | the
		/// rest); for Chinese a single form.
		/// </summary>
		public static Int32 PluralIndex(String language, Int64 count)
		{
			Int64 n = Math.Abs(count);
			Boolean few = n % 10 is >= 2 and <= 4 && n % 100 is not (>= 12 and <= 14);
			return language switch
			{
				"ru" => n % 10 == 1 && n % 100 != 11 ? 0 : few ? 1 : 2,
				"pl" => n == 1 ? 0 : few ? 1 : 2,
				"cs" => n == 1 ? 0 : n is >= 2 and <= 4 ? 1 : 2,
				"zh" => 0,
				"fr" or "pt" => n is 0 or 1 ? 0 : 1,
				_ => n == 1 ? 0 : 1,
			};
		}

		/// <summary>A text with the right form for a number: "{0} file|{0} files" gives "1 file" and "2 files" ({0} is the number).</summary>
		public static String Plural(String key, Int64 count)
		{
			String[] forms = Get(key).Split('|');
			String form = forms[Math.Min(PluralIndex(Language, count), forms.Length - 1)];
			try
			{
				return String.Format(CultureInfo.CurrentCulture, form, count);
			}
			catch (FormatException)
			{
				return form;
			}
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