namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;

	/// <summary>The texts of the interface: each one is used, and each one that is used exists.</summary>
	public sealed class StringKeysTests
	{
		private static List<String> DefinedKeys()
		{
			return Regex.Matches(SourceFiles.Read("Resources", "Strings.en.xaml"), "x:Key=\"(Str\\.[A-Za-z0-9_]+)\"")
				.Select(match => match.Groups[1].Value)
				.ToList();
		}

		/// <summary>The keys the source files name: in the XAML, and the quoted ones of the code (some are only the beginning of a key).</summary>
		private static List<String> NamedKeys()
		{
			List<String> names = [];
			foreach (String path in SourceFiles.Files(".xaml").Where(path => !Path.GetFileName(path).StartsWith("Strings.", StringComparison.Ordinal)))
			{
				names.AddRange(Regex.Matches(File.ReadAllText(path), "Str\\.[A-Za-z0-9_]+").Select(match => match.Value));
			}

			foreach (String path in SourceFiles.Files(".cs"))
			{
				names.AddRange(Regex.Matches(File.ReadAllText(path), "\"(Str\\.[A-Za-z0-9_]*)").Select(match => match.Groups[1].Value));
			}

			return names;
		}

		[Fact]
		public void EveryText_IsUsedByTheProgram()
		{
			List<String> named = NamedKeys();

			// A key is used when it is named, or when it is the beginning of a name the code finishes (Str.Trait + the number of the trait).
			List<String> unused = DefinedKeys()
				.Where(key => !named.Any(name => name == key || (key.StartsWith(name, StringComparison.Ordinal) && Regex.IsMatch(key.Substring(name.Length), "^([0-9]+|[A-Z][A-Za-z0-9]*)$"))))
				.ToList();

			Assert.True(unused.Count == 0, "Texts that nothing uses: " + String.Join(", ", unused));
		}

		[Fact]
		public void EveryTextTheProgramNames_Exists()
		{
			HashSet<String> defined = DefinedKeys().ToHashSet();
			List<String> missing = [];
			foreach (String path in SourceFiles.Files(".xaml").Where(path => !Path.GetFileName(path).StartsWith("Strings.", StringComparison.Ordinal)))
			{
				missing.AddRange(Regex.Matches(File.ReadAllText(path), "Str\\.[A-Za-z0-9_]+").Select(match => match.Value).Where(key => !defined.Contains(key)));
			}

			Assert.True(missing.Count == 0, "Texts of the XAML that do not exist: " + String.Join(", ", missing.Distinct()));
		}
	}
}