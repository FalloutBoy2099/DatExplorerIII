namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;

	/// <summary>Extraction with no folders: the files that share a name get the suffix _1, _2...</summary>
	public sealed class FlatExtractionTests : IDisposable
	{
		private readonly String _folder;

		public FlatExtractionTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		/// <summary>Three files named a.txt in three folders (the contents tell them apart), and a b.txt.</summary>
		private DatArchive Build()
		{
			String path = Path.Combine(_folder, "test.zip");
			using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
			{
				foreach ((String name, String content) in new[] { ("art/a.txt", "art"), ("sound/a.txt", "sound"), ("data/a.txt", "data"), ("data/b.txt", "b") })
				{
					using StreamWriter writer = new(zip.CreateEntry(name).Open());
					writer.Write(content);
				}
			}

			return DatArchive.Open(path);
		}

		[Fact]
		public void TheFirstFileKeepsItsName_TheOthersGetUnderscoreOneThenTwo()
		{
			using DatArchive archive = Build();
			Dictionary<DatEntry, String> names = DatArchive.GetFlatNames(archive.Entries);

			Assert.Equal(["a.txt", "a_1.txt", "a_2.txt", "b.txt"], archive.Entries.Select(entry => names[entry]).ToArray());
		}

		[Fact]
		public void TheNames_DoNotChange_WhenAFileIsNotExtracted()
		{
			using DatArchive archive = Build();
			Dictionary<DatEntry, String> names = DatArchive.GetFlatNames(archive.Entries);
			String output = Path.Combine(_folder, "out");
			Directory.CreateDirectory(output);
			File.WriteAllText(Path.Combine(output, "a.txt"), "already there");

			// The user skipped the first a.txt, which is on the disk: only the second one is extracted, as a_1.txt, and a.txt is not touched.
			DatEntry second = archive.Entries.First(entry => entry.FullPath == "sound\\a.txt");
			DatExtractResult result = archive.Extract([second], output, String.Empty, null, default, names);

			Assert.Equal(1, result.Succeeded);
			Assert.Equal("already there", File.ReadAllText(Path.Combine(output, "a.txt")));
			Assert.Equal("sound", File.ReadAllText(Path.Combine(output, "a_1.txt")));
		}

		[Fact]
		public void ExtractingEverything_WritesEachFileUnderItsOwnName()
		{
			using DatArchive archive = Build();
			String output = Path.Combine(_folder, "out");

			DatExtractResult result = archive.Extract(archive.Entries.ToList(), output, String.Empty, null, default, DatArchive.GetFlatNames(archive.Entries));

			Assert.Equal(4, result.Succeeded);
			Assert.Equal("art", File.ReadAllText(Path.Combine(output, "a.txt")));
			Assert.Equal("sound", File.ReadAllText(Path.Combine(output, "a_1.txt")));
			Assert.Equal("data", File.ReadAllText(Path.Combine(output, "a_2.txt")));
			Assert.Equal("b", File.ReadAllText(Path.Combine(output, "b.txt")));
			Assert.Empty(Directory.GetDirectories(output));
		}
	}
}
