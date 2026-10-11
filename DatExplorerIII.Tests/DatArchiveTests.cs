namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;

	public sealed class DatArchiveTests : IDisposable
	{
		private readonly String _folder;

		public DatArchiveTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		private String CreateSource(String name, Byte[] content)
		{
			String path = Path.Combine(_folder, "source", name);
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, content);
			return path;
		}

		private static Byte[] Compressible(Int32 length)
		{
			return Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("fallout ", length / 8 + 1))).Take(length).ToArray();
		}

		private static Byte[] Random(Int32 length, Int32 seed)
		{
			Byte[] data = new Byte[length];
			new Random(seed).NextBytes(data);
			return data;
		}

		private String ArchivePath(String name)
		{
			return Path.Combine(_folder, name);
		}

		private DatArchive BuildSample(DatFormat format, String name, out Dictionary<String, Byte[]> expected)
		{
			expected = new Dictionary<String, Byte[]>
			{
				["art\\critters\\hero.frm"] = Compressible(5000),
				["art\\critters\\npc.frm"] = Random(700, 1),
				["art\\items\\empty.txt"] = Array.Empty<Byte>(),
				["data\\text.msg"] = Compressible(100),
				["root.cfg"] = Random(33, 2),
			};

			DatArchive archive = DatArchive.Create(ArchivePath(name), format);
			foreach (KeyValuePair<String, Byte[]> pair in expected)
			{
				Int32 separator = pair.Key.LastIndexOf('\\') + 1;
				String source = CreateSource(pair.Key.Replace('\\', '_'), pair.Value);
				archive.AddFile(source, pair.Key.Substring(0, separator), pair.Key.Substring(separator));
			}

			return archive;
		}

		private static void AssertContent(DatArchive archive, Dictionary<String, Byte[]> expected)
		{
			Assert.Equal(expected.Count, archive.Count);
			foreach (KeyValuePair<String, Byte[]> pair in expected)
			{
				DatEntry? entry = archive.Find(pair.Key.ToUpperInvariant());
				Assert.NotNull(entry);
				Assert.Equal(pair.Value, archive.ReadData(entry));
			}
		}

		[Theory]
		[InlineData(DatFormat.Fallout1)]
		[InlineData(DatFormat.Fallout2)]
		[InlineData(DatFormat.Arcanum)]
		public void SaveAndReopen_KeepsFiles(DatFormat format)
		{
			using DatArchive archive = BuildSample(format, "sample.dat", out Dictionary<String, Byte[]> expected);
			Assert.Equal(DatPendingChanges.Created | DatPendingChanges.Added, archive.PendingChanges);

			archive.Save();
			Assert.Equal(DatPendingChanges.None, archive.PendingChanges);
			AssertContent(archive, expected);

			using DatArchive reopened = DatArchive.Open(archive.FilePath);
			Assert.Equal(format, reopened.Format);
			AssertContent(reopened, expected);
			Assert.Contains("art\\critters\\", reopened.Folders.Select(folder => folder.ToLowerInvariant()));
			Assert.Contains("art\\", reopened.Folders.Select(folder => folder.ToLowerInvariant()));
		}

		[Fact]
		public void Fallout2_CompressesOnlyWhenItHelps()
		{
			using DatArchive archive = BuildSample(DatFormat.Fallout2, "compress.dat", out _);
			archive.Save();

			using DatArchive reopened = DatArchive.Open(archive.FilePath);
			DatEntry text = reopened.Find("art\\critters\\hero.frm")!;
			DatEntry noise = reopened.Find("art\\critters\\npc.frm")!;
			Assert.True(text.IsCompressed);
			Assert.True(text.PackedSize < text.UnpackedSize);
			Assert.False(noise.IsCompressed);
		}

		[Theory]
		[InlineData(DatFormat.Fallout1)]
		[InlineData(DatFormat.Fallout2)]
		[InlineData(DatFormat.Arcanum)]
		public void EmptyArchive_CanBeSavedAndReopened(DatFormat format)
		{
			using DatArchive archive = DatArchive.Create(ArchivePath("empty.dat"), format);
			archive.Save();

			using DatArchive reopened = DatArchive.Open(archive.FilePath);
			Assert.Equal(format, reopened.Format);
			Assert.Equal(0, reopened.Count);
		}

		[Theory]
		[InlineData(DatFormat.Fallout1)]
		[InlineData(DatFormat.Fallout2)]
		[InlineData(DatFormat.Arcanum)]
		public void Edit_DeleteRenameAdd_ThenFullSave(DatFormat format)
		{
			using (DatArchive created = BuildSample(format, "edit.dat", out _))
			{
				created.Save();
			}

			using DatArchive archive = DatArchive.Open(ArchivePath("edit.dat"));
			archive.Remove([archive.Find("data\\text.msg")!]);
			archive.Rename(archive.Find("root.cfg")!, "Renamed.CFG");
			String renamedFolder = archive.RenameFolder("art\\critters\\", "People");
			Assert.Equal("art\\People\\", renamedFolder);
			archive.AddFile(CreateSource("added.bin", Compressible(900)), "art\\people\\", "Added.bin");
			archive.AddFolder("empty\\folder\\");

			Assert.True(archive.PendingChanges.HasFlag(DatPendingChanges.Added));
			Assert.True(archive.PendingChanges.HasFlag(DatPendingChanges.Deleted));
			Assert.True(archive.PendingChanges.HasFlag(DatPendingChanges.Renamed));

			archive.Save();

			using DatArchive reopened = DatArchive.Open(archive.FilePath);
			Assert.Equal(5, reopened.Count);
			Assert.Null(reopened.Find("data\\text.msg"));
			Assert.Null(reopened.Find("art\\critters\\hero.frm"));
			Assert.Equal(Compressible(5000), reopened.ReadData(reopened.Find("art\\people\\hero.frm")!));
			Assert.Equal(Random(33, 2), reopened.ReadData(reopened.Find("renamed.cfg")!));
			Assert.Equal(Compressible(900), reopened.ReadData(reopened.Find("art\\people\\added.bin")!));
			Assert.Equal(Random(700, 1), reopened.ReadData(reopened.Find("art\\people\\npc.frm")!));
		}

		[Fact]
		public void Fallout2_QuickSave_AppendsAndRewritesTree()
		{
			using (DatArchive created = BuildSample(DatFormat.Fallout2, "quick.dat", out _))
			{
				created.Save();
			}

			String path = ArchivePath("quick.dat");
			using DatArchive archive = DatArchive.Open(path);
			archive.Remove([archive.Find("art\\items\\empty.txt")!]);
			archive.AddFile(CreateSource("quick.bin", Random(4000, 9)), "extra\\", "quick.bin");
			archive.AddFile(CreateSource("quick2.txt", Compressible(3000)), String.Empty, "quick2.txt");
			archive.Save(quick: true);
			Assert.Equal(DatPendingChanges.None, archive.PendingChanges);

			using DatArchive reopened = DatArchive.Open(path);
			Assert.Equal(6, reopened.Count);
			Assert.Null(reopened.Find("art\\items\\empty.txt"));
			Assert.Equal(Random(4000, 9), reopened.ReadData(reopened.Find("extra\\quick.bin")!));
			Assert.Equal(Compressible(3000), reopened.ReadData(reopened.Find("quick2.txt")!));
			Assert.Equal(Compressible(5000), reopened.ReadData(reopened.Find("art\\critters\\hero.frm")!));
			Assert.Equal(Random(33, 2), reopened.ReadData(reopened.Find("root.cfg")!));

			// The same instance stays usable after an in place save.
			Assert.Equal(Random(4000, 9), archive.ReadData(archive.Find("extra\\quick.bin")!));
		}

		[Fact]
		public void Fallout2_RenameOnly_RewritesTreeWithoutChangingData()
		{
			using (DatArchive created = BuildSample(DatFormat.Fallout2, "rename.dat", out _))
			{
				created.Save();
			}

			String path = ArchivePath("rename.dat");
			Int64 sizeBefore = new FileInfo(path).Length;

			using DatArchive archive = DatArchive.Open(path);
			archive.Rename(archive.Find("data\\text.msg")!, "other.msg");
			Assert.Equal(DatPendingChanges.Renamed, archive.PendingChanges);
			archive.Save();

			Assert.True(Math.Abs(new FileInfo(path).Length - sizeBefore) <= 8);
			using DatArchive reopened = DatArchive.Open(path);
			Assert.Equal(Compressible(100), reopened.ReadData(reopened.Find("data\\other.msg")!));
		}

		[Fact]
		public void Fallout1_Compressed_IsDecoded()
		{
			// Build a Fallout 1 archive by hand with one LZSS compressed file.
			Byte[] block = [0x00, 0x04, 0x01, (Byte)'A', 0xEE, 0xF2, 0x00, 0x00];
			using MemoryStream file = new();
			void BigEndian(Int32 value) => file.Write([(Byte)(value >> 24), (Byte)(value >> 16), (Byte)(value >> 8), (Byte)value]);

			BigEndian(1);
			BigEndian(1);
			BigEndian(0);
			BigEndian(0);
			file.WriteByte(1);
			file.WriteByte((Byte)'.');
			BigEndian(1);
			BigEndian(1);
			BigEndian(16);
			BigEndian(0);
			file.WriteByte(5);
			file.Write(Encoding.ASCII.GetBytes("A.TXT"));
			BigEndian(0x40);
			BigEndian((Int32)file.Position + 12);
			BigEndian(6);
			BigEndian(block.Length);
			file.Write(block);

			String path = ArchivePath("fo1lzss.dat");
			File.WriteAllBytes(path, file.ToArray());

			using DatArchive archive = DatArchive.Open(path);
			Assert.Equal(DatFormat.Fallout1, archive.Format);
			DatEntry entry = archive.Find("a.txt")!;
			Assert.True(entry.IsCompressed);
			Assert.Equal("AAAAAA", Encoding.ASCII.GetString(archive.ReadData(entry)));
		}

		[Fact]
		public void Lzss_DecodesLiteralsMatchesAndRawBlocks()
		{
			Byte[] compressed =
			[
				0x00, 0x09, 0xFF, (Byte)'A', (Byte)'B', (Byte)'C', (Byte)'D', (Byte)'E', (Byte)'F', (Byte)'G', (Byte)'H',
				0xFF, 0xFB, (Byte)'H', (Byte)'E', (Byte)'L', (Byte)'L', (Byte)'O',
				0x00, 0x00,
			];

			Byte[] result = Lzss.Decompress(compressed, 13);
			Assert.Equal("ABCDEFGHHELLO", Encoding.ASCII.GetString(result));
			Assert.Throws<InvalidDataException>(() => Lzss.Decompress(compressed, 20));
			Assert.Throws<InvalidDataException>(() => Lzss.Decompress(compressed, -1));
			Assert.Throws<InvalidDataException>(() => Lzss.Decompress(compressed, Int32.MaxValue));
		}

		[Fact]
		public void Extract_KeepsStructure_AndRefusesPathTraversal()
		{
			using DatArchive archive = BuildSample(DatFormat.Fallout2, "extract.dat", out _);
			archive.Save();

			String destination = Path.Combine(_folder, "out");
			List<DatEntry> entries = archive.GetEntriesUnder("art\\");
			DatExtractResult result = archive.Extract(entries, destination, String.Empty);
			Assert.Equal(3, result.Succeeded);
			Assert.Empty(result.Failures);
			Assert.True(File.Exists(Path.Combine(destination, "art", "critters", "hero.frm")));

			String stripped = Path.Combine(_folder, "out2");
			archive.Extract(archive.GetEntries("art\\critters\\").ToList(), stripped, "art\\critters\\");
			Assert.True(File.Exists(Path.Combine(stripped, "hero.frm")));

			DatEntry evil = new("..\\", "evil.txt");
			String root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
			Assert.Throws<DatException>(() => DatArchive.ResolveTarget(root, evil, String.Empty));
			DatEntry rooted = new("C:\\", "evil.txt");
			Assert.Throws<DatException>(() => DatArchive.ResolveTarget(root, rooted, String.Empty));
		}

		[Fact]
		public void Open_RejectsGarbage()
		{
			String path = ArchivePath("garbage.dat");
			File.WriteAllBytes(path, Random(1000, 5));
			Assert.Throws<DatException>(() => DatArchive.Open(path));

			File.WriteAllBytes(path, Array.Empty<Byte>());
			Assert.Throws<DatException>(() => DatArchive.Open(path));
		}

		#region Arcanum

		private static Byte[] Tail(String path, Int32 length)
		{
			using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			Byte[] tail = new Byte[length];
			stream.Position = stream.Length - length;
			stream.ReadExactly(tail);
			return tail;
		}

		[Fact]
		public void Arcanum_HasTheFooterOfTheGame_AndCompressesOnlyWhenItHelps()
		{
			using DatArchive archive = BuildSample(DatFormat.Arcanum, "arcanum.dat", out _);
			archive.Save();

			// 16 bytes of GUID, "1TAD", the length of the names, the length of the index and the footer.
			Byte[] footer = Tail(archive.FilePath, 28);
			Assert.Equal("1TAD", Encoding.ASCII.GetString(footer, 16, 4));
			Int32 nameTable = BitConverter.ToInt32(footer, 20);
			Int32 indexSize = BitConverter.ToInt32(footer, 24);
			Assert.True(indexSize > 28 + 4 + nameTable);
			Assert.Equal(archive.Entries.Sum(entry => entry.FullPath.Length + 1) + archive.Folders.Sum(folder => folder.TrimEnd('\\').Length + 1), nameTable);

			using DatArchive reopened = DatArchive.Open(archive.FilePath);
			Assert.Equal(DatFormat.Arcanum, reopened.Format);
			Assert.True(reopened.Find("art\\critters\\hero.frm")!.IsCompressed);
			Assert.False(reopened.Find("art\\critters\\npc.frm")!.IsCompressed);
			Assert.Equal(700, reopened.Find("art\\critters\\npc.frm")!.PackedSize);
		}

		[Fact]
		public void Arcanum_KeepsEmptyFolders_WhichFalloutArchivesCannot()
		{
			String source = CreateSource("a.txt", [1, 2, 3]);
			using (DatArchive archive = DatArchive.Create(ArchivePath("folders.dat"), DatFormat.Arcanum))
			{
				archive.AddFile(source, "text", "a.txt");
				archive.AddFolder("empty\\deeper");
				archive.Save();
			}

			using DatArchive reopened = DatArchive.Open(ArchivePath("folders.dat"));
			Assert.Equal(new[] { "empty\\", "empty\\deeper\\", "text\\" }, reopened.Folders.OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase).ToArray());
			Assert.Single(reopened.Entries);
			Assert.True(reopened.FolderExists("empty\\deeper"));
		}

		[Fact]
		public void Arcanum_KeepsItsIdentity_WhenItIsWrittenAgain()
		{
			String source = CreateSource("a.txt", [1, 2, 3]);
			String path = ArchivePath("identity.dat");
			using (DatArchive archive = DatArchive.Create(path, DatFormat.Arcanum))
			{
				archive.AddFile(source, "", "a.txt");
				archive.Save();
			}

			Byte[] guid = Tail(path, 28).AsSpan(0, 16).ToArray();
			Assert.NotEqual(new Byte[16], guid);

			using (DatArchive reopened = DatArchive.Open(path))
			{
				reopened.AddFile(CreateSource("b.txt", [4]), "", "b.txt");
				reopened.Save();
			}

			Assert.Equal(guid, Tail(path, 28).AsSpan(0, 16).ToArray());
			using DatArchive again = DatArchive.Open(path);
			Assert.Equal(2, again.Count);
		}

		[Fact]
		public void Arcanum_WithoutGuid_IsRead()
		{
			// Written by hand: a stored file, then the index, then the footer " TAD" that has no GUID.
			Byte[] content = [10, 20, 30];
			using MemoryStream file = new();
			file.Write(content);

			using MemoryStream index = new();
			using (BinaryWriter writer = new(index, Encoding.Latin1, true))
			{
				Byte[] name = Encoding.Latin1.GetBytes("data\\a.bin\0");
				writer.Write(1);
				writer.Write(name.Length);
				writer.Write(name);
				writer.Write(0);
				writer.Write(1);
				writer.Write(3);
				writer.Write(3);
				writer.Write(0);
			}

			file.Write(index.ToArray());
			file.Write(Encoding.ASCII.GetBytes(" TAD"));
			file.Write(BitConverter.GetBytes(11));
			file.Write(BitConverter.GetBytes((Int32)index.Length + 12));

			String path = ArchivePath("noguid.dat");
			File.WriteAllBytes(path, file.ToArray());

			using DatArchive archive = DatArchive.Open(path);
			Assert.Equal(DatFormat.Arcanum, archive.Format);
			Assert.Equal(content, archive.ReadData(archive.Find("data\\a.bin")!));
		}

		[Fact]
		public void Arcanum_ThatIsDamaged_IsReportedClearly()
		{
			String source = CreateSource("a.txt", Compressible(3000));
			String path = ArchivePath("damaged.dat");
			using (DatArchive archive = DatArchive.Create(path, DatFormat.Arcanum))
			{
				archive.AddFile(source, "x", "a.txt");
				archive.AddFile(CreateSource("b.txt", Compressible(800)), "x", "b.txt");
				archive.Save();
			}

			Byte[] bytes = File.ReadAllBytes(path);

			// The count of the index says more files than the index holds.
			Byte[] tooMany = (Byte[])bytes.Clone();
			Int32 indexStart = tooMany.Length - BitConverter.ToInt32(tooMany, tooMany.Length - 4);
			BitConverter.GetBytes(50).CopyTo(tooMany, indexStart);
			String tooManyPath = ArchivePath("toomany.dat");
			File.WriteAllBytes(tooManyPath, tooMany);
			Assert.Equal(DatErrorKind.InvalidTree, Assert.Throws<DatException>(() => DatArchive.Open(tooManyPath)).Kind);

			// In recovery mode, what can be read is read.
			using (DatArchive recovered = DatArchive.Open(tooManyPath, DatOpenMode.Recover))
			{
				Assert.Equal(2, recovered.Count);
				Assert.True(recovered.IsRecovered);
			}

			// A file that points outside of the archive is flagged, the others stay usable.
			Byte[] outside = (Byte[])bytes.Clone();

			// The offset of the last file is the last number of the index, just before the footer (28 bytes).
			Int32 lastOffsetField = outside.Length - 28 - 4;
			BitConverter.GetBytes(Int32.MaxValue - 100).CopyTo(outside, lastOffsetField);
			String outsidePath = ArchivePath("outside.dat");
			File.WriteAllBytes(outsidePath, outside);

			using DatArchive flagged = DatArchive.Open(outsidePath);
			Assert.Equal(1, flagged.DamagedCount);
			Assert.Equal(1, flagged.Entries.Count(entry => !entry.IsDamaged));
		}

		[Fact]
		public void Arcanum_PathsThatAreTooLongForTheGame_AreRefused()
		{
			String source = CreateSource("a.txt", [1]);
			using DatArchive archive = DatArchive.Create(ArchivePath("long.dat"), DatFormat.Arcanum);

			// The folders are allowed (200 characters), the whole path (262) is longer than the game accepts (259).
			archive.AddFile(source, String.Join("\\", Enumerable.Repeat(new String('d', 66), 3)), new String('f', 60) + ".txt");

			Assert.Equal(DatErrorKind.PathTooLong, Assert.Throws<DatException>(() => archive.Save()).Kind);
		}

		/// <summary>The real archives of the game are read, their files decompress, and writing them again loses nothing.</summary>
		[Fact]
		public void RealArcanum_Archives_AreReadAndWrittenAgain()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_ARCANUM_DIR");
			String? path = folder == null ? null : Path.Combine(folder, "ArcanumZPackFrench.dat");
			if (path == null || !File.Exists(path))
			{
				return;
			}

			String copy = ArchivePath("zpack.dat");
			File.Copy(path, copy);

			using (DatArchive original = DatArchive.Open(copy))
			{
				Assert.Equal(DatFormat.Arcanum, original.Format);
				Assert.Equal(1299, original.Count);
				Assert.Empty(original.Verify().Damaged);

				Dictionary<String, Byte[]> hashes = original.Entries.ToDictionary(entry => entry.FullPath, entry => System.Security.Cryptography.SHA256.HashData(original.ReadData(entry)));
				Byte[] guid = Tail(copy, 28).AsSpan(0, 16).ToArray();

				original.RequestRepair();
				original.Save();

				using DatArchive again = DatArchive.Open(copy);
				Assert.Equal(original.Count, again.Count);
				Assert.All(hashes, pair => Assert.Equal(pair.Value, System.Security.Cryptography.SHA256.HashData(again.ReadData(again.Find(pair.Key)!))));
				Assert.Equal(guid, Tail(copy, 28).AsSpan(0, 16).ToArray());
			}
		}

		#endregion

		[Fact]
		public void Names_AreValidated()
		{
			using DatArchive archive = DatArchive.Create(ArchivePath("names.dat"), DatFormat.Fallout2);
			String source = CreateSource("n.txt", [1]);
			Assert.Throws<DatException>(() => archive.AddFile(source, "a\\", "bad|name"));
			Assert.Throws<DatException>(() => archive.AddFile(source, "a\\", "bad\u20ACname"));
			archive.AddFile(source, "a/b", "ok.txt");
			Assert.NotNull(archive.Find("A\\B\\OK.TXT"));
			Assert.Throws<DatException>(() => archive.AddFile(source, "a\\b\\", "OK.txt"));
		}
	}
}