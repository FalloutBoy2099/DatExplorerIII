namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;

	public sealed class LocalizationTests
	{
		private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static String StringsFolder => Path.Combine(AppContext.BaseDirectory, "Strings");

		public static IEnumerable<Object[]> Languages()
		{
			return Directory.GetFiles(StringsFolder, "Strings.*.xaml")
				.Select(path => Path.GetFileNameWithoutExtension(path).Substring("Strings.".Length))
				.Where(language => language != "en")
				.Select(language => new Object[] { language });
		}

		private static Dictionary<String, String> Load(String language)
		{
			XDocument document = XDocument.Load(Path.Combine(StringsFolder, $"Strings.{language}.xaml"));
			return document.Root!.Elements().ToDictionary(element => (String)element.Attribute(XamlNamespace + "Key")!, element => element.Value);
		}

		private static String[] Placeholders(String text)
		{
			return Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Distinct().OrderBy(value => value).ToArray();
		}

		[Fact]
		public void EnglishIsPresent()
		{
			Assert.NotEmpty(Load("en"));
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void Translation_HasTheSameKeysAsEnglish(String language)
		{
			Dictionary<String, String> english = Load("en");
			Dictionary<String, String> translation = Load(language);

			Assert.Empty(english.Keys.Except(translation.Keys));
			Assert.Empty(translation.Keys.Except(english.Keys));
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void Translation_KeepsThePlaceholders(String language)
		{
			Dictionary<String, String> english = Load("en");
			Dictionary<String, String> translation = Load(language);

			foreach (KeyValuePair<String, String> pair in english)
			{
				Assert.True(
					Placeholders(pair.Value).SequenceEqual(Placeholders(translation[pair.Key])),
					$"{language}: placeholders of {pair.Key} differ from English.");
			}
		}

		[Fact]
		public void AllExpectedLanguagesAreShipped()
		{
			String[] shipped = Languages().Select(row => (String)row[0]).OrderBy(language => language).ToArray();
			Assert.Equal(new[] { "cs", "de", "es", "fr", "it", "pl", "pt", "ru", "sv", "zh" }, shipped);
		}

		[Fact]
		public void LanguageMenu_ListsEveryShippedLanguage()
		{
			String[] menu = Loc.Languages.Select(language => language.Code).OrderBy(code => code).ToArray();
			String[] shipped = Languages().Select(row => (String)row[0]).Append("en").OrderBy(code => code).ToArray();
			Assert.Equal(shipped, menu);
			Assert.All(Loc.Languages, language => Assert.NotEmpty(language.NativeName));
		}

		[Theory]
		[InlineData("dark", ThemePreference.Dark)]
		[InlineData("Light", ThemePreference.Light)]
		[InlineData("Automatic", ThemePreference.Automatic)]
		[InlineData("", ThemePreference.Automatic)]
		[InlineData(null, ThemePreference.Automatic)]
		[InlineData("42", ThemePreference.Automatic)]
		public void ThemePreference_IsParsedLeniently(String? text, ThemePreference expected)
		{
			Assert.Equal(expected, ThemeManager.Parse(text));
		}
	}
}