namespace DatExplorerIII.Tests
{
	using System;
	using System.IO;
	using DatExplorerIII.Services;

	/// <summary>The settings are kept in a folder named after the program; the ones of its former name are taken over.</summary>
	public sealed class SettingsServiceTests : IDisposable
	{
		private readonly String _root = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		public void Dispose()
		{
			if (Directory.Exists(_root))
			{
				Directory.Delete(_root, true);
			}
		}

		private String Folder(String name)
		{
			String path = Path.Combine(_root, name);
			Directory.CreateDirectory(path);
			return path;
		}

		[Fact]
		public void TheSettingsOfTheFormerName_AreCopiedOnTheFirstStart()
		{
			String legacy = Folder("legacy");
			File.WriteAllText(Path.Combine(legacy, "settings.json"), "{ \"Theme\": \"Dark\", \"SingleInstance\": false }");

			SettingsService settings = new(Path.Combine(_root, "current"), legacy);

			Assert.Equal("Dark", settings.Data.Theme);
			Assert.False(settings.Data.SingleInstance);
			Assert.True(File.Exists(Path.Combine(_root, "current", "settings.json")));

			// The former file is left alone.
			Assert.True(File.Exists(Path.Combine(legacy, "settings.json")));
		}

		[Fact]
		public void TheSettingsOfTheNewName_AreNotReplacedByTheFormerOnes()
		{
			String legacy = Folder("legacy");
			File.WriteAllText(Path.Combine(legacy, "settings.json"), "{ \"Theme\": \"Dark\" }");
			String current = Folder("current");
			File.WriteAllText(Path.Combine(current, "settings.json"), "{ \"Theme\": \"Light\" }");

			SettingsService settings = new(current, legacy);

			Assert.Equal("Light", settings.Data.Theme);
		}

		[Fact]
		public void WithoutAnySettings_TheDefaultsAreUsed_AndSavedSettingsAreReadAgain()
		{
			String current = Path.Combine(_root, "current");
			SettingsService first = new(current, Path.Combine(_root, "missing"));
			Assert.Equal("Automatic", first.Data.Theme);
			Assert.True(first.Data.SingleInstance);

			first.Data.Theme = "Light";
			first.Save();

			Assert.Equal("Light", new SettingsService(current, null).Data.Theme);
		}
	}
}