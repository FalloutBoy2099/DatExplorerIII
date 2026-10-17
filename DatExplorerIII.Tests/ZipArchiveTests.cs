namespace DatExplorerIII.Tests
{
	using System;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;

	/// <summary>ZIP files browsed like an archive.</summary>
	public sealed class ZipArchiveTests : IDisposable
	{
		private readonly String _folder;

		public ZipArchiveTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		private String Build()
		{
			String path = Path.Combine(_folder, "test.zip");
			using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create))
			{
				void Add(String name, String content)
				{
					using StreamWriter writer = new(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
					writer.Write(content);
				}

				Add("readme.txt", "hello");
				Add("art/tiles/a.txt", new String('x', 5000));
				zip.CreateEntry("empty/");
			}

			return path;
		}

		[Fact]
		public void AZip_IsBrowsedLikeAnArchive_FoldersAndFilesIncluded()
		{
			using DatArchive archive = DatArchive.Open(Build());

			Assert.Equal(DatFormat.Zip, archive.Format);
			Assert.Equal(["art\\tiles\\a.txt", "readme.txt"], archive.Entries.Select(entry => entry.FullPath).OrderBy(path => path, StringComparer.Ordinal).ToList());
			Assert.True(archive.FolderExists("art\\tiles\\"));
			Assert.True(archive.FolderExists("empty\\"));
			Assert.Equal("hello", Encoding.UTF8.GetString(archive.ReadData(archive.Find("readme.txt")!)));
			Assert.Equal(5000, archive.ReadData(archive.Find("art\\tiles\\a.txt")!).Length);
			Assert.True(archive.Find("art\\tiles\\a.txt")!.IsCompressed);
		}

		[Fact]
		public void AZip_CanBeExtracted_AndVerified_ButNotSaved()
		{
			using DatArchive archive = DatArchive.Open(Build());
			String output = Path.Combine(_folder, "out");

			DatExtractResult result = archive.Extract(archive.Entries.ToList(), output, String.Empty);

			Assert.Equal(2, result.Succeeded);
			Assert.Equal("hello", File.ReadAllText(Path.Combine(output, "readme.txt")));
			Assert.Empty(archive.Verify().Damaged);

			archive.Remove([archive.Find("readme.txt")!]);
			Assert.Equal(DatErrorKind.ReadOnlyFormat, Assert.Throws<DatException>(() => archive.Save()).Kind);
		}

		[Fact]
		public void ADamagedZip_IsNotADat()
		{
			String path = Path.Combine(_folder, "bad.zip");
			File.WriteAllBytes(path, [(Byte)'P', (Byte)'K', 3, 4, 1, 2, 3, 4, 5, 6, 7, 8]);

			Assert.Throws<DatException>(() => DatArchive.Open(path));
		}

		[Fact]
		public void ZipFiles_AreOpenedAsArchives_ButOnlyDatFilesAreDatFiles()
		{
			Assert.True(DatFiles.IsArchiveFile("a.ZIP"));
			Assert.True(DatFiles.IsArchiveFile("master.dat"));
			Assert.False(DatFiles.IsDatFile("a.zip"));
		}
	}
}
