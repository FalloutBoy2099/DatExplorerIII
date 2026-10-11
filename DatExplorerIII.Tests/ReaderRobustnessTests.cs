namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Services;

	/// <summary>
	/// Damaged files must never bring the program down: every reader answers a damaged file with an
	/// <see cref="InvalidDataException"/> (or reads what it can), never with another kind of error, and never takes forever.
	/// The files of the games are damaged on purpose (bytes changed, cut, sizes set to extreme values); they are read from the
	/// folders given in DATEXPLORERIII_FALLOUT_DIR and DATEXPLORERIII_ARCANUM_DIR, and nothing runs without them.
	/// </summary>
	public sealed class ReaderRobustnessTests
	{
		private const Int32 FilesPerExtension = 6;
		private const Int32 DamagedCopies = 25;

		private static Action<Byte[]>? ReaderOf(String extension)
		{
			return extension switch
			{
				".frm" => data => FrmReader.Read(data),
				".rix" => data => RixReader.Read(data),
				".pal" => data => PaletteReader.Read(data),
				".acm" => data => AcmReader.Read(data),
				".gcd" => data => GcdReader.Read(data),
				".pro" => data =>
				{
					if (ArcanumProReader.IsArcanumPro(data))
					{
						ArcanumProReader.Read(data);
					}
					else
					{
						ProReader.Read(data);
					}
				}
				,
				".map" => data => MapReader.Read(data),
				".msg" => data => TextDecoder.Decode(data),
				".bio" => data => BioReader.Read(data),
				".mes" => data => MesReader.Read(data),
				".dlg" => data => DlgReader.Read(data),
				".art" => data => ArtReader.Read(data),
				".mp3" => data => Mp3Reader.Read(data),
				".wav" => data => WavReader.Read(data),
				".mve" => data =>
				{
					MveMovie movie = MveReader.Read(data);
					MveFrameReader frames = movie.CreateFrameReader();
					Byte[] pixels = new Byte[frames.FrameBytes];
					for (Int32 index = 0; index < Math.Min(movie.FrameCount, 3); index++)
					{
						frames.ReadFrame(pixels);
					}
				}
				,
				_ => null,
			};
		}

		private static Byte[] Damage(Byte[] data, Random random)
		{
			Byte[] copy = (Byte[])data.Clone();
			switch (random.Next(5))
			{
				case 0:
					for (Int32 index = 0; index < random.Next(1, 9); index++)
					{
						copy[random.Next(copy.Length)] = (Byte)random.Next(256);
					}

					return copy;

				case 1:
					return copy[..random.Next(0, copy.Length)];

				case 2:
					if (copy.Length >= 8)
					{
						// A size or a count set to an extreme value, in either byte order.
						Int32 at = random.Next(0, copy.Length - 4);
						UInt32[] extremes = [0u, 0xFFFFFFFFu, 0x7FFFFFFFu, 0x80000000u, 0x00FFFFFFu, 0x10000u];
						BitConverter.GetBytes(extremes[random.Next(extremes.Length)]).CopyTo(copy, at);
						if (random.Next(2) == 0)
						{
							Array.Reverse(copy, at, 4);
						}
					}

					return copy;

				case 3:
					Int32 start = random.Next(0, copy.Length);
					Array.Clear(copy, start, Math.Min(random.Next(1, 64), copy.Length - start));
					return copy;

				default:
					// The start of a file is where its sizes are.
					for (Int32 index = 0; index < random.Next(1, 5); index++)
					{
						copy[random.Next(Math.Min(copy.Length, 64))] = (Byte)random.Next(256);
					}

					return copy;
			}
		}

		private static List<(String Path, Byte[] Data)> Samples(String archivePath, params String[] extensions)
		{
			List<(String, Byte[])> samples = new();
			using DatArchive archive = DatArchive.Open(archivePath);
			foreach (String extension in extensions)
			{
				foreach (Models.DatEntry entry in archive.Entries.Where(e => e.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && !e.IsDamaged && e.UnpackedSize is > 0 and < 2_000_000).Take(FilesPerExtension))
				{
					samples.Add((entry.FullPath, archive.ReadData(entry)));
				}
			}

			return samples;
		}

		private static void AssertRobust(List<(String Path, Byte[] Data)> samples)
		{
			Assert.NotEmpty(samples);
			Random random = new(2026);
			List<String> failures = new();

			foreach ((String path, Byte[] data) in samples)
			{
				Action<Byte[]>? read = ReaderOf(Path.GetExtension(path).ToLowerInvariant());
				if (read == null)
				{
					continue;
				}

				for (Int32 round = 0; round < DamagedCopies; round++)
				{
					Byte[] damaged = Damage(data, random);
					Exception? error = null;
					Task task = Task.Run(() =>
					{
						try
						{
							read(damaged);
						}
						catch (Exception exception)
						{
							error = exception;
						}
					});

					if (!task.Wait(TimeSpan.FromSeconds(20)))
					{
						failures.Add($"{path}: takes too long");
					}
					else if (error != null && error is not InvalidDataException)
					{
						failures.Add($"{path}: {error.GetType().Name}: {error.Message} ({error.StackTrace?.Split('\n').FirstOrDefault(line => line.Contains("DatExplorerIII", StringComparison.Ordinal))?.Trim()})");
					}
				}
			}

			Assert.Empty(failures.Distinct().Take(10));
		}

		[Fact]
		public void RealFallout_DamagedFilesAreRefusedWithAnInvalidDataException()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_FALLOUT_DIR");
			if (folder == null || !File.Exists(Path.Combine(folder, "master.dat")))
			{
				return;
			}

			AssertRobust(Samples(Path.Combine(folder, "master.dat"), ".frm", ".rix", ".pal", ".acm", ".gcd", ".pro", ".map", ".msg", ".bio", ".mve"));
		}

		[Fact]
		public void RealArcanum_DamagedFilesAreRefusedWithAnInvalidDataException()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_ARCANUM_DIR");
			String? archive = folder == null ? null : Path.Combine(folder, "ArcanumZPackFrench.dat");
			if (archive == null || !File.Exists(archive))
			{
				return;
			}

			AssertRobust(Samples(archive, ".mes", ".dlg", ".art", ".mp3", ".pro"));
		}
	}
}