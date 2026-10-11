namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;

	/// <summary>
	/// Reading damaged archives: recovery, verification, repair, and the guarantee that a hostile file
	/// can only produce a <see cref="DatException"/>.
	/// </summary>
	public sealed class DatRepairTests : IDisposable
	{
		private readonly String _folder;

		public DatRepairTests()
		{
			_folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		private static Byte[] Compressible(Int32 length)
		{
			return Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("vault tec ", length / 10 + 1))).Take(length).ToArray();
		}

		/// <summary>Builds an archive of six files (compressed and not) and returns its path.</summary>
		private String Build(DatFormat format, String name = "sample.dat")
		{
			String source = Path.Combine(_folder, "source");
			Directory.CreateDirectory(source);
			String path = Path.Combine(_folder, name);

			using DatArchive archive = DatArchive.Create(path, format);
			for (Int32 i = 0; i < 6; i++)
			{
				Byte[] data = i % 2 == 0 ? Compressible(2000 + i) : new Random(i).GetBytes(300 + i);
				String file = Path.Combine(source, $"f{i}.bin");
				File.WriteAllBytes(file, data);
				archive.AddFile(file, i < 3 ? "art\\" : "data\\", $"file{i}.dat");
			}

			archive.Save();
			return path;
		}

		/// <summary>Position of each entry of a Fallout 2 directory tree: the offset of its name length field.</summary>
		private static List<Int32> EntryPositions(Byte[] file)
		{
			Int32 treeSize = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(file.Length - 8));
			Int32 position = file.Length - 8 - treeSize;
			Int32 count = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(position));
			position += 4;

			List<Int32> positions = new();
			for (Int32 i = 0; i < count; i++)
			{
				positions.Add(position);
				Int32 nameLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(position));
				position += 4 + nameLength + 13;
			}

			return positions;
		}

		private static void Patch(String path, Action<Byte[]> change)
		{
			Byte[] bytes = File.ReadAllBytes(path);
			change(bytes);
			File.WriteAllBytes(path, bytes);
		}

		[Fact]
		public void AnnouncedSizes_ThatAreImpossible_AreDamageNotCrashes()
		{
			String path = Build(DatFormat.Fallout2);

			Patch(path, file =>
			{
				List<Int32> entries = EntryPositions(file);

				// First entry: unpacked size -1. Third entry: unpacked size 2 GB for compressed data.
				Int32 first = entries[0] + 4 + BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(entries[0])) + 1;
				BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(first), -1);
				Int32 third = entries[2] + 4 + BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(entries[2])) + 1;
				BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(third), Int32.MaxValue);
			});

			using DatArchive archive = DatArchive.Open(path);
			DatExtractResult result = archive.Extract(archive.Entries.ToList(), Path.Combine(_folder, "out"), String.Empty);

			Assert.Equal(2, result.Failures.Count);
			Assert.Equal(4, result.Succeeded);
			Assert.Contains(archive.Issues, issue => issue.Kind == DatIssueKind.EntryInvalidSize);
			Assert.Equal(DatErrorKind.CorruptData, Assert.Throws<DatException>(() => archive.ReadData(archive.Entries[2])).Kind);
		}

		[Fact]
		public void Fallout1_DirectoryNames_KeepTheirNonAsciiLetters()
		{
			String source = Path.Combine(_folder, "a.txt");
			File.WriteAllText(source, "hello");
			String path = Path.Combine(_folder, "fo1.dat");

			using (DatArchive archive = DatArchive.Create(path, DatFormat.Fallout1))
			{
				archive.AddFile(source, "ÿ\\café\\", "a.txt");
				archive.AddFile(source, "µ\\", "b.txt");
				archive.Save();
			}

			using DatArchive reopened = DatArchive.Open(path);
			Assert.NotNull(reopened.Find("ÿ\\café\\a.txt"));
			Assert.NotNull(reopened.Find("µ\\b.txt"));
			Assert.Equal("ABCÿ", DatArchive.ToUpperAscii("abcÿ"));
		}

		[Fact]
		public void TrailingBytes_AreAnErrorInStrictMode_AndRecoveredOtherwise()
		{
			String path = Build(DatFormat.Fallout2);
			Int64 cleanLength = new FileInfo(path).Length;
			File.AppendAllText(path, new String('x', 100));

			Assert.Equal(DatErrorKind.NotADat, Assert.Throws<DatException>(() => DatArchive.Open(path)).Kind);

			using (DatArchive archive = DatArchive.Open(path, DatOpenMode.Recover))
			{
				Assert.True(archive.IsRecovered);
				Assert.Equal(6, archive.Count);
				Assert.Contains(archive.Issues, issue => issue.Kind == DatIssueKind.TrailingData && issue.Value == 100);
				Assert.True(archive.PendingChanges.HasFlag(DatPendingChanges.Repaired));
				Assert.Equal(Compressible(2000), archive.ReadData(archive.Find("art\\file0.dat")!));

				// The quick save is not allowed to carry the damage along: the archive is written again.
				archive.Save(quick: true);
				Assert.Equal(DatPendingChanges.None, archive.PendingChanges);
				Assert.Empty(archive.Issues);
				Assert.NotNull(archive.BackupPath);
				Assert.Equal(cleanLength + 100, new FileInfo(archive.BackupPath!).Length);
			}

			using DatArchive repaired = DatArchive.Open(path);
			Assert.Equal(6, repaired.Count);
			Assert.Equal(cleanLength, new FileInfo(path).Length);
		}

		[Fact]
		public void Fallout2_Footer_IsFoundBeforeALargeTrailer()
		{
			String path = Build(DatFormat.Fallout2);
			using (FileStream stream = new(path, FileMode.Append))
			{
				stream.Write(new Byte[3 * 1024 * 1024 + 5]);
			}

			using DatArchive archive = DatArchive.Open(path, DatOpenMode.Recover);
			Assert.Equal(6, archive.Count);
			Assert.Contains(archive.Issues, issue => issue.Kind == DatIssueKind.TrailingData && issue.Value == 3 * 1024 * 1024 + 5);
		}

		[Fact]
		public void CutShortDirectory_IsReadAsFarAsItGoes()
		{
			String path = Build(DatFormat.Fallout2);

			// The third entry announces a name of 2 GB: the entries before it are still good.
			Patch(path, file =>
			{
				List<Int32> entries = EntryPositions(file);
				BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(entries[2]), Int32.MaxValue);
			});

			Assert.Equal(DatErrorKind.InvalidTree, Assert.Throws<DatException>(() => DatArchive.Open(path)).Kind);

			using DatArchive archive = DatArchive.Open(path, DatOpenMode.Recover);
			Assert.Equal(2, archive.Count);
			Assert.Contains(archive.Issues, issue => issue.Kind == DatIssueKind.TruncatedTree && issue.Value == 2);
			Assert.All(archive.Entries, entry => Assert.NotEmpty(archive.ReadData(entry)));
		}

		[Fact]
		public void FileOutsideOfTheArchive_IsFlagged_AndCanBeRemovedByTheRepair()
		{
			String path = Build(DatFormat.Fallout2);
			Int64 originalLength = new FileInfo(path).Length;

			Patch(path, file =>
			{
				List<Int32> entries = EntryPositions(file);
				Int32 offsetField = entries[1] + 4 + BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(entries[1])) + 1 + 8;
				BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(offsetField), 0xFFFFFF00);
			});

			using (DatArchive archive = DatArchive.Open(path))
			{
				Assert.Equal(1, archive.DamagedCount);
				DatEntry damaged = archive.Entries.Single(entry => entry.IsDamaged);
				Assert.Equal(DatIssueKind.EntryOutsideArchive, damaged.Problem);
				Assert.Equal(DatErrorKind.OutsideArchive, Assert.Throws<DatException>(() => archive.ReadData(damaged)).Kind);

				archive.Remove([archive.Entries.First(entry => !entry.IsDamaged)]);
				Assert.Equal(DatErrorKind.DamagedEntriesPresent, Assert.Throws<DatException>(() => archive.Save()).Kind);

				Assert.Equal(1, archive.RemoveDamaged());
				Assert.True(archive.RequiresRebuild);
				archive.Save();
				Assert.True(File.Exists(archive.BackupPath));
				Assert.Equal(originalLength, new FileInfo(archive.BackupPath!).Length);
			}

			using DatArchive repaired = DatArchive.Open(path);
			Assert.Equal(4, repaired.Count);
			Assert.Empty(repaired.Issues);
		}

		[Theory]
		[InlineData(DatFormat.Fallout2)]
		public void Verify_FindsFilesWhoseDataIsCorrupted(DatFormat format)
		{
			String path = Build(format);
			Int32 offset;
			using (DatArchive archive = DatArchive.Open(path))
			{
				DatEntry victim = archive.Find("art\\file0.dat")!;
				Assert.True(victim.IsCompressed);
				offset = (Int32)victim.Offset + victim.PackedSize / 2;
			}

			Patch(path, file =>
			{
				for (Int32 i = 0; i < 16; i++)
				{
					file[offset + i] ^= 0xA5;
				}
			});

			using DatArchive damaged = DatArchive.Open(path);
			Assert.Equal(0, damaged.DamagedCount);

			DatVerifyResult result = damaged.Verify();
			Assert.Equal(6, result.Checked);
			Assert.Equal("art\\file0.dat", Assert.Single(result.Damaged).FullPath);
			Assert.Contains(damaged.Issues, issue => issue.Kind == DatIssueKind.EntryCorrupt && issue.Path == "art\\file0.dat");

			// A second verification does not report the same file twice.
			Int32 issues = damaged.Issues.Count;
			damaged.Verify();
			Assert.Equal(issues, damaged.Issues.Count);
		}

		[Fact]
		public void Verify_OfAHealthyArchive_FindsNothing()
		{
			foreach (DatFormat format in new[] { DatFormat.Fallout1, DatFormat.Fallout2 })
			{
				String path = Build(format, format + ".dat");
				using DatArchive archive = DatArchive.Open(path);
				DatVerifyResult result = archive.Verify();
				Assert.Equal(6, result.Checked);
				Assert.Empty(result.Damaged);
			}
		}

		[Fact]
		public void ArchiveWithTwoFilesOfTheSamePath_IsReported()
		{
			String path = Build(DatFormat.Fallout2);

			// Give the second file the name of the first one (same length): a duplicate.
			Patch(path, file =>
			{
				List<Int32> entries = EntryPositions(file);
				Int32 length = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(entries[0]));
				file.AsSpan(entries[0] + 4, length).CopyTo(file.AsSpan(entries[1] + 4, length));
			});

			using DatArchive archive = DatArchive.Open(path);
			Assert.Contains(archive.Issues, issue => issue.Kind == DatIssueKind.DuplicateEntry);
			Assert.Equal(6, archive.Count);
		}

		[Fact]
		public void DamagedArchives_OnlyEverProduceDatExceptions()
		{
			Random random = new(20261008);

			foreach (DatFormat format in new[] { DatFormat.Fallout1, DatFormat.Fallout2 })
			{
				Byte[] original = File.ReadAllBytes(Build(format, format + "-original.dat"));
				String target = Path.Combine(_folder, "fuzz.dat");

				for (Int32 iteration = 0; iteration < 400; iteration++)
				{
					Byte[] bytes = (Byte[])original.Clone();
					for (Int32 mutation = random.Next(1, 6); mutation > 0; mutation--)
					{
						Int32 position = random.Next(3) switch
						{
							0 => random.Next(Math.Min(64, bytes.Length)),
							1 => bytes.Length - 1 - random.Next(Math.Min(700, bytes.Length)),
							_ => random.Next(bytes.Length),
						};
						bytes[position] = (Byte)random.Next(256);
					}

					if (random.Next(8) == 0)
					{
						Array.Resize(ref bytes, random.Next(bytes.Length));
					}

					File.WriteAllBytes(target, bytes);

					foreach (DatOpenMode mode in new[] { DatOpenMode.Strict, DatOpenMode.Recover })
					{
						try
						{
							using DatArchive archive = DatArchive.Open(target, mode);
							archive.Verify();
							archive.Extract(archive.Entries.ToList(), Path.Combine(_folder, "out"), String.Empty);
						}
						catch (DatException)
						{
							// The expected way to refuse a damaged file.
						}
					}
				}
			}
		}
	}

	internal static class RandomExtensions
	{
		public static Byte[] GetBytes(this Random random, Int32 length)
		{
			Byte[] data = new Byte[length];
			random.NextBytes(data);
			return data;
		}
	}
}