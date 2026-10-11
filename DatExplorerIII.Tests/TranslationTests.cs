namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using DatExplorerIII.Services;

	/// <summary>
	/// What <see cref="LocalizationTests"/> does not check: no text is empty, the file dialog filters keep their patterns, and a
	/// text with a mistake in its braces does not bring the program down.
	/// </summary>
	public sealed class TranslationTests
	{
		private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

		private static String StringsFolder => Path.Combine(AppContext.BaseDirectory, "Strings");

		private static Dictionary<String, String> Load(String code)
		{
			XDocument document = XDocument.Load(Path.Combine(StringsFolder, $"Strings.{code}.xaml"));
			return document.Root!.Elements().ToDictionary(element => (String)element.Attribute(Xaml + "Key")!, element => element.Value);
		}

		public static IEnumerable<Object[]> Codes()
		{
			return Directory.GetFiles(StringsFolder, "Strings.*.xaml")
				.Select(path => Path.GetFileNameWithoutExtension(path)["Strings.".Length..])
				.Where(code => code != "en")
				.Select(code => new Object[] { code });
		}

		[Theory]
		[MemberData(nameof(Codes))]
		public void ALanguage_HasNoEmptyText(String code)
		{
			Assert.All(Load(code), pair => Assert.False(String.IsNullOrWhiteSpace(pair.Value), pair.Key));
		}

		[Theory]
		[MemberData(nameof(Codes))]
		public void ALanguage_KeepsTheSamePatternsInTheFileFilters(String code)
		{
			Dictionary<String, String> english = Load("en");
			Dictionary<String, String> translation = Load(code);

			// "name|pattern|name|pattern": the names are translated, the patterns are not.
			foreach (String key in english.Keys.Where(key => english[key].Contains('|', StringComparison.Ordinal) && english[key].Contains("*.", StringComparison.Ordinal)))
			{
				Assert.Equal(
					english[key].Split('|').Where((_, index) => index % 2 == 1).ToArray(),
					translation[key].Split('|').Where((_, index) => index % 2 == 1).ToArray());
			}
		}

		[Fact]
		public void Loc_FormatDoesNotFailOnAnInvalidText()
		{
			// Without the application (as here) the texts are not found and the key itself is the format.
			Assert.Equal("Str.Test", Loc.Format("Str.Test", 1, 2));
			Assert.Equal("Str.{Test", Loc.Format("Str.{Test", 1));
			Assert.Equal("Str.Test{0", Loc.Format("Str.Test{0", 1));
		}
	}
}