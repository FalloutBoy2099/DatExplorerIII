namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;

	/// <summary>The archives of Temple of Elemental Evil: the footer of Arcanum, and an index that is a tree of folders.</summary>
	public sealed class ToeeArchiveTests : IDisposable
	{
		private readonly String _folder;

		public ToeeArchiveTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		/// <summary>The data "hello" and "world!", then an index: the root, the folder art, the folder art\tiles, a file in each.</summary>
		private String Build()
		{
			using MemoryStream file = new();
			using BinaryWriter writer = new(file);
			writer.Write(Encoding.ASCII.GetBytes("hello"));
			writer.Write(Encoding.ASCII.GetBytes("world!"));

			using MemoryStream index = new();
			using (BinaryWriter entries = new(index))
			{
				entries.Write(5);
				void Entry(String name, Int32 flags, Int32 size, Int32 offset, Int32 parent)
				{
					Byte[] bytes = Encoding.ASCII.GetBytes(name + '\0');
					entries.Write(bytes.Length);
					entries.Write(bytes);
					entries.Write(0);
					entries.Write(flags);
					entries.Write(size);
					entries.Write(size);
					entries.Write(offset);
					entries.Write(parent);
					entries.Write(-1);
					entries.Write(-1);
				}

				Entry(".", 0x400, 0, 0, -1);
				Entry("art", 0x400, 0, 0, 0);
				Entry("tiles", 0x400, 0, 0, 1);
				Entry("a.txt", 1, 5, 0, 1);
				Entry("b.txt", 1, 6, 5, 2);
			}

			Byte[] indexBytes = index.ToArray();
			writer.Write(indexBytes);
			writer.Write(new Byte[16]);
			writer.Write(Encoding.ASCII.GetBytes("1TAD"));
			writer.Write(0);
			writer.Write(indexBytes.Length + 28);
			writer.Flush();

			String path = Path.Combine(_folder, "toee.dat");
			File.WriteAllBytes(path, file.ToArray());
			return path;
		}

		[Fact]
		public void TheTree_IsReadAsPaths_AndTheFormatIsToldFromArcanum()
		{
			using DatArchive archive = DatArchive.Open(Build());

			Assert.Equal(DatFormat.Toee, archive.Format);
			Assert.Equal(["art\\a.txt", "art\\tiles\\b.txt"], archive.Entries.Select(entry => entry.FullPath).OrderBy(path => path, StringComparer.Ordinal).ToList());
			Assert.Equal("hello", Encoding.ASCII.GetString(archive.ReadData(archive.Entries.First(entry => entry.Name == "a.txt"))));
			Assert.Equal("world!", Encoding.ASCII.GetString(archive.ReadData(archive.Entries.First(entry => entry.Name == "b.txt"))));
		}

		[Fact]
		public void AToeeArchive_CannotBeSaved()
		{
			using DatArchive archive = DatArchive.Open(Build());
			archive.Remove([archive.Entries.First()]);

			DatException error = Assert.Throws<DatException>(() => archive.Save());

			Assert.Equal(DatErrorKind.ReadOnlyFormat, error.Kind);
		}
	}
}
