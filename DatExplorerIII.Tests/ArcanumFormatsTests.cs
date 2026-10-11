namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>
	/// The files of Arcanum (messages, dialogs, art) and the usual picture and sound files. The tests with the real files of the
	/// game run when the folder with its archives is given in DATEXPLORERIII_ARCANUM_DIR.
	/// </summary>
	public sealed class ArcanumFormatsTests : IDisposable
	{
		private readonly String _folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		static ArcanumFormatsTests()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		public ArcanumFormatsTests()
		{
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		private DatArchive CreateArchive(params (String Directory, String Name, Byte[] Content)[] files)
		{
			DatArchive archive = DatArchive.Create(Path.Combine(_folder, "arcanum.dat"), DatFormat.Arcanum);
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String source = Path.Combine(_folder, Guid.NewGuid().ToString("N"));
				File.WriteAllBytes(source, content);
				archive.AddFile(source, directory, name);
			}

			return archive;
		}

		#region Messages

		[Fact]
		public void Mes_IsRead_WithItsCommentsAndItsMultiLineTexts()
		{
			String text = "// ---- Arcanum ---\r\n// broadcast.mes\r\n{100}{partez}\r\n{101}{attendez}\r\n\r\n{5000}{The lever won't budge.}\r\n"
				+ "{7}{first line\r\nsecond line}\r\n{}{}\r\n{ 12 }{ with spaces }\r\n";

			MesFile file = MesReader.Parse(text, "test");

			Assert.Equal([100, 101, 5000, 7, 12], file.Entries.Select(entry => entry.Number));
			Assert.Equal("The lever won't budge.", file.Entries[2].Text);
			Assert.Equal("first line\r\nsecond line", file.Entries[3].Text);
			Assert.Equal(" with spaces ", file.Entries[4].Text);
			Assert.Equal(0, file.SkippedCount);
			Assert.Equal("test", file.EncodingName);
		}

		[Fact]
		public void Mes_SkipsWhatIsNotAMessage_WithoutLosingTheNextOnes()
		{
			MesFile file = MesReader.Parse("{1}{one}\n{two}{not a number}\n{3}{three}\n{4}");

			Assert.Equal([1, 3], file.Entries.Select(entry => entry.Number));
			Assert.True(file.SkippedCount > 0);
		}

		[Fact]
		public void Mes_InTheLanguageOfTheGame_IsDecoded()
		{
			Byte[] french = Encoding.GetEncoding(1252).GetBytes("// version française\r\n{1}{Où êtes-vous ? À côté déjà fermé.}\r\n");
			MesFile file = MesReader.Read(french);
			Assert.Equal("Où êtes-vous ? À côté déjà fermé.", file.Entries.Single().Text);
			Assert.Equal("Windows-1252", file.EncodingName);

			MesFile russian = MesReader.Read(Encoding.GetEncoding(1251).GetBytes("{1}{Привет, как дела? Это проверка.}"));
			Assert.Equal("Привет, как дела? Это проверка.", russian.Entries.Single().Text);
		}

		[Fact]
		public void Mes_OfAnEmptyFile_HasNoMessage()
		{
			Assert.Empty(MesReader.Read(Array.Empty<Byte>()).Entries);
			Assert.Empty(MesReader.Parse("// only a comment\r\n").Entries);
		}

		#endregion

		#region Dialogs

		[Fact]
		public void Dlg_IsRead_AsWhatCharactersSayAndAnswersOfThePlayer()
		{
			String text = "// ---- Arcanum ---\r\n"
				+ "{1}{What does a stranger want?}{Ah, a young lady!}{}{}{}{}\r\n"
				+ "{2}{How do you know?}{}{5}{}{6}{}\r\n"
				+ "{3}{Hello, King.}{}{5}{gf2282 1}{330}{re +10}\r\n"
				+ "{}{}{}{}{}{}{}\r\n"
				+ "{4}{Only for her.}{0}{-4}{}{9}{}\r\n";

			DlgFile file = DlgReader.Parse(text);

			Assert.Equal([1, 2, 3, 4], file.Entries.Select(entry => entry.Number));
			Assert.Equal(0, file.SkippedCount);

			DlgEntry npc = file.Entries[0];
			Assert.False(npc.IsPlayerOption);
			Assert.Equal("Ah, a young lady!", npc.Text2);

			DlgEntry option = file.Entries[2];
			Assert.True(option.IsPlayerOption);
			Assert.Equal("5", option.Iq);
			Assert.Equal("gf2282 1", option.Test);
			Assert.Equal("330", option.Goto);
			Assert.Equal("re +10", option.Effect);

			Assert.Equal("-4", file.Entries[3].Iq);
			Assert.Equal("0", file.Entries[3].Text2);
		}

		[Fact]
		public void Dlg_ADamagedLine_DoesNotSpoilTheNextOnes()
		{
			// The second line has a number missing: its values are skipped one by one until a line starts again.
			String text = "{1}{a}{}{}{}{}{}\r\n{x}{b}{}{}{}{}{}\r\n{3}{c}{}{}{}{}{}\r\n";

			DlgFile file = DlgReader.Parse(text);

			Assert.Equal([1, 3], file.Entries.Select(entry => entry.Number));
			Assert.True(file.SkippedCount >= 1);
		}

		#endregion

		#region Art

		/// <summary>Builds an art file: the header, the palettes (blue, green, red, zero), the table of pictures, the data.</summary>
		private static Byte[] BuildArt(UInt32 flags, Int32 palettes, Int32 frames, Int32 rotations, Func<Int32, Int32, (Int32 Width, Int32 Height, Byte[] Data)> picture)
		{
			using MemoryStream stream = new();
			UInt32[] header = new UInt32[33];
			header[0] = flags;
			header[1] = 12;
			header[2] = 8;
			for (Int32 p = 0; p < palettes; p++)
			{
				header[3 + p] = 1;
			}

			header[7] = 2;
			header[8] = (UInt32)frames;
			foreach (UInt32 value in header)
			{
				stream.Write(BitConverter.GetBytes(value));
			}

			for (Int32 p = 0; p < palettes; p++)
			{
				Byte[] palette = new Byte[1024];
				palette[(1 * 4) + 0] = (Byte)(10 + p);
				palette[(1 * 4) + 1] = (Byte)(20 + p);
				palette[(1 * 4) + 2] = (Byte)(30 + p);
				stream.Write(palette);
			}

			List<(Int32 Width, Int32 Height, Byte[] Data)> pictures = new();
			for (Int32 r = 0; r < rotations; r++)
			{
				for (Int32 f = 0; f < frames; f++)
				{
					pictures.Add(picture(r, f));
				}
			}

			foreach ((Int32 width, Int32 height, Byte[] data) in pictures)
			{
				stream.Write(BitConverter.GetBytes(width));
				stream.Write(BitConverter.GetBytes(height));
				stream.Write(BitConverter.GetBytes(data.Length));
				stream.Write(BitConverter.GetBytes(width / 2));
				stream.Write(BitConverter.GetBytes(height - 1));
				stream.Write(BitConverter.GetBytes(0));
				stream.Write(BitConverter.GetBytes(0));
			}

			foreach ((Int32 _, Int32 _, Byte[] data) in pictures)
			{
				stream.Write(data);
			}

			return stream.ToArray();
		}

		[Fact]
		public void Art_StaticPicture_RawAndRunLengthEncoded_IsRead()
		{
			// 4 x 3 pixels, as they are.
			Byte[] raw = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

			// The same 4 x 3 pixels, as runs: 0 repeated 5 times, then the literals 7 and 8, then 9 repeated 3 times... a run can
			// go from a row to the next.
			Byte[] rle = [0x05, 0x00, 0x82, 0x07, 0x08, 0x05, 0x09];

			ArtImage first = ArtReader.Read(BuildArt(1, 2, 1, 1, (_, _) => (4, 3, raw)));
			Assert.True(first.IsStatic);
			Assert.Equal(2, first.Palettes.Count);
			Assert.Single(first.Image.Directions);
			Assert.Equal(raw, first.Image.Directions[0].Frames[0].Pixels);
			Assert.Equal(4, first.Image.Directions[0].Frames[0].Width);
			Assert.Equal(3, first.Image.Directions[0].Frames[0].Height);

			// The colors are blue, green, red and a zero in the file.
			Assert.Equal(30, first.Palettes[0].GetRed(1));
			Assert.Equal(20, first.Palettes[0].GetGreen(1));
			Assert.Equal(10, first.Palettes[0].GetBlue(1));
			Assert.Equal(31, first.Palettes[1].GetRed(1));

			ArtImage second = ArtReader.Read(BuildArt(1, 1, 1, 1, (_, _) => (4, 3, rle)));
			Assert.Equal(new Byte[] { 0, 0, 0, 0, 0, 7, 8, 9, 9, 9, 9, 9 }, second.Image.Directions[0].Frames[0].Pixels);
		}

		[Fact]
		public void Art_Animation_HasEightRotationsOfEveryFrame()
		{
			ArtImage art = ArtReader.Read(BuildArt(0, 1, 3, 8, (r, f) => (2, 2, new Byte[] { (Byte)r, (Byte)f, 0, 1 })));

			Assert.False(art.IsStatic);
			Assert.Equal(8, art.Image.Directions.Count);
			Assert.Equal(3, art.Image.FramesPerDirection);
			Assert.Equal(12, art.Image.FramesPerSecond);
			Assert.Equal(2, art.Image.ActionFrame);
			Assert.Equal(new Byte[] { 5, 2, 0, 1 }, art.Image.Directions[5].Frames[2].Pixels);
		}

		[Fact]
		public void Art_ThatHoldsFiveRotations_IsRecognizedByItsSize()
		{
			ArtImage art = ArtReader.Read(BuildArt(2, 1, 2, 5, (r, f) => (2, 2, new Byte[] { (Byte)r, (Byte)f, 0, 1 })));

			Assert.Equal(5, art.Image.Directions.Count);
			Assert.Equal(new Byte[] { 4, 1, 0, 1 }, art.Image.Directions[4].Frames[1].Pixels);
		}

		[Fact]
		public void Art_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildArt(1, 1, 1, 1, (_, _) => (4, 3, new Byte[12]));
			Assert.True(ArtReader.IsArt(valid));

			Assert.Throws<InvalidDataException>(() => ArtReader.Read(valid.AsSpan(0, 200).ToArray()));
			Assert.Throws<InvalidDataException>(() => ArtReader.Read(valid.AsSpan(0, valid.Length - 4).ToArray()));
			Assert.Throws<InvalidDataException>(() => ArtReader.Read(new Byte[3000]));

			// A picture that is far too large.
			Byte[] huge = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt32LittleEndian(huge.AsSpan(0x84 + 0x400), 100_000);
			Assert.Throws<InvalidDataException>(() => ArtReader.Read(huge));
		}

		[Fact]
		public void Art_RandomBytes_NeverCrashTheReader()
		{
			Random random = new(20261012);
			Byte[][] valid =
			[
				BuildArt(1, 1, 1, 1, (_, _) => (6, 4, new Byte[] { 0x05, 0x00, 0x8A, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 0x03, 7 })),
				BuildArt(0, 2, 2, 8, (r, f) => (3, 3, new Byte[] { (Byte)r, (Byte)f, 0, 1, 2, 3, 4, 5, 6 })),
			];

			for (Int32 iteration = 0; iteration < 3000; iteration++)
			{
				Byte[] data = (Byte[])valid[iteration % valid.Length].Clone();
				for (Int32 mutation = random.Next(1, 8); mutation > 0; mutation--)
				{
					data[random.Next(data.Length)] = (Byte)random.Next(256);
				}

				try
				{
					ArtReader.Read(data);
				}
				catch (InvalidDataException)
				{
					// The expected way to refuse a damaged picture.
				}
			}
		}

		#endregion

		#region Pictures and sounds

		private static Byte[] EncodePicture(BitmapEncoder encoder)
		{
			Byte[] pixels = new Byte[16 * 8 * 4];
			for (Int32 i = 0; i < pixels.Length; i += 4)
			{
				pixels[i] = 200;
				pixels[i + 1] = 100;
				pixels[i + 2] = 50;
				pixels[i + 3] = 255;
			}

			encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(16, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4)));
			using MemoryStream stream = new();
			encoder.Save(stream);
			return stream.ToArray();
		}

		[Theory]
		[InlineData("png")]
		[InlineData("bmp")]
		[InlineData("jpg")]
		[InlineData("gif")]
		public void Pictures_OfTheUsualFormats_AreDecoded(String format)
		{
			BitmapEncoder encoder = format switch
			{
				"png" => new PngBitmapEncoder(),
				"bmp" => new BmpBitmapEncoder(),
				"jpg" => new JpegBitmapEncoder(),
				_ => new GifBitmapEncoder(),
			};

			BitmapSource bitmap = PictureDecoder.Decode(EncodePicture(encoder));

			Assert.Equal(16, bitmap.PixelWidth);
			Assert.Equal(8, bitmap.PixelHeight);
			Assert.True(bitmap.IsFrozen);
		}

		[Fact]
		public void Pictures_ThatAreNotPictures_AreRefused()
		{
			Assert.Throws<InvalidDataException>(() => PictureDecoder.Decode(Array.Empty<Byte>()));
			Assert.Throws<InvalidDataException>(() => PictureDecoder.Decode(Encoding.ASCII.GetBytes("This is a text, not a picture.")));
		}

		/// <summary>Frames of MPEG 1 layer III, 128 kb/s, 44.1 kHz, stereo, that hold nothing: silence.</summary>
		private static Byte[] BuildSilentMp3(Int32 frames, Int32 padding = 0)
		{
			Byte[] frame = new Byte[417];
			frame[0] = 0xFF;
			frame[1] = 0xFB;
			frame[2] = 0x90;
			frame[3] = 0x00;

			using MemoryStream stream = new();
			stream.Write(new Byte[padding]);
			for (Int32 i = 0; i < frames; i++)
			{
				stream.Write(frame);
			}

			return stream.ToArray();
		}

		[Fact]
		public void Mp3_IsDecodedToASound()
		{
			AudioClip clip = Mp3Reader.Read(BuildSilentMp3(40));

			Assert.Equal(44100, clip.SampleRate);
			Assert.Equal(2, clip.Channels);
			Assert.InRange(clip.Duration.TotalSeconds, 0.9, 1.1);
			Assert.Equal("RIFF", Encoding.ASCII.GetString(clip.WavData, 0, 4));
			Assert.False(clip.IsIncomplete);
		}

		[Fact]
		public void Mp3_ThatStartsWithPadding_IsStillAMp3()
		{
			Assert.True(Mp3Reader.IsMp3(BuildSilentMp3(3, 300)));
			Assert.True(Mp3Reader.IsMp3(Encoding.ASCII.GetBytes("ID3\u0003\0\0\0\0\0\0")));
			Assert.Equal(44100, Mp3Reader.Read(BuildSilentMp3(10, 200)).SampleRate);
		}

		[Fact]
		public void Mp3_ThatIsNotOne_IsRefused()
		{
			Assert.False(Mp3Reader.IsMp3(Array.Empty<Byte>()));
			Assert.False(Mp3Reader.IsMp3(new Byte[2000]));
			Assert.False(Mp3Reader.IsMp3(Encoding.ASCII.GetBytes("RIFF....WAVE this is a text")));
			Assert.Throws<InvalidDataException>(() => Mp3Reader.Read(new Byte[2000]));
			Assert.Throws<InvalidDataException>(() => Mp3Reader.Read(Encoding.ASCII.GetBytes("This is only a text file, not a sound.")));
		}

		[Fact]
		public void Mp3_RandomBytes_NeverCrashTheDecoder()
		{
			Random random = new(20261013);
			Byte[] valid = BuildSilentMp3(6);

			for (Int32 iteration = 0; iteration < 400; iteration++)
			{
				Byte[] data = (Byte[])valid.Clone();
				for (Int32 mutation = random.Next(1, 12); mutation > 0; mutation--)
				{
					data[random.Next(data.Length)] = (Byte)random.Next(256);
				}

				try
				{
					Mp3Reader.Read(data);
				}
				catch (InvalidDataException)
				{
					// The expected way to refuse a damaged sound.
				}
			}
		}

		#endregion

		#region Viewer

		[Theory]
		[InlineData("level.art", FileViewerKind.Image)]
		[InlineData("splash.PNG", FileViewerKind.Image)]
		[InlineData("portrait.bmp", FileViewerKind.Image)]
		[InlineData("photo.jpg", FileViewerKind.Image)]
		[InlineData("photo.JPEG", FileViewerKind.Image)]
		[InlineData("anim.gif", FileViewerKind.Image)]
		[InlineData("speech.mp3", FileViewerKind.Audio)]
		[InlineData("game.mes", FileViewerKind.Table)]
		[InlineData("01000praetor.DLG", FileViewerKind.Table)]
		public void ViewerKind_OfTheNewFiles(String name, FileViewerKind expected)
		{
			Assert.True(FileViewers.TryGetKind(name, out FileViewerKind kind));
			Assert.Equal(expected, kind);
		}

		[Fact]
		public async Task Viewer_ShowsMessagesAndDialogsAsTables()
		{
			Byte[] mes = Encoding.GetEncoding(1252).GetBytes("// c\r\n{1}{un}\r\n{2}{deux}\r\n{3}{trois}\r\n");
			Byte[] dlg = Encoding.GetEncoding(1252).GetBytes("{1}{Bonjour}{}{}{}{}{}\r\n{2}{Salut}{}{5}{}{1}{}\r\n");
			using DatArchive archive = CreateArchive(("mes", "game.mes", mes), ("dlg", "01.dlg", dlg));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("mes\\game.mes")!);
			Assert.True(viewer.IsTable);
			Assert.Equal(3, viewer.Table.Rows!.Count);
			Assert.Equal(2, viewer.Table.Rows.Table!.Columns.Count);
			Assert.Equal("deux", viewer.Table.Rows[1]["Text"]);

			await viewer.LoadAsync(archive, archive.Find("dlg\\01.dlg")!);
			Assert.True(viewer.IsTable);
			Assert.Equal(2, viewer.Table.Rows!.Count);
			Assert.Equal(8, viewer.Table.Rows.Table!.Columns.Count);
			Assert.Equal(viewer.Table.Rows[0]["Kind"], viewer.Table.Rows[0]["Kind"]);
			Assert.NotEqual(viewer.Table.Rows[0]["Kind"], viewer.Table.Rows[1]["Kind"]);

			await viewer.LoadAsync(archive, archive.Find("mes\\game.mes")!);
			Assert.False(viewer.IsPrototype);
		}

		[Fact]
		public async Task Viewer_ShowsArtAndPictureFiles()
		{
			Byte[] art = BuildArt(1, 2, 1, 1, (_, _) => (4, 3, new Byte[12]));
			Byte[] png = EncodePicture(new PngBitmapEncoder());
			using DatArchive archive = CreateArchive(("art", "level.art", art), ("pictures", "photo.png", png));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\level.art")!);
			Assert.True(viewer.IsImage);
			Assert.Equal(2, viewer.Image.PaletteChoices.Count);
			Assert.NotNull(viewer.Image.Bitmap);

			await viewer.LoadAsync(archive, archive.Find("pictures\\photo.png")!);
			Assert.True(viewer.IsImage);
			Assert.Equal(16, viewer.Image.Bitmap!.PixelWidth);
			Assert.Equal(8, viewer.Image.Bitmap.PixelHeight);
			Assert.Empty(viewer.Image.PaletteChoices);
		}

		[Fact]
		public async Task Viewer_PlaysAnMp3_AndExplainsABrokenOne()
		{
			using DatArchive archive = CreateArchive(("sound", "speech.mp3", BuildSilentMp3(40)), ("sound", "broken.mp3", new Byte[] { 1, 2, 3, 4 }));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("sound\\speech.mp3")!);
			Assert.True(viewer.IsAudio);
			Assert.True(viewer.Audio.HasClip);
			Assert.False(viewer.Audio.CanChooseChannels);

			await viewer.LoadAsync(archive, archive.Find("sound\\broken.mp3")!);
			Assert.False(viewer.Audio.HasClip);
			Assert.NotEmpty(viewer.Audio.Warning);
		}

		#endregion

		#region Real files of the game

		private static String? ArcanumFolder()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_ARCANUM_DIR");
			return folder != null && File.Exists(Path.Combine(folder, "ArcanumZPackFrench.dat")) ? folder : null;
		}

		[Fact]
		public void RealArcanum_MessagesDialogsAndPicturesAreRead()
		{
			String? folder = ArcanumFolder();
			if (folder == null)
			{
				return;
			}

			using DatArchive archive = DatArchive.Open(Path.Combine(folder, "ArcanumZPackFrench.dat"));

			List<DatEntry> dialogs = archive.Entries.Where(entry => entry.Name.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase)).ToList();
			Assert.True(dialogs.Count > 700);
			Int32 lines = 0;
			foreach (DatEntry entry in dialogs)
			{
				DlgFile file = DlgReader.Read(archive.ReadData(entry));
				lines += file.Entries.Count;
				Assert.True(file.Entries.Count > 0, entry.FullPath);
			}

			Assert.True(lines > 30_000);

			foreach (DatEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".mes", StringComparison.OrdinalIgnoreCase)))
			{
				MesFile file = MesReader.Read(archive.ReadData(entry));
				Assert.True(file.Entries.Count > 0, entry.FullPath);
			}

			foreach (DatEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".art", StringComparison.OrdinalIgnoreCase)))
			{
				ArtImage art = ArtReader.Read(archive.ReadData(entry));
				FrmFrame frame = art.Image.Directions[0].Frames[0];
				Assert.Equal(668, frame.Width);
				Assert.Equal(400, frame.Height);
			}

			foreach (DatEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)).Take(40))
			{
				Assert.True(PictureDecoder.Decode(archive.ReadData(entry)).PixelWidth > 0, entry.FullPath);
			}
		}

		[Fact]
		public void RealArcanum_ModulesAreReadAndVerified_AndTheirSoundsDecode()
		{
			String? folder = ArcanumFolder();
			String modules = folder == null ? String.Empty : Path.GetFullPath(Path.Combine(folder, "..", "_mods", "modules"));
			if (folder == null || !Directory.Exists(modules))
			{
				return;
			}

			foreach (String path in Directory.GetFiles(modules, "*.dat"))
			{
				using DatArchive archive = DatArchive.Open(path);
				Assert.Equal(DatFormat.Arcanum, archive.Format);
				Assert.Empty(archive.Issues);
				Assert.Empty(archive.Verify().Damaged);

				foreach (DatEntry entry in archive.Entries.Where(entry => entry.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).Take(6))
				{
					AudioClip clip = Mp3Reader.Read(archive.ReadData(entry));
					Assert.True(clip.Duration > TimeSpan.Zero, entry.FullPath);
				}
			}
		}

		#endregion
	}
}