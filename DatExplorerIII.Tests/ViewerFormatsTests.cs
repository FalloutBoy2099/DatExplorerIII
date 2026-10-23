namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Threading;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>
	/// The readers of the Fallout file formats (pictures, palettes, sounds), the decoding of texts, and the choice of the viewer.
	/// </summary>
	public sealed class ViewerFormatsTests : IDisposable
	{
		private readonly String _folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		static ViewerFormatsTests()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		public ViewerFormatsTests()
		{
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		#region Choice of the viewer

		[Theory]
		[InlineData("scenery.lst", FileViewerKind.Text)]
		[InlineData("README.TXT", FileViewerKind.Text)]
		[InlineData("game.msg", FileViewerKind.Text)]
		[InlineData("hero.frm", FileViewerKind.Image)]
		[InlineData("hero.FR3", FileViewerKind.Image)]
		[InlineData("splash0.rix", FileViewerKind.Image)]
		[InlineData("color.pal", FileViewerKind.Image)]
		[InlineData("default.act", FileViewerKind.Image)]
		[InlineData("speech.acm", FileViewerKind.Audio)]
		[InlineData("effect.WAV", FileViewerKind.Audio)]
		[InlineData("intro.MVE", FileViewerKind.Video)]
		[InlineData("combat.gcd", FileViewerKind.Character)]
		[InlineData("combat.bio", FileViewerKind.Text)]
		[InlineData("00000008.PRO", FileViewerKind.Prototype)]
		[InlineData("artemple.MAP", FileViewerKind.Map)]
		public void ViewerKind_FollowsTheExtension(String name, FileViewerKind expected)
		{
			Assert.True(FileViewers.TryGetKind(name, out FileViewerKind kind));
			Assert.Equal(expected, kind);
			Assert.True(FileViewers.IsViewable(name));
		}

		[Theory]
		[InlineData("critter.bin")]
		[InlineData("script.obj")]
		[InlineData("noextension")]
		public void ViewerKind_IsUnknownForOtherFiles(String name)
		{
			Assert.False(FileViewers.IsViewable(name));
		}

		#endregion

		#region Text

		private static Byte[] Encode(String text, Int32 codePage)
		{
			return Encoding.GetEncoding(codePage).GetBytes(text);
		}

		[Theory]
		[InlineData("{100}{}{Où êtes-vous ? Café à côté, déjà fermé.}", 1252, "Windows-1252")]
		[InlineData("{0}{}{** Automatisch plündern: AUS **} Größe, Übung, schön", 1252, "Windows-1252")]
		[InlineData("{100}{}{Привет, как дела? Это проверка текста.}", 1251, "Windows-1251")]
		[InlineData("{100}{}{Jak sobie życzysz. Już się robi. Auto-łupienie gracza WYŁ. Zażółć gęślą jaźń.}", 1250, "Windows-1250")]
		public void Text_OfTheGameLanguages_IsDetected(String text, Int32 codePage, String expectedName)
		{
			DecodedText decoded = TextDecoder.Decode(Encode(text, codePage));
			Assert.Equal(expectedName, decoded.EncodingName);
			Assert.True(decoded.IsDetected);
			Assert.Equal(text, decoded.Text);
			Assert.False(decoded.LooksBinary);
		}

		[Fact]
		public void Text_WithoutAccents_IsAscii()
		{
			DecodedText decoded = TextDecoder.Decode(Encoding.ASCII.GetBytes("art\\critters\\hero.frm\r\nart\\items\\knife.frm\r\n"));
			Assert.Equal("ASCII", decoded.EncodingName);
		}

		[Fact]
		public void Text_OfUnicodeFiles_IsDetected()
		{
			String text = "Où êtes-vous ? — « Привет »";

			Assert.Equal("UTF-8", TextDecoder.Decode(new UTF8Encoding(false).GetBytes(text)).EncodingName);
			Assert.Equal(text, TextDecoder.Decode(new UTF8Encoding(false).GetBytes(text)).Text);

			// The byte order mark is not part of the text.
			Byte[] withBom = new Byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(text)).ToArray();
			DecodedText utf8 = TextDecoder.Decode(withBom);
			Assert.Equal("UTF-8", utf8.EncodingName);
			Assert.Equal(text, utf8.Text);

			Byte[] utf16 = new Byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(text)).ToArray();
			Assert.Equal(text, TextDecoder.Decode(utf16).Text);
			Assert.Equal("UTF-16 LE", TextDecoder.Decode(utf16).EncodingName);

			Byte[] utf16Big = new Byte[] { 0xFE, 0xFF }.Concat(Encoding.BigEndianUnicode.GetBytes(text)).ToArray();
			Assert.Equal(text, TextDecoder.Decode(utf16Big).Text);
			Assert.Equal("UTF-16 BE", TextDecoder.Decode(utf16Big).EncodingName);

			// Without a mark, plain western text in UTF-16 is recognized by its zero bytes.
			Byte[] noMark = Encoding.Unicode.GetBytes("{100}{}{Hello world, this is a test.}\r\n");
			Assert.Equal("{100}{}{Hello world, this is a test.}\r\n", TextDecoder.Decode(noMark).Text);
		}

		[Fact]
		public void Text_ReadWithAChosenEncoding_UsesIt()
		{
			Byte[] bytes = Encode("café", 1252);
			Assert.Equal("café", TextDecoder.Decode(bytes, 1252).Text);
			Assert.False(TextDecoder.Decode(bytes, 1252).IsDetected);
			Assert.Equal("cafΘ", TextDecoder.Decode(bytes, 437).Text);

			// Russian text read as Windows-1252 is the usual mix-up: choosing the encoding repairs it.
			Byte[] russian = Encode("Привет", 1251);
			Assert.NotEqual("Привет", TextDecoder.Decode(russian, 1252).Text);
			Assert.Equal("Привет", TextDecoder.Decode(russian, 1251).Text);
		}

		[Fact]
		public void Text_OfBinaryData_IsFlagged_AndShowsNoRawControlCharacters()
		{
			Byte[] binary = Enumerable.Range(0, 400).Select(i => (Byte)(i % 7 == 0 ? 0 : i % 31)).ToArray();
			DecodedText decoded = TextDecoder.Decode(binary, 1252);

			Assert.True(decoded.LooksBinary);
			Assert.DoesNotContain('\0', decoded.Text);
			Assert.Contains('␀', decoded.Text);
		}

		[Fact]
		public void Text_OfAnEmptyFile_IsEmpty()
		{
			DecodedText decoded = TextDecoder.Decode(Array.Empty<Byte>());
			Assert.Equal(String.Empty, decoded.Text);
			Assert.False(decoded.LooksBinary);
		}

		#endregion

		#region Palette, FRM, RIX

		[Fact]
		public void Palette_SixBitValues_AreShiftedLikeTheGameDoes()
		{
			Byte[] data = new Byte[768 + 100];
			data[3] = 63;
			data[4] = 32;
			data[5] = 0;

			Palette palette = PaletteReader.Read(data);
			Assert.Equal(252, palette.GetRed(1));
			Assert.Equal(128, palette.GetGreen(1));
			Assert.Equal(0, palette.GetBlue(1));
			Assert.Throws<InvalidDataException>(() => PaletteReader.Read(new Byte[767]));
		}

		[Fact]
		public void Palette_WithValuesBeyondSixBits_IsStillReadAsSixBits()
		{
			// The palette of the game holds 255 in the colors that are cycled; one of them must not turn the whole palette
			// into 8 bit colors (the pictures would be four times too dark).
			Byte[] data = new Byte[768];
			data[3] = 59;
			data[(255 * 3) + 1] = 255;

			Palette palette = PaletteReader.Read(data);
			Assert.Equal(236, palette.GetRed(1));
			Assert.Equal(252, palette.GetGreen(255));
		}

		[Fact]
		public void Palette_OfEightBitColors_IsKept()
		{
			Byte[] data = new Byte[768 + 4];
			data[3] = 200;
			Palette palette = PaletteReader.ReadRgb(data);
			Assert.Equal(200, palette.GetRed(1));
			Assert.Throws<InvalidDataException>(() => PaletteReader.ReadRgb(new Byte[700]));
		}

		/// <summary>Builds a FRM file: <paramref name="directions"/> directions of <paramref name="frames"/> frames of width x height.</summary>
		private static Byte[] BuildFrm(Int32 directions, Int32 frames, Int32 width, Int32 height)
		{
			using MemoryStream stream = new();
			Byte[] header = new Byte[62];
			BinaryPrimitives.WriteUInt32BigEndian(header, 4);
			BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 10);
			BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(6), 2);
			BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8), (UInt16)frames);

			Int32 frameSize = 12 + (width * height);
			for (Int32 d = 0; d < directions; d++)
			{
				BinaryPrimitives.WriteInt16BigEndian(header.AsSpan(10 + (d * 2)), (Int16)(d + 1));
				BinaryPrimitives.WriteInt16BigEndian(header.AsSpan(22 + (d * 2)), (Int16)(-(d + 1)));
				BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(34 + (d * 4)), (UInt32)(d * frames * frameSize));
			}

			BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(58), (UInt32)(directions * frames * frameSize));
			stream.Write(header);

			for (Int32 d = 0; d < directions; d++)
			{
				for (Int32 f = 0; f < frames; f++)
				{
					Byte[] frameHeader = new Byte[12];
					BinaryPrimitives.WriteUInt16BigEndian(frameHeader, (UInt16)width);
					BinaryPrimitives.WriteUInt16BigEndian(frameHeader.AsSpan(2), (UInt16)height);
					BinaryPrimitives.WriteUInt32BigEndian(frameHeader.AsSpan(4), (UInt32)(width * height));
					BinaryPrimitives.WriteInt16BigEndian(frameHeader.AsSpan(8), (Int16)f);
					BinaryPrimitives.WriteInt16BigEndian(frameHeader.AsSpan(10), (Int16)(-f));
					stream.Write(frameHeader);
					stream.Write(Enumerable.Range(0, width * height).Select(i => (Byte)((d * 40) + (f * 10) + (i % 7))).ToArray());
				}
			}

			return stream.ToArray();
		}

		[Fact]
		public void Frm_WithSixDirections_IsRead()
		{
			FrmImage image = FrmReader.Read(BuildFrm(6, 3, 5, 4));

			Assert.Equal(4, image.Version);
			Assert.Equal(10, image.FramesPerSecond);
			Assert.Equal(2, image.ActionFrame);
			Assert.Equal(3, image.FramesPerDirection);
			Assert.Equal(6, image.Directions.Count);
			Assert.Equal(3, image.Directions[2].ShiftX);
			Assert.Equal(-3, image.Directions[2].ShiftY);

			FrmFrame frame = image.Directions[4].Frames[2];
			Assert.Equal(5, frame.Width);
			Assert.Equal(4, frame.Height);
			Assert.Equal(2, frame.OffsetX);
			Assert.Equal(-2, frame.OffsetY);
			Assert.Equal(20, frame.Pixels.Length);
			Assert.Equal((Byte)((4 * 40) + 20), frame.Pixels[0]);
		}

		[Fact]
		public void Image_TheDirectionBox_ShowsTheFirstDirection_WhateverWasShownBefore()
		{
			RunOnUiThread(() =>
			{
				ImageViewerViewModel viewer = new(new ManualUiThread());
				System.Windows.Controls.ComboBox box = new();
				System.Windows.Controls.StackPanel panel = new() { DataContext = viewer, Children = { box } };
				panel.SetBinding(System.Windows.UIElement.VisibilityProperty, new System.Windows.Data.Binding("HasDirections") { Converter = new System.Windows.Controls.BooleanToVisibilityConverter() });
				box.SetBinding(System.Windows.Controls.ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding("Directions"));
				box.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new System.Windows.Data.Binding("SelectedDirectionNumber"));
				System.Windows.Window window = new() { Content = panel, ShowActivated = false, Left = -5000, Top = 0, Width = 300, Height = 200 };
				window.Show();

				void Pump()
				{
					for (Int32 i = 0; i < 4; i++)
					{
						window.UpdateLayout();
						window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
					}
				}

				PaletteChoice[] gray = [new PaletteChoice("Gray", Palette.Grayscale())];
				viewer.ShowAnimation(FrmReader.Read(BuildFrm(1, 3, 4, 4)), gray);
				Pump();
				viewer.ShowAnimation(FrmReader.Read(BuildFrm(6, 3, 4, 4)), gray);
				Pump();
				Assert.Equal(0, viewer.SelectedDirection);
				Assert.Equal(1, box.SelectedItem);

				viewer.SelectedDirection = 3;
				Pump();
				viewer.ShowAnimation(FrmReader.Read(BuildFrm(6, 3, 4, 4)), gray);
				Pump();
				Assert.Equal(1, box.SelectedItem);

				viewer.ShowAnimation(FrmReader.Read(BuildFrm(4, 3, 4, 4)), gray);
				Pump();
				Assert.Equal(1, box.SelectedItem);
				window.Close();
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Image_TheWheel_StepsTheMagnificationFrom1To16_WithoutFit()
		{
			ImageViewerViewModel viewer = new(new ManualUiThread()) { WheelNotchesSource = () => 1 };
			Assert.Equal(1, viewer.SelectedZoom.Factor);

			// Fit is not a step: the wheel stays at 1x going down.
			viewer.Wheel(-120);
			Assert.Equal(1, viewer.SelectedZoom.Factor);
			Assert.False(viewer.IsFit);

			viewer.Wheel(120);
			Assert.Equal(2, viewer.SelectedZoom.Factor);

			for (Int32 i = 0; i < 10; i++)
			{
				viewer.Wheel(120);
			}

			Assert.Equal(16, viewer.SelectedZoom.Factor);

			// From fit (chosen in the box), the wheel goes to 1x.
			viewer.SelectedZoom = viewer.ZoomOptions.First(option => option.Factor == 0);
			viewer.Wheel(120);
			Assert.Equal(1, viewer.SelectedZoom.Factor);
		}

		[Fact]
		public void Image_Animation_AdvancesOnTheTicksOfTheTimerService_AndStopsWhenPaused()
		{
			RunOnUiThread(() =>
			{
				ManualUiThread uiThread = new();
				ImageViewerViewModel viewer = new(uiThread);
				ManualTimer timer = Assert.Single(uiThread.Created);
				viewer.ShowAnimation(FrmReader.Read(BuildFrm(1, 3, 4, 4)), [new PaletteChoice("Gray", Palette.Grayscale())]);
				Assert.Equal(0, viewer.CurrentFrame);

				// A tick before the play button is pressed does nothing; then each tick shows the next frame, and the frames come round.
				timer.Raise();
				Assert.Equal(0, viewer.CurrentFrame);
				viewer.PlayCommand.Execute(null);
				Assert.True(viewer.IsPlaying);
				Assert.True(timer.IsRunning);
				Assert.Equal(TimeSpan.FromMilliseconds(100), timer.Interval);
				timer.Raise();
				Assert.Equal(1, viewer.CurrentFrame);
				timer.Raise();
				timer.Raise();
				Assert.Equal(0, viewer.CurrentFrame);

				viewer.PlayCommand.Execute(null);
				Assert.False(viewer.IsPlaying);
				Assert.False(timer.IsRunning);
				viewer.Dispose();
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Frm_WithOneDirection_HasOnlyOne()
		{
			FrmImage image = FrmReader.Read(BuildFrm(1, 1, 8, 8));
			Assert.Single(image.Directions);
			Assert.Single(image.Directions[0].Frames);
		}

		[Fact]
		public void Frm_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildFrm(2, 2, 6, 6);
			Assert.Throws<InvalidDataException>(() => FrmReader.Read(valid.AsSpan(0, 40).ToArray()));
			Assert.Throws<InvalidDataException>(() => FrmReader.Read(valid.AsSpan(0, valid.Length - 10).ToArray()));

			// No frame, or an absurd number of frames.
			Byte[] noFrames = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt16BigEndian(noFrames.AsSpan(8), 0);
			Assert.Throws<InvalidDataException>(() => FrmReader.Read(noFrames));
			Byte[] tooMany = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt16BigEndian(tooMany.AsSpan(8), 60000);
			Assert.Throws<InvalidDataException>(() => FrmReader.Read(tooMany));

			// A direction that starts outside of the file.
			Byte[] outside = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt32BigEndian(outside.AsSpan(38), 0x7FFFFFF0);
			Assert.Throws<InvalidDataException>(() => FrmReader.Read(outside));
		}

		private static Byte[] BuildRix(Int32 width, Int32 height)
		{
			Byte[] data = new Byte[778 + (width * height)];
			Encoding.ASCII.GetBytes("RIX3").CopyTo(data, 0);
			BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4), (UInt16)width);
			BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), (UInt16)height);
			data[8] = 0xAF;
			data[10 + 3] = 63;
			for (Int32 i = 0; i < width * height; i++)
			{
				data[778 + i] = (Byte)(i % 256);
			}

			return data;
		}

		[Fact]
		public void Rix_IsRead()
		{
			RixImage image = RixReader.Read(BuildRix(32, 20));
			Assert.Equal(32, image.Width);
			Assert.Equal(20, image.Height);
			Assert.Equal(640, image.Pixels.Length);
			Assert.Equal(252, image.Palette.GetRed(1));
			Assert.Equal(5, image.Pixels[5]);
		}

		[Fact]
		public void Rix_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildRix(32, 20);
			Assert.True(RixReader.IsRix(valid));
			Assert.False(RixReader.IsRix(new Byte[2000]));
			Assert.Throws<InvalidDataException>(() => RixReader.Read(valid.AsSpan(0, 900).ToArray()));
			Assert.Throws<InvalidDataException>(() => RixReader.Read(new Byte[2000]));

			Byte[] empty = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt16LittleEndian(empty.AsSpan(4), 0);
			Assert.Throws<InvalidDataException>(() => RixReader.Read(empty));
		}

		private static Byte[] BuildMask()
		{
			// A mask of 44 bytes a line: the first 8 pixels of the first line are set, and a bit after the end of the second line.
			Byte[] data = new Byte[13200];
			data[0] = 0xFF;
			data[44 + 43] = 0x01;
			return data;
		}

		[Fact]
		public void Msk_IsRead_BitByBit()
		{
			MaskImage mask = MskReader.Read(BuildMask());
			Assert.Equal(350, mask.Width);
			Assert.Equal(300, mask.Height);
			Assert.True(mask.Pixels[0]);
			Assert.True(mask.Pixels[7]);
			Assert.False(mask.Pixels[8]);

			// The last bit of a line is not a pixel (a line has 350 pixels in 352 bits): it does not show in the next line.
			Assert.False(mask.Pixels[350 + 349]);
			Assert.Equal(8, mask.SetCount);
		}

		[Fact]
		public void Msk_LastPixelOfALine_IsRead()
		{
			Byte[] data = new Byte[13200];
			data[43] = 0x04;
			MaskImage mask = MskReader.Read(data);
			Assert.True(mask.Pixels[349]);
			Assert.Equal(1, mask.SetCount);
		}

		[Fact]
		public void Msk_OfAnotherSize_IsRefused()
		{
			Assert.Throws<InvalidDataException>(() => MskReader.Read(new Byte[13199]));
			Assert.Throws<InvalidDataException>(() => MskReader.Read(Array.Empty<Byte>()));
		}

		[Fact]
		public void Msk_IsDrawnWhiteOnBlack()
		{
			PixelImage picture = ImageRenderer.Render(MskReader.Read(BuildMask()));
			Assert.Equal(350, picture.Width);
			Assert.Equal(255, picture.Pixels[0]);
			Assert.Equal(0, picture.Pixels[8 * 4]);
			Assert.Equal(255, picture.Pixels[3]);
		}

		[Fact]
		public void Msk_IsShownAsAPicture()
		{
			Assert.True(FileViewers.TryGetKind("wrldmp00.msk", out FileViewerKind kind));
			Assert.Equal(FileViewerKind.Image, kind);
		}

		#endregion

		#region Sound

		/// <summary>Writes bits the way the ACM files hold them: least significant first.</summary>
		private sealed class BitWriter
		{
			private readonly List<Byte> _bytes = new();
			private Int32 _bitCount;

			public void Write(Int32 value, Int32 bits)
			{
				for (Int32 i = 0; i < bits; i++)
				{
					if (_bitCount % 8 == 0)
					{
						_bytes.Add(0);
					}

					if (((value >> i) & 1) != 0)
					{
						_bytes[^1] |= (Byte)(1 << (_bitCount % 8));
					}

					_bitCount++;
				}
			}

			public Byte[] ToArray() => _bytes.ToArray();
		}

		/// <summary>A valid ACM sound of silence: one block of 2 columns by 4 rows, every column filled with zeros.</summary>
		private static Byte[] BuildSilentAcm(Int32 totalValues, Int32 channels, Int32 blocks = 1)
		{
			BitWriter writer = new();
			writer.Write(0x032897, 24);
			writer.Write(1, 8);
			writer.Write(totalValues & 0xFFFF, 16);
			writer.Write(totalValues >> 16, 16);
			writer.Write(channels, 16);
			writer.Write(22050, 16);
			writer.Write(1, 4);
			writer.Write(4, 12);

			for (Int32 block = 0; block < blocks; block++)
			{
				writer.Write(0, 4);
				writer.Write(0, 16);
				writer.Write(0, 5);
				writer.Write(0, 5);
			}

			return writer.ToArray();
		}

		[Fact]
		public void Acm_Silence_IsDecoded()
		{
			Byte[] file = BuildSilentAcm(8, 1);
			Assert.True(AcmReader.IsAcm(file));

			AudioClip clip = AcmReader.Read(file);

			Assert.Equal(22050, clip.SampleRate);
			Assert.Equal(1, clip.Channels);
			Assert.False(clip.IsIncomplete);
			Assert.Equal(44 + 16, clip.WavData.Length);
			Assert.Equal("RIFF", Encoding.ASCII.GetString(clip.WavData, 0, 4));
			Assert.Equal("WAVE", Encoding.ASCII.GetString(clip.WavData, 8, 4));
			Assert.Equal(8.0 / 22050, clip.Duration.TotalSeconds, 5);

			for (Int32 i = 44; i < clip.WavData.Length; i += 2)
			{
				Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(clip.WavData.AsSpan(i)), (Int16)(-2), (Int16)2);
			}
		}

		[Fact]
		public void Acm_StereoHeaderWithAnOddNumberOfValues_IsReadAsMono()
		{
			Byte[] file = BuildSilentAcm(7, 2);
			Assert.Equal(1, AcmReader.Read(file).Channels);

			// A choice made by the user wins over the guess.
			AudioClip forced = AcmReader.Read(file, 2);
			Assert.Equal(2, forced.Channels);
			Assert.Equal(6 * 2, forced.WavData.Length - 44);
			Assert.True(forced.IsIncomplete);

			Assert.Equal(2, AcmReader.Read(BuildSilentAcm(8, 2)).Channels);
		}

		[Fact]
		public void Acm_ThatIsCutShort_IsIncomplete_OrRefused()
		{
			// Two blocks announced, one present: the first one is played.
			Byte[] oneOfTwo = BuildSilentAcm(16, 1, 1);
			AudioClip clip = AcmReader.Read(oneOfTwo);
			Assert.True(clip.IsIncomplete);
			Assert.Equal(16, clip.WavData.Length - 44);

			// Nothing but the header: no sound at all.
			Assert.Throws<InvalidDataException>(() => AcmReader.Read(oneOfTwo.AsSpan(0, 14).ToArray()));
		}

		[Fact]
		public void Acm_ThatIsNotOne_IsRefused()
		{
			Assert.False(AcmReader.IsAcm(new Byte[40]));
			Assert.Throws<InvalidDataException>(() => AcmReader.Read(new Byte[40]));
			Assert.Throws<InvalidDataException>(() => AcmReader.Read(Encoding.ASCII.GetBytes("This is only a text file, not a sound.")));
			Assert.Throws<InvalidDataException>(() => AcmReader.Read(Array.Empty<Byte>()));

			// A sampling rate that no sound has.
			Byte[] badRate = BuildSilentAcm(8, 1);
			BinaryPrimitives.WriteUInt16LittleEndian(badRate.AsSpan(10), 100);
			Assert.Throws<InvalidDataException>(() => AcmReader.Read(badRate));
		}

		[Fact]
		public void Acm_RandomBytes_NeverCrashTheDecoder()
		{
			Random random = new(20261009);
			Byte[] valid = BuildSilentAcm(64, 1, 3);

			for (Int32 iteration = 0; iteration < 3000; iteration++)
			{
				Byte[] data = (Byte[])valid.Clone();
				for (Int32 mutation = random.Next(1, 6); mutation > 0; mutation--)
				{
					data[random.Next(data.Length)] = (Byte)random.Next(256);
				}

				try
				{
					AcmReader.Read(data);
				}
				catch (InvalidDataException)
				{
					// The expected way to refuse a damaged sound.
				}
			}
		}

		[Fact]
		public void Wav_IsReadForItsDescriptionAndShape()
		{
			Byte[] pcm = new Byte[44100 * 2];
			for (Int32 i = 0; i < pcm.Length / 2; i++)
			{
				BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (Int16)(i < 22050 ? 0 : 16000));
			}

			AudioClip clip = WavReader.Read(WavWriter.Create(pcm, 44100, 1));

			Assert.Equal(44100, clip.SampleRate);
			Assert.Equal(1, clip.Channels);
			Assert.Equal(1.0, clip.Duration.TotalSeconds, 3);
			Assert.Equal(1000, clip.Envelope.Length);
			Assert.Equal(0, clip.Envelope[100]);
			Assert.InRange(clip.Envelope[900], (Byte)120, (Byte)130);
			Assert.Throws<InvalidDataException>(() => WavReader.Read(new Byte[100]));
		}

		#endregion

		#region Viewer view model

		private DatArchive CreateArchive(params (String Directory, String Name, Byte[] Content)[] files)
		{
			DatArchive archive = DatArchive.Create(Path.Combine(_folder, "viewer.dat"), DatFormat.Fallout2);
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String source = Path.Combine(_folder, Guid.NewGuid().ToString("N"));
				File.WriteAllBytes(source, content);
				archive.AddFile(source, directory, name);
			}

			return archive;
		}

		[Fact]
		public async Task Viewer_ShowsATextOfTheArchive_AndReadsItAgainWithAnotherEncoding()
		{
			Byte[] polish = Encode("{0}{}{** Auto-łupienie gracza WYŁ. **}\r\n{100}{}{Zażółć gęślą jaźń.}", 1250);
			using DatArchive archive = CreateArchive(("text\\polish\\game", "p_party_orders.msg", polish));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("text\\polish\\game\\p_party_orders.msg")!);

			Assert.True(viewer.IsText);
			Assert.False(viewer.HasMessage);
			Assert.Contains("Zażółć gęślą jaźń.", viewer.Text.Text);

			viewer.Text.SelectedEncoding = viewer.Text.Options.First(option => option.CodePage == 1252);
			Assert.DoesNotContain("Zażółć", viewer.Text.Text);
		}

		[Fact]
		public async Task Viewer_CutsALargeText()
		{
			Byte[] big = Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("art\\items\\knife.frm\r\n", 300_000)));
			using DatArchive archive = CreateArchive(("art\\items", "items.lst", big));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\items\\items.lst")!);

			Assert.True(viewer.IsText);
			Assert.True(viewer.Text.Text.Length <= 4 * 1024 * 1024);
			Assert.NotEmpty(viewer.Text.Notice);
		}

		[Fact]
		public async Task Viewer_SaysSoWhenAFileCannotBeShown()
		{
			using DatArchive archive = CreateArchive(("proto", "critter.pro", new Byte[] { 1, 2, 3 }), ("art", "broken.frm", new Byte[] { 1, 2, 3, 4 }), ("data", "empty.txt", Array.Empty<Byte>()));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("proto\\critter.pro")!);
			Assert.True(viewer.HasMessage);
			Assert.False(viewer.IsText);

			await viewer.LoadAsync(archive, archive.Find("art\\broken.frm")!);
			Assert.True(viewer.HasMessage);
			Assert.False(viewer.IsImage);

			await viewer.LoadAsync(archive, archive.Find("data\\empty.txt")!);
			Assert.True(viewer.IsText);
			Assert.NotEmpty(viewer.Text.Notice);
		}

		[Fact]
		public async Task Viewer_ShowsAPictureWithThePaletteOfTheSameName()
		{
			Byte[] gray = new Byte[768];
			Byte[] red = new Byte[768];
			for (Int32 i = 0; i < 256; i++)
			{
				gray[i * 3] = gray[(i * 3) + 1] = gray[(i * 3) + 2] = (Byte)(i / 4);
				red[i * 3] = 63;
			}

			using DatArchive archive = CreateArchive(
				("art\\critters", "hero.frm", BuildFrm(1, 1, 4, 4)),
				("art\\critters", "hero.pal", red),
				("art\\critters", "other.frm", BuildFrm(1, 1, 4, 4)),
				(String.Empty, "color.pal", gray));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\critters\\hero.frm")!);
			Assert.True(viewer.IsImage);
			Assert.Equal(252, viewer.Image.SelectedPalette!.Palette.GetRed(1));
			Assert.Contains(viewer.Image.PaletteChoices, choice => choice.Display.Equals("color.pal", StringComparison.OrdinalIgnoreCase));

			// Without a palette of its name, the picture uses the palette of the game.
			await viewer.LoadAsync(archive, archive.Find("art\\critters\\other.frm")!);
			Assert.Equal(0, viewer.Image.SelectedPalette!.Palette.GetRed(1));
		}

		#region Ending images

		[Theory]
		[InlineData("art\\intrface\\", "eg_den01.frm", 640, 480, EndingKind.Standard)]
		[InlineData("ART\\INTRFACE", "eg_den01.frm", 640, 480, EndingKind.Standard)]
		[InlineData("art\\intrface\\", "dp.frm", 1500, 480, EndingKind.Panning)]
		[InlineData("art\\intrface\\", "x.frm", 640, 479, EndingKind.WrongSize)]
		[InlineData("art\\intrface\\", "x.frm", 639, 480, EndingKind.WrongSize)]
		[InlineData("art\\intrface\\", "x.frm", 640, 600, EndingKind.WrongSize)]
		[InlineData("art\\critters\\", "x.frm", 640, 480, EndingKind.WrongFolder)]
		[InlineData("", "x.frm", 640, 480, EndingKind.WrongFolder)]
		public void APicture_CanBeAnImageOfTheEnding_WhenItIsInTheInterfaceFolderAndHasTheSizeOfTheScreen(String directory, String name, Int32 width, Int32 height, EndingKind expected)
		{
			EndingImageCheck check = EndingImages.Check(directory, name, width, height);

			Assert.Equal(expected, check.Kind);
			Assert.Equal(expected is EndingKind.Standard or EndingKind.Panning, check.IsUsable);
			Assert.Equal(width, check.Width);
			Assert.Equal(height, check.Height);
		}

		[Fact]
		public void TheDeathScreen_HasAWiderText()
		{
			Assert.True(EndingImages.Check("art\\intrface\\", "death.frm", 640, 480).IsDeathScreen);
			Assert.Equal(EndingImages.DeathTextWidth, EndingImages.Check("art\\intrface\\", "DEATH.FRM", 640, 480).TextWidth);
			Assert.False(EndingImages.Check("art\\intrface\\", "eg_den01.frm", 640, 480).IsDeathScreen);
			Assert.Equal(EndingImages.SlideTextWidth, EndingImages.Check("art\\intrface\\", "eg_den01.frm", 640, 480).TextWidth);
		}

		[Fact]
		public void TheLinesOfTheSubtitles_LoseWhatComesBeforeTheFirstColon()
		{
			Assert.Equal(["War.", "War never changes: ever.", "No colon here"], EndingImages.Records("1:War.\r\n2:War never changes: ever.\r\n\r\nNo colon here\r\n"));
			Assert.Empty(EndingImages.Records(String.Empty));
			Assert.Equal("french", EndingImages.LanguageFolder("fr"));
			Assert.Equal("english", EndingImages.LanguageFolder("zh"));
		}

		private DatArchive CreateEndingArchive()
		{
			return CreateArchive(
				("text\\english\\cuts", "nar_1.txt", Encoding.ASCII.GetBytes("1:English one\r\n2:English two\r\n")),
				("text\\english\\cuts", "nar_dth1.txt", Encoding.ASCII.GetBytes("1:You died.\r\n2:Sadly.\r\n")),
				("text\\english\\cuts", "artimer1.txt", Encoding.ASCII.GetBytes("1:Not an ending")),
				("text\\french\\cuts", "nar_1.txt", Encoding.ASCII.GetBytes("1:Un francais\r\n")),
				("text\\english\\game", "nar_2.txt", Encoding.ASCII.GetBytes("1:Not in the folder of the cuts")));
		}

		[Fact]
		public void TheTextOfTheEndings_IsPickedInTheLanguage_AndASlideHasOneLineWhereTheDeathScreenHasAWholeFile()
		{
			using DatArchive archive = CreateEndingArchive();
			Random random = new(7);

			Assert.Contains(EndingImages.Pick([archive], "en", false, random), new[] { "English one", "English two" });
			Assert.Equal("Un francais", EndingImages.Pick([archive], "fr", false, random));

			// A language that the archives do not have: English.
			Assert.Contains(EndingImages.Pick([archive], "de", false, random), new[] { "English one", "English two" });
			Assert.Equal("You died. Sadly.", EndingImages.Pick([archive], "en", true, random));

			Assert.Single(EndingImages.TextFiles([archive], "en", false));
			Assert.Single(EndingImages.TextFiles([archive], "en", true));
		}

		[Fact]
		public void WithoutTextOfTheEndings_NothingIsPicked()
		{
			using DatArchive archive = CreateArchive(("art\\intrface", "x.frm", BuildFrm(1, 1, 4, 4)));

			Assert.Null(EndingImages.Pick([archive], "en", false, new Random(1)));
			Assert.Null(EndingImages.Pick([archive], "en", true, new Random(1)));
		}

		[Fact]
		public async Task Viewer_SaysWhetherAPictureWithAPaletteCanBeAnImageOfTheEnding_AndShowsATextOnIt()
		{
			Byte[] palette = new Byte[768];
			Byte[] slide = BuildFrm(1, 1, 640, 480);
			using DatArchive archive = CreateArchive(
				("art\\intrface", "death.frm", slide),
				("art\\intrface", "death.pal", palette),
				("art\\intrface", "eg_den01.frm", slide),
				("art\\intrface", "eg_den01.pal", palette),
				("art\\intrface", "small.frm", BuildFrm(1, 1, 4, 4)),
				("art\\intrface", "small.pal", palette),
				("art\\intrface", "nopal.frm", slide),
				("art\\critters", "elsewhere.frm", slide),
				("art\\critters", "elsewhere.pal", palette),
				("text\\english\\cuts", "nar_1.txt", Encoding.ASCII.GetBytes("1:The ending of the game")),
				("text\\english\\cuts", "nar_dth1.txt", Encoding.ASCII.GetBytes("1:The death\r\n2:of the player")));
			using ViewerViewModel viewer = new(new ManualUiThread());

			// A death screen: its text is a whole file of the death texts.
			await viewer.LoadAsync(archive, archive.Find("art\\intrface\\death.frm")!);
			Assert.True(viewer.Image.HasEnding);
			Assert.True(viewer.Image.IsEndingUsable);
			Assert.True(viewer.Image.Ending!.IsDeathScreen);
			Assert.False(viewer.Image.EndingTextVisible);
			viewer.Image.ShowEndingText = true;
			Assert.True(viewer.Image.EndingTextVisible);
			Assert.Equal("The death of the player", viewer.Image.EndingText);
			Assert.Equal(EndingImages.DeathTextWidth, viewer.Image.EndingTextWidth);
			Assert.Equal((EndingImages.ScreenWidth - EndingImages.DeathTextWidth) / 2.0, viewer.Image.EndingTextLeft);

			// A slide: one line of a text of the endings; the toggle stays on for the next picture.
			await viewer.LoadAsync(archive, archive.Find("art\\intrface\\eg_den01.frm")!);
			Assert.True(viewer.Image.IsEndingUsable);
			Assert.True(viewer.Image.EndingTextVisible);
			Assert.Equal("The ending of the game", viewer.Image.EndingText);
			Assert.Equal(EndingImages.SlideTextWidth, viewer.Image.EndingTextWidth);
			Assert.True(viewer.Image.NewEndingTextCommand.CanExecute(null));

			// Too small, in another folder: it says so, and there is no text over it.
			await viewer.LoadAsync(archive, archive.Find("art\\intrface\\small.frm")!);
			Assert.True(viewer.Image.HasEnding);
			Assert.False(viewer.Image.IsEndingUsable);
			Assert.Equal(EndingKind.WrongSize, viewer.Image.Ending!.Kind);
			Assert.False(viewer.Image.EndingTextVisible);
			Assert.False(viewer.Image.NewEndingTextCommand.CanExecute(null));
			await viewer.LoadAsync(archive, archive.Find("art\\critters\\elsewhere.frm")!);
			Assert.Equal(EndingKind.WrongFolder, viewer.Image.Ending!.Kind);

			// No palette of its own: nothing is said.
			await viewer.LoadAsync(archive, archive.Find("art\\intrface\\nopal.frm")!);
			Assert.False(viewer.Image.HasEnding);
			Assert.Equal(String.Empty, viewer.Image.EndingStatus);
		}

		[Fact]
		public async Task AnEndingImage_WithoutTextOfTheEndings_ShowsTheSample()
		{
			Byte[] palette = new Byte[768];
			using DatArchive archive = CreateArchive(("art\\intrface", "eg_den01.frm", BuildFrm(1, 1, 640, 480)), ("art\\intrface", "eg_den01.pal", palette));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\intrface\\eg_den01.frm")!);
			viewer.Image.ShowEndingText = true;

			Assert.Equal(EndingImages.SampleText, viewer.Image.EndingText);
		}

		#endregion

		#endregion

		#region Movies

		private static Byte[] Segment(Byte opcode, Byte version, params Byte[] body)
		{
			Byte[] bytes = new Byte[4 + body.Length];
			BinaryPrimitives.WriteUInt16LittleEndian(bytes, (UInt16)body.Length);
			bytes[2] = opcode;
			bytes[3] = version;
			body.CopyTo(bytes, 4);
			return bytes;
		}

		private static Byte[] Chunk(UInt16 type, params Byte[][] segments)
		{
			Byte[] body = segments.SelectMany(segment => segment).ToArray();
			Byte[] bytes = new Byte[4 + body.Length];
			BinaryPrimitives.WriteUInt16LittleEndian(bytes, (UInt16)body.Length);
			BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), type);
			body.CopyTo(bytes, 4);
			return bytes;
		}

		/// <summary>
		/// A movie of 16 by 8 pixels (two blocks), 20 pictures per second, with a palette, a sound of three samples and two
		/// pictures: a red block and a green block, then the red block copied from the previous picture and a blue block.
		/// </summary>
		private static Byte[] BuildMovie(Boolean withSecondPicture = true)
		{
			using MemoryStream stream = new();
			stream.Write(Encoding.ASCII.GetBytes("Interplay MVE File\u001A\0"));
			stream.Write([0x1A, 0x00, 0x00, 0x01, 0x33, 0x11]);

			stream.Write(Chunk(
				2,
				Segment(0x05, 2, 2, 0, 1, 0, 1, 0, 0, 0),
				Segment(0x02, 0, 0x50, 0xC3, 0, 0, 1, 0),
				Segment(0x03, 1, 0, 0, 6, 0, 0x22, 0x56, 0, 0, 0, 0)));

			Byte[] palette = [0, 0, 4, 0, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63];

			// Opcode 0xE fills a block with one color, 0xB gives its 64 pixels, 0x0 copies the block of the previous picture.
			stream.Write(Chunk(
				3,
				Segment(0x0C, 0, palette),
				Segment(0x0F, 0, 0xBE),
				Segment(0x11, 0, Enumerable.Repeat((Byte)0, 14).Append((Byte)1).Concat(Enumerable.Repeat((Byte)2, 64)).ToArray()),
				Segment(0x08, 0, 0, 0, 1, 0, 4, 0, 100, 0, 1, 1),
				Segment(0x07, 3, 0, 0, 0, 0, 0, 0)));

			if (withSecondPicture)
			{
				stream.Write(Chunk(
					3,
					Segment(0x0F, 0, 0xE0),
					Segment(0x11, 0, Enumerable.Repeat((Byte)0, 14).Append((Byte)3).ToArray()),
					Segment(0x07, 3, 0, 0, 0, 0, 0, 0)));
			}

			stream.Write(Chunk(4));
			return stream.ToArray();
		}

		private static UInt32 Pixel(Byte[] bgra, Int32 width, Int32 x, Int32 y)
		{
			Int32 offset = ((y * width) + x) * 4;
			return (UInt32)((bgra[offset + 2] << 16) | (bgra[offset + 1] << 8) | bgra[offset]);
		}

		[Fact]
		public void Mve_IsRead_WithItsSizeSpeedAndSound()
		{
			Byte[] file = BuildMovie();
			Assert.True(MveReader.IsMve(file));

			MveMovie movie = MveReader.Read(file);

			Assert.Equal(16, movie.Width);
			Assert.Equal(8, movie.Height);
			Assert.Equal(2, movie.FrameCount);
			Assert.Equal(50, movie.FramePeriod.TotalMilliseconds);
			Assert.Equal(20, movie.FramesPerSecond, 3);
			Assert.False(movie.IsIncomplete);

			Assert.NotNull(movie.Audio);
			Assert.Equal(22050, movie.Audio!.SampleRate);
			Assert.Equal(1, movie.Audio.Channels);

			// The first sample is given, the next ones are changes of 1.
			Assert.Equal(44 + 6, movie.Audio.WavData.Length);
			Assert.Equal(100, BinaryPrimitives.ReadInt16LittleEndian(movie.Audio.WavData.AsSpan(44)));
			Assert.Equal(101, BinaryPrimitives.ReadInt16LittleEndian(movie.Audio.WavData.AsSpan(46)));
			Assert.Equal(102, BinaryPrimitives.ReadInt16LittleEndian(movie.Audio.WavData.AsSpan(48)));
		}

		[Fact]
		public void Mve_PicturesAreDecoded_AndBlocksCanBeCopiedFromThePreviousPicture()
		{
			MveMovie movie = MveReader.Read(BuildMovie());
			MveFrameReader reader = movie.CreateFrameReader();
			Byte[] bgra = new Byte[reader.FrameBytes];

			Assert.True(reader.ReadFrame(bgra));
			Assert.Equal(0xFC0000u, Pixel(bgra, 16, 3, 3));
			Assert.Equal(0x00FC00u, Pixel(bgra, 16, 12, 5));
			Assert.Equal(0xFF, bgra[3]);

			Assert.True(reader.ReadFrame(bgra));
			Assert.Equal(0xFC0000u, Pixel(bgra, 16, 3, 3));
			Assert.Equal(0x0000FCu, Pixel(bgra, 16, 12, 5));

			Assert.False(reader.ReadFrame(bgra));

			// Starting again gives the same first picture.
			reader.Rewind();
			Assert.True(reader.ReadFrame(bgra));
			Assert.Equal(0x00FC00u, Pixel(bgra, 16, 12, 5));
		}

		[Fact]
		public void Mve_ThatEndsEarly_IsIncomplete_ButKeepsItsFirstPictures()
		{
			Byte[] file = BuildMovie();
			Byte[] cut = file.AsSpan(0, file.Length - 20).ToArray();

			MveMovie movie = MveReader.Read(cut);

			Assert.True(movie.IsIncomplete);
			Assert.Equal(1, movie.FrameCount);
		}

		[Fact]
		public void Mve_ThatIsDamaged_IsRefused()
		{
			Assert.False(MveReader.IsMve(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => MveReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => MveReader.Read(Encoding.ASCII.GetBytes("Interplay MVE File\u001A\0")));

			// A header that is fine and nothing else: no picture.
			Byte[] header = BuildMovie().AsSpan(0, 26).ToArray();
			Assert.Throws<InvalidDataException>(() => MveReader.Read(header));
		}

		[Fact]
		public void Mve_RandomBytes_NeverCrashTheDecoder()
		{
			Random random = new(20261010);
			Byte[] valid = BuildMovie();

			for (Int32 iteration = 0; iteration < 3000; iteration++)
			{
				Byte[] data = (Byte[])valid.Clone();
				for (Int32 mutation = random.Next(1, 6); mutation > 0; mutation--)
				{
					data[random.Next(26, data.Length)] = (Byte)random.Next(256);
				}

				try
				{
					MveMovie movie = MveReader.Read(data);
					MveFrameReader reader = movie.CreateFrameReader();
					Byte[] bgra = new Byte[reader.FrameBytes];
					while (reader.ReadFrame(bgra))
					{
					}
				}
				catch (InvalidDataException)
				{
					// The expected way to refuse a damaged movie.
				}
			}
		}

		/// <summary>
		/// A movie of many pictures: the left block takes a color that depends on the picture, the right block is drawn in
		/// the first picture and copied from the previous picture in the others (so a picture depends on all the ones before).
		/// </summary>
		private static Byte[] BuildLongMovie(Int32 pictures)
		{
			using MemoryStream stream = new();
			stream.Write(Encoding.ASCII.GetBytes("Interplay MVE File\u001A\0"));
			stream.Write([0x1A, 0x00, 0x00, 0x01, 0x33, 0x11]);
			stream.Write(Chunk(2, Segment(0x05, 2, 2, 0, 1, 0, 1, 0, 0, 0), Segment(0x02, 0, 0x50, 0xC3, 0, 0, 1, 0)));

			Byte[] palette = [0, 0, 4, 0, 0, 0, 0, 63, 0, 0, 0, 63, 0, 0, 0, 63];
			for (Int32 picture = 0; picture < pictures; picture++)
			{
				Byte color = (Byte)((picture % 3) + 1);
				Byte[] data = picture == 0
					? Enumerable.Repeat((Byte)0, 14).Append(color).Append((Byte)2).ToArray()
					: Enumerable.Repeat((Byte)0, 14).Append(color).ToArray();

				// The left block is filled (0xE); the right one is filled in the first picture, copied from the previous one later.
				Byte map = picture == 0 ? (Byte)0xEE : (Byte)0x0E;
				stream.Write(Chunk(
					3,
					picture == 0 ? Segment(0x0C, 0, palette) : Array.Empty<Byte>(),
					Segment(0x0F, 0, map),
					Segment(0x11, 0, data),
					Segment(0x07, 3, 0, 0, 0, 0, 0, 0)));
			}

			stream.Write(Chunk(4));
			return stream.ToArray();
		}

		[Fact]
		public void Mve_JumpingToAPicture_GivesTheSamePictureAsPlayingToIt()
		{
			MveMovie movie = MveReader.Read(BuildLongMovie(250));
			Assert.Equal(250, movie.FrameCount);

			// The pictures one after the other.
			MveFrameReader sequential = movie.CreateFrameReader();
			List<Byte[]> expected = new();
			Byte[] buffer = new Byte[sequential.FrameBytes];
			while (sequential.ReadFrame(buffer))
			{
				expected.Add((Byte[])buffer.Clone());
			}

			Assert.Equal(250, expected.Count);

			// Jumps in every direction, from a reader that has never gone through the pictures it jumps over.
			MveFrameReader jumper = MveReader.Read(BuildLongMovie(250)).CreateFrameReader();
			foreach (Int32 frame in new[] { 237, 0, 99, 100, 101, 249, 150, 5, 5, 199, 200, 64 })
			{
				jumper.SeekTo(frame);
				Assert.Equal(frame, jumper.FramePosition);
				Assert.True(jumper.ReadFrame(buffer));
				Assert.Equal(expected[frame], buffer);
			}

			Assert.Equal(0xFC0000u, Pixel(expected[237], 16, 3, 3));
			Assert.Equal(0x00FC00u, Pixel(expected[237], 16, 12, 5));
		}

		[Fact]
		public void Mve_SavedStates_MakeJumpsQuick_AndCanBeBuiltOnAnotherThread()
		{
			MveMovie movie = MveReader.Read(BuildLongMovie(450));
			movie.BuildCheckpoints(CancellationToken.None);

			// A cancelled build stops at once and keeps what it has.
			using CancellationTokenSource cancelled = new();
			cancelled.Cancel();
			MveReader.Read(BuildLongMovie(450)).BuildCheckpoints(cancelled.Token);

			MveFrameReader reader = movie.CreateFrameReader();
			Byte[] buffer = new Byte[reader.FrameBytes];
			reader.SeekTo(433);
			Assert.True(reader.ReadFrame(buffer));
			Assert.Equal(0x00FC00u, Pixel(buffer, 16, 12, 5));
			Assert.Equal(0x00FC00u, Pixel(buffer, 16, 3, 3));

			// Past the end: the last picture.
			reader.SeekTo(10_000);
			Assert.Equal(449, reader.FramePosition);
		}

		[Fact]
		public void Mve_AudioCanStartFromAMoment()
		{
			MveMovie movie = MveReader.Read(BuildMovie());
			Assert.Same(movie.Audio!.WavData, movie.CreateAudioFrom(TimeSpan.Zero));

			// Three samples: from the time of the second one, two remain.
			Byte[]? from = movie.CreateAudioFrom(TimeSpan.FromSeconds(1.0 / 22050));
			Assert.NotNull(from);
			Assert.Equal(44 + 4, from!.Length);
			Assert.Equal(101, BinaryPrimitives.ReadInt16LittleEndian(from.AsSpan(44)));
			Assert.Null(movie.CreateAudioFrom(TimeSpan.FromSeconds(1)));
		}

		/// <summary>Runs a test on a thread with a dispatcher, as the program does: a picture belongs to the thread that made it.</summary>
		private static void RunOnUiThread(Func<Task> body)
		{
			Exception? error = null;
			Thread thread = new(() =>
			{
				Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
				SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
				_ = dispatcher.InvokeAsync(async () =>
				{
					try
					{
						await body();
					}
					catch (Exception exception)
					{
						error = exception;
					}
					finally
					{
						dispatcher.InvokeShutdown();
					}
				});
				Dispatcher.Run();
			});
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();

			if (error != null)
			{
				ExceptionDispatchInfo.Capture(error).Throw();
			}
		}

		[Fact]
		public void Viewer_Movie_CanBeMovedInAndPaused()
		{
			RunOnUiThread(async () =>
			{
				using DatArchive archive = CreateArchive(("art\\cuts", "long.mve", BuildLongMovie(250)));
				using ViewerViewModel viewer = new(new ManualUiThread());
				await viewer.LoadAsync(archive, archive.Find("art\\cuts\\long.mve")!);

				Assert.True(viewer.IsVideo);
				Assert.Equal(249, viewer.Video.LastFrame);
				Assert.Equal(0, viewer.Video.Position);

				viewer.Video.Position = 237;
				Assert.Equal(237, viewer.Video.Position);
				Assert.Equal(0xFC0000u, Pixel(PixelsOf(viewer.Video), 16, 3, 3));

				viewer.Video.Position = 4;
				Assert.Equal(4, viewer.Video.Position);
				Assert.False(viewer.Video.IsPlaying);
				Assert.Contains("/", viewer.Video.PositionText);
			});
		}

		private static Byte[] PixelsOf(VideoViewerViewModel video)
		{
			return video.Frame!.Pixels.ToArray();
		}

		#endregion

		[Fact]
		public void Viewer_Movie_ShowsTheSubtitlesOfTheSveFileWithTheSameName_InTheLanguageOfTheInterface()
		{
			RunOnUiThread(async () =>
			{
				Byte[] english = Encoding.ASCII.GetBytes("10:First\r\n100:Second\r\n");
				Byte[] french = Encoding.ASCII.GetBytes("10:Premier\r\n");
				using DatArchive archive = CreateArchive(("art\\cuts", "long.mve", BuildLongMovie(250)), ("text\\french\\cuts", "long.sve", french), ("text\\english\\cuts", "long.sve", english));
				using ViewerViewModel viewer = new(new ManualUiThread());
				await viewer.LoadAsync(archive, archive.Find("art\\cuts\\long.mve")!);

				// Nothing is said before the first subtitle; then each one stays until the next.
				Assert.Equal(String.Empty, viewer.Video.Subtitle);
				Assert.False(viewer.Video.HasSubtitle);
				viewer.Video.Position = 10;
				Assert.Equal("First", viewer.Video.Subtitle);
				viewer.Video.Position = 99;
				Assert.Equal("First", viewer.Video.Subtitle);
				viewer.Video.Position = 100;
				Assert.Equal("Second", viewer.Video.Subtitle);
				viewer.Video.Position = 249;
				Assert.Equal("Second", viewer.Video.Subtitle);
				viewer.Video.Position = 5;
				Assert.Equal(String.Empty, viewer.Video.Subtitle);

				// The option turns them off, and on again, without loading the movie again.
				viewer.Video.Position = 120;
				viewer.ShowSubtitles = false;
				Assert.Equal(String.Empty, viewer.Video.Subtitle);
				viewer.ShowSubtitles = true;
				Assert.Equal("Second", viewer.Video.Subtitle);
			});
		}

		[Fact]
		public void Viewer_Movie_WithoutSubtitles_ShowsNone()
		{
			RunOnUiThread(async () =>
			{
				using DatArchive archive = CreateArchive(("art\\cuts", "long.mve", BuildLongMovie(250)), ("text\\english\\cuts", "other.sve", Encoding.ASCII.GetBytes("0:Not mine")));
				using ViewerViewModel viewer = new(new ManualUiThread());
				await viewer.LoadAsync(archive, archive.Find("art\\cuts\\long.mve")!);

				viewer.Video.Position = 100;
				Assert.False(viewer.Video.HasSubtitle);
			});
		}

		#region Characters and biographies

		private static Byte[] BuildCharacter()
		{
			Byte[] data = new Byte[GcdReader.Length];
			void Put(Int32 offset, Int32 value) => BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), value);

			Int32[] special = [8, 5, 9, 3, 4, 7, 4];
			for (Int32 i = 0; i < special.Length; i++)
			{
				Put(4 + (i * 4), special[i]);
			}

			Put(4 + (7 * 4), 44);
			Put(4 + (33 * 4), 21);
			Put(4 + (34 * 4), 1);
			Put(0x90 + (0 * 4), 2);
			Put(0x168, 1500);
			Encoding.ASCII.GetBytes("Narg").CopyTo(data, 0x174);

			Int32[] tagged = [4, 0, 5, -1];
			for (Int32 i = 0; i < tagged.Length; i++)
			{
				Put(0x194 + (i * 4), tagged[i]);
			}

			Put(0x1A4, 6);
			Put(0x1A8, 15);
			return data;
		}

		[Fact]
		public void Gcd_IsRead()
		{
			GcdCharacter character = GcdReader.Read(BuildCharacter());

			Assert.Equal("Narg", character.Name);
			Assert.Equal(8, character.BaseStats[(Int32)CharacterStat.Strength]);
			Assert.Equal(10, character.GetStat(CharacterStat.Strength));
			Assert.Equal(4, character.GetStat(CharacterStat.Luck));
			Assert.Equal(44, character.GetStat(CharacterStat.HitPoints));
			Assert.Equal(21, character.GetStat(CharacterStat.Age));
			Assert.Equal(1, character.GetStat(CharacterStat.Gender));
			Assert.Equal(1500, character.Experience);
			Assert.Equal([4, 0, 5, -1], character.TaggedSkills);
			Assert.Equal([6, 15], character.Traits);
		}

		[Fact]
		public void Gcd_ThatIsTooShort_IsRefused()
		{
			Assert.Throws<InvalidDataException>(() => GcdReader.Read(new Byte[GcdReader.Length - 1]));
			Assert.Throws<InvalidDataException>(() => GcdReader.Read(Array.Empty<Byte>()));
		}

		[Fact]
		public void Bio_IsReadAsText()
		{
			DecodedText bio = BioReader.Read(Encoding.ASCII.GetBytes("\r\nNARG'S STORY:\r\n\r\nNarg's exceptional\r\nphysique.\r\n"));
			Assert.Equal("ASCII", bio.EncodingName);
			Assert.Contains("NARG'S STORY:", bio.Text);

			// A biography in the language of the game: the same rules as any other text.
			Assert.Contains("Jak się masz", BioReader.Read(Encode("Jak się masz, łobuzie? Zażółć gęślą jaźń.", 1250)).Text);
		}

		[Fact]
		public async Task Viewer_ShowsACharacter_AndAMovie()
		{
			using DatArchive archive = CreateArchive(("premade", "combat.gcd", BuildCharacter()), ("art\\cuts", "test.mve", BuildMovie()));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("premade\\combat.gcd")!);
			Assert.True(viewer.IsCharacter);
			Assert.False(viewer.HasMessage);
			Assert.Equal(6, viewer.Character.Sections.Count);
			Assert.Contains(viewer.Character.Sections[0].Rows, row => row.Value == "Narg");

			await viewer.LoadAsync(archive, archive.Find("art\\cuts\\test.mve")!);
			Assert.True(viewer.IsVideo);
			Assert.False(viewer.IsCharacter);
			Assert.True(viewer.Video.HasMovie);
			Assert.Empty(viewer.Character.Sections);

			// A file that is not a movie is explained, not shown.
			using DatArchive broken = CreateArchive(("art\\cuts", "broken.mve", new Byte[] { 1, 2, 3 }));
			await viewer.LoadAsync(broken, broken.Find("art\\cuts\\broken.mve")!);
			Assert.True(viewer.HasMessage);
			Assert.False(viewer.IsVideo);
		}

		#endregion

		#region Prototypes

		/// <summary>Writes the numbers of a prototype file: big endian 32 bit numbers and a few single bytes.</summary>
		private sealed class ProBuilder
		{
			private readonly List<Byte> _bytes = new();

			public ProBuilder Int(params Int32[] values)
			{
				foreach (Int32 value in values)
				{
					Byte[] bytes = new Byte[4];
					BinaryPrimitives.WriteInt32BigEndian(bytes, value);
					_bytes.AddRange(bytes);
				}

				return this;
			}

			public ProBuilder Byte(Char value)
			{
				_bytes.Add((System.Byte)value);
				return this;
			}

			/// <summary>The numbers of every object, for an object of the given kind and number.</summary>
			public static ProBuilder Start(Int32 kind, Int32 id) => new ProBuilder().Int((kind << 24) | id, 100 + id, (kind << 24) | 7, 4, 3, 8);

			public Byte[] ToArray() => _bytes.ToArray();
		}

		private static ItemProto ReadItem(ItemType type, Func<ProBuilder, ProBuilder> data)
		{
			// The numbers of an item: extended flags, script, kind, material, size, weight, cost, inventory picture, sound letter.
			ProBuilder builder = ProBuilder.Start(0, 42).Int(0xA000, -1, (Int32)type, 1, 10, 35, 1100, 0x07000001).Byte('0');
			return (ItemProto)ProReader.Read(data(builder).ToArray());
		}

		[Fact]
		public void Pro_Armor_IsRead()
		{
			ItemProto item = ReadItem(ItemType.Armor, b => b.Int(10, 30, 75, 10, 20, 0, 500, 25, 4, 6, 4, 4, 0, 0, 4, -1, 0x1000010, 0x1000009));

			Assert.Equal(ProtoType.Item, item.Type);
			Assert.Equal(42, item.Id);
			Assert.Equal(142, item.MessageId);
			Assert.Equal(ItemType.Armor, item.ItemType);
			Assert.Equal(1, item.Material);
			Assert.Equal(35, item.Weight);
			Assert.Equal(1100, item.Cost);
			Assert.Equal('0', item.SoundId);
			Assert.Equal(0xA000, item.ExtendedFlags);
			Assert.Equal(-1, item.ScriptId);
			Assert.Equal(10, item.Armor!.ArmorClass);
			Assert.Equal([30, 75, 10, 20, 0, 500, 25], item.Armor.DamageResistance);
			Assert.Equal([4, 6, 4, 4, 0, 0, 4], item.Armor.DamageThreshold);
			Assert.Equal(-1, item.Armor.Perk);
			Assert.Equal(0x1000009, item.Armor.FemaleFid);
			Assert.Null(item.Weapon);
		}

		[Fact]
		public void Pro_Weapon_IsRead_AndEndsWithItsSoundLetter()
		{
			ItemProto item = ReadItem(ItemType.Weapon, b => b.Int(6, 5, 12, 0, 25, 20, -1, 4, 5, 6, 2, -1, 10, 8, 29, 30).Byte('D'));

			WeaponData weapon = item.Weapon!;
			Assert.Equal(5, weapon.MinDamage);
			Assert.Equal(12, weapon.MaxDamage);
			Assert.Equal(25, weapon.MaxRange1);
			Assert.Equal(5, weapon.ActionPointCost1);
			Assert.Equal(6, weapon.ActionPointCost2);
			Assert.Equal(29, weapon.AmmoPid);
			Assert.Equal(30, weapon.AmmoCapacity);
			Assert.Equal('D', weapon.SoundCode);
		}

		[Fact]
		public void Pro_OtherItems_AreRead()
		{
			ItemProto container = ReadItem(ItemType.Container, b => b.Int(500, 8));
			Assert.Equal(500, container.Container!.MaxSize);
			Assert.Equal(8, container.Container.OpenFlags);

			ItemProto drug = ReadItem(ItemType.Drug, b => b.Int(-2, 35, -1, 10, 20, 0, 5, 1, 2, 3, 6, 4, 5, 6, 30, 7, 60));
			Assert.Equal([-2, 35, -1], drug.Drug!.Stat);
			Assert.Equal([10, 20, 0], drug.Drug.Amount);
			Assert.Equal(5, drug.Drug.Duration1);
			Assert.Equal([1, 2, 3], drug.Drug.Amount1);
			Assert.Equal(6, drug.Drug.Duration2);
			Assert.Equal([4, 5, 6], drug.Drug.Amount2);
			Assert.Equal(30, drug.Drug.AddictionChance);
			Assert.Equal(7, drug.Drug.WithdrawalEffect);
			Assert.Equal(60, drug.Drug.WithdrawalOnset);

			ItemProto ammo = ReadItem(ItemType.Ammo, b => b.Int(8, 24, 0, -25, 1, 1));
			Assert.Equal(8, ammo.Ammo!.Caliber);
			Assert.Equal(-25, ammo.Ammo.DamageResistanceModifier);

			ItemProto misc = ReadItem(ItemType.Misc, b => b.Int(-1, 2, 3));
			Assert.Equal(3, misc.MiscData!.Charges);

			ItemProto key = ReadItem(ItemType.Key, b => b.Int(1234));
			Assert.Equal(1234, key.Key!.KeyCode);
		}

		private static Byte[] BuildCritter(Boolean withDamageType)
		{
			ProBuilder builder = ProBuilder.Start(1, 1).Int(0x6000, -1, -1, 1, 1, 2);
			builder.Int(5, 6, 7, 8, 9, 10, 4, 30, 7);
			builder.Int(Enumerable.Repeat(0, GcdCharacter.StatCount - 9).ToArray());
			builder.Int(Enumerable.Repeat(0, GcdCharacter.StatCount).ToArray());
			Int32[] skills = new Int32[GcdCharacter.SkillCount];
			skills[3] = 40;
			builder.Int(skills);
			builder.Int(1, 60, 10);
			if (withDamageType)
			{
				builder.Int(3);
			}

			return builder.ToArray();
		}

		[Fact]
		public void Pro_Critter_IsRead_EvenWhenTheLastNumberIsMissing()
		{
			Byte[] file = BuildCritter(true);
			Assert.Equal(416, file.Length);

			CritterProto critter = (CritterProto)ProReader.Read(file);
			Assert.Equal(ProtoType.Critter, critter.Type);
			Assert.Equal(1, critter.AiPacket);
			Assert.Equal(1, critter.Team);
			Assert.Equal(2, critter.CritterFlags);
			Assert.Equal(5, critter.GetStat(CharacterStat.Strength));
			Assert.Equal(4, critter.GetStat(CharacterStat.Luck));
			Assert.Equal(30, critter.GetStat(CharacterStat.HitPoints));
			Assert.Equal(40, critter.Skills[3]);
			Assert.Equal(1, critter.BodyType);
			Assert.Equal(60, critter.Experience);
			Assert.Equal(10, critter.KillType);
			Assert.Equal(3, critter.DamageType);

			// Some files of the game stop before the kind of damage.
			CritterProto shorter = (CritterProto)ProReader.Read(BuildCritter(false));
			Assert.Equal(10, shorter.KillType);
			Assert.Equal(0, shorter.DamageType);
		}

		[Fact]
		public void Pro_Scenery_Walls_Tiles_AndMiscObjects_AreRead()
		{
			SceneryProto door = (SceneryProto)ProReader.Read(ProBuilder.Start(2, 2).Int(0x2800, -1, 0, 3).Byte('D').Int(0, -858993460).ToArray());
			Assert.Equal(SceneryType.Door, door.SceneryType);
			Assert.Equal(3, door.Material);
			Assert.Equal('D', door.SoundId);
			Assert.Equal(-858993460, door.Door!.KeyCode);

			SceneryProto stairs = (SceneryProto)ProReader.Read(ProBuilder.Start(2, 69).Int(0x2800, -1, 1, 1).Byte('1').Int(0xFFFFFF, -1).ToArray());
			Assert.Equal(0xFFFFFF, stairs.Stairs!.DestinationTile);
			Assert.Equal(-1, stairs.Stairs.DestinationMap);

			SceneryProto elevator = (SceneryProto)ProReader.Read(ProBuilder.Start(2, 1294).Int(0x2800, -1, 2, 1).Byte('1').Int(3, 4).ToArray());
			Assert.Equal(3, elevator.Elevator!.ElevatorType);
			Assert.Equal(4, elevator.Elevator.Level);

			SceneryProto ladder = (SceneryProto)ProReader.Read(ProBuilder.Start(2, 140).Int(0x2800, -1, 3, 3).Byte('1').Int(0xFFFFFF).ToArray());
			Assert.Equal(0xFFFFFF, ladder.LadderDestination);
			Assert.Equal(45, ProBuilder.Start(2, 140).Int(0x2800, -1, 5, 1).Byte('0').Int(-858993460).ToArray().Length);

			WallProto wall = (WallProto)ProReader.Read(ProBuilder.Start(3, 1).Int(0x20000000, -1, 1).ToArray());
			Assert.Equal(ProtoType.Wall, wall.Type);
			Assert.Equal(1, wall.Material);
			Assert.Equal(0x20000000, wall.ExtendedFlags);

			TileProto tile = (TileProto)ProReader.Read(ProBuilder.Start(4, 1).Int(1).ToArray());
			Assert.Equal(ProtoType.Tile, tile.Type);
			Assert.Equal(1, tile.Material);

			MiscProto misc = (MiscProto)ProReader.Read(ProBuilder.Start(5, 1).Int(0x20000000).ToArray());
			Assert.Equal(ProtoType.Misc, misc.Type);
			Assert.Equal(1, misc.Id);
		}

		[Fact]
		public void Pro_ThatIsDamaged_IsRefused()
		{
			Assert.Throws<InvalidDataException>(() => ProReader.Read(Array.Empty<Byte>()));
			Assert.Throws<InvalidDataException>(() => ProReader.Read(new Byte[23]));

			// A kind of object that does not exist.
			Assert.Throws<InvalidDataException>(() => ProReader.Read(ProBuilder.Start(9, 1).Int(0, 0, 0).ToArray()));

			// An item of an unknown kind, and items that stop too early.
			Assert.Throws<InvalidDataException>(() => ProReader.Read(ProBuilder.Start(0, 1).Int(0, -1, 99, 1, 1, 1, 1, 1).Byte('0').ToArray()));
			Byte[] armor = ReadItemBytes(ItemType.Armor, b => b.Int(10, 30, 75, 10, 20, 0, 500, 25, 4, 6, 4, 4, 0, 0, 4, -1, 1, 2));
			Assert.Throws<InvalidDataException>(() => ProReader.Read(armor.AsSpan(0, armor.Length - 1).ToArray()));
			Assert.Throws<InvalidDataException>(() => ProReader.Read(BuildCritter(true).AsSpan(0, 300).ToArray()));
			Assert.Throws<InvalidDataException>(() => ProReader.Read(BuildCritter(false).AsSpan(0, 411).ToArray()));
		}

		private static Byte[] ReadItemBytes(ItemType type, Func<ProBuilder, ProBuilder> data)
		{
			return data(ProBuilder.Start(0, 42).Int(0xA000, -1, (Int32)type, 1, 10, 35, 1100, 0x07000001).Byte('0')).ToArray();
		}

		[Fact]
		public void Pro_RandomBytes_NeverCrashTheReader()
		{
			Random random = new(20261011);
			Byte[][] valid =
			[
				ReadItemBytes(ItemType.Weapon, b => b.Int(6, 5, 12, 0, 25, 20, -1, 4, 5, 6, 2, -1, 10, 8, 29, 30).Byte('D')),
				ReadItemBytes(ItemType.Drug, b => b.Int(-2, 35, -1, 10, 20, 0, 5, 1, 2, 3, 6, 4, 5, 6, 30, 7, 60)),
				BuildCritter(true),
				ProBuilder.Start(2, 2).Int(0x2800, -1, 0, 3).Byte('D').Int(0, -1).ToArray(),
			];

			for (Int32 iteration = 0; iteration < 4000; iteration++)
			{
				Byte[] data = (Byte[])valid[iteration % valid.Length].Clone();
				for (Int32 mutation = random.Next(1, 5); mutation > 0; mutation--)
				{
					data[random.Next(data.Length)] = (Byte)random.Next(256);
				}

				if (random.Next(4) == 0)
				{
					data = data.AsSpan(0, random.Next(data.Length)).ToArray();
				}

				try
				{
					ProReader.Read(data);
				}
				catch (InvalidDataException)
				{
					// The expected way to refuse a damaged prototype.
				}
			}
		}

		[Fact]
		public async Task Viewer_ShowsAPrototype_WithItsGeneralNumbersAndThoseOfItsKind()
		{
			Byte[] weapon = ReadItemBytes(ItemType.Weapon, b => b.Int(6, 5, 12, 0, 25, 20, -1, 4, 5, 6, 2, -1, 10, 8, 29, 30).Byte('D'));
			using DatArchive archive = CreateArchive(("proto\\items", "00000008.pro", weapon), ("proto\\critters", "00000001.pro", BuildCritter(true)), ("proto\\tiles", "00000001.pro", ProBuilder.Start(4, 1).Int(1).ToArray()));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("proto\\items\\00000008.pro")!);
			Assert.True(viewer.IsPrototype);
			Assert.Equal(2, viewer.Prototype.Sections.Count);
			Assert.Contains(viewer.Prototype.Sections[1].Rows, row => row.Value == "5 - 12");

			await viewer.LoadAsync(archive, archive.Find("proto\\critters\\00000001.pro")!);
			Assert.True(viewer.IsPrototype);
			Assert.True(viewer.Prototype.Sections.Count >= 5);

			await viewer.LoadAsync(archive, archive.Find("proto\\tiles\\00000001.pro")!);
			Assert.Single(viewer.Prototype.Sections);

			// The viewer of another kind of file lets go of it.
			using DatArchive other = CreateArchive(("data", "a.txt", Encoding.ASCII.GetBytes("text")));
			await viewer.LoadAsync(other, other.Find("data\\a.txt")!);
			Assert.False(viewer.IsPrototype);
			Assert.Empty(viewer.Prototype.Sections);
		}

		#endregion

		#region Real movies and characters of the game

		[Fact]
		public async Task Viewer_ShowsAPictureWithThePaletteOfTheProgram_WhenTheArchiveHasNone()
		{
			// No palette in the archive at all: the palette of the game that the program holds is used.
			using DatArchive archive = CreateArchive(("art\\critters", "hero.frm", BuildFrm(1, 1, 4, 4)));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("art\\critters\\hero.frm")!);

			Assert.True(viewer.IsImage);
			PaletteChoice chosen = viewer.Image.SelectedPalette!;
			Assert.Equal(236, chosen.Palette.GetRed(1));
			Assert.Equal(DefaultPalette.Palette.GetBlue(200), chosen.Palette.GetBlue(200));
			Assert.Equal(2, viewer.Image.PaletteChoices.Count);
		}

		[Fact]
		public void DefaultPalette_IsThePaletteOfTheGame()
		{
			Palette palette = DefaultPalette.Palette;

			// The colors of the interface of the game: 59 shifted to 8 bits, and the greens at the end of the table.
			Assert.Equal(236, palette.GetRed(1));
			Assert.Equal(220, palette.GetGreen(2));
			Assert.Equal(108, palette.GetGreen(200));
			Assert.Equal(0, palette.GetRed(200));
		}

		#endregion

	}
}