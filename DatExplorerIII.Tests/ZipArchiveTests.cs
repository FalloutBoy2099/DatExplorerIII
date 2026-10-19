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

		[Fact]
		public void AZipThatAnnouncesAnImpossibleSize_IsCorrupt_AndNotReadIntoMemory()
		{
			String path = Build();
			Byte[] bytes = File.ReadAllBytes(path);

			// The size of the content of the first file in the central directory (signature PK 1 2, then 24 bytes) becomes 100 million.
			for (Int32 i = 0; i < bytes.Length - 4; i++)
			{
				if (bytes[i] == 0x50 && bytes[i + 1] == 0x4B && bytes[i + 2] == 1 && bytes[i + 3] == 2)
				{
					System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i + 24), 100_000_000);
					break;
				}
			}

			File.WriteAllBytes(path, bytes);
			using DatArchive archive = DatArchive.Open(path);
			DatEntry big = archive.Entries.First(entry => entry.UnpackedSize == 100_000_000);

			Assert.Equal(DatErrorKind.CorruptData, Assert.Throws<DatException>(() => archive.ReadData(big)).Kind);
		}

		[Fact]
		public void ADat_WhoseFirstFileStartsLikeAZip_IsStillADat()
		{
			Byte[] content = new Byte[300];
			new Random(5).NextBytes(content);
			content[0] = (Byte)'P';
			content[1] = (Byte)'K';
			content[2] = 3;
			content[3] = 4;
			String source = Path.Combine(_folder, "first.bin");
			File.WriteAllBytes(source, content);
			String path = Path.Combine(_folder, "odd.dat");

			using (DatArchive created = DatArchive.Create(path, DatFormat.Fallout2))
			{
				created.AddFile(source, String.Empty);
				created.Save();
			}

			using DatArchive archive = DatArchive.Open(path);

			Assert.Equal(DatFormat.Fallout2, archive.Format);
			Assert.Equal(content, archive.ReadData(archive.Find("first.bin")!));
		}
	}
}