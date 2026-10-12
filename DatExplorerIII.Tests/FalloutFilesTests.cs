namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Data;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The fonts (.aaf, .fon), the lip synchronization files (.lip), the variables (.gam) and the subtitles (.sve) of Fallout.</summary>
	public sealed class FalloutFilesTests : IDisposable
	{
		private static Byte[] Ascii(String text) => Encoding.ASCII.GetBytes(text);

		public void Dispose()
		{
			foreach (String folder in _folders)
			{
				try
				{
					Directory.Delete(folder, true);
				}
				catch (IOException)
				{
				}
			}
		}

		#region AAF

		private static Byte[] BuildAaf()
		{
			Byte[] data = new Byte[0x80C + 4];
			Ascii("AAFF").CopyTo(data, 0);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), 9);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(6), 1);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(8), 4);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(10), 3);

			// "A": 2 x 2 pixels at the start of the pictures.
			Int32 entry = 0x0C + (65 * 8);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(entry), 2);
			BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(entry + 2), 2);
			BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(entry + 4), 0);
			new Byte[] { 1, 9, 0, 5 }.CopyTo(data, 0x80C);
			return data;
		}

		[Fact]
		public void Aaf_IsRead_WithItsMeasuresAndItsCharacters()
		{
			FontFile font = AafReader.Read(BuildAaf());

			Assert.Equal(FontFormat.Aaf, font.Format);
			Assert.Equal((9, 1, 4, 3), (font.Height, font.Spacing, font.SpaceWidth, font.LineSpacing));
			Assert.Equal(256, font.Glyphs.Count);
			Assert.Equal(2, font.Glyphs[65].Width);
			Assert.Equal(2, font.Glyphs[65].Height);
			Assert.Equal([1, 9, 0, 5], font.Glyphs[65].Pixels);
			Assert.Empty(font.Glyphs[66].Pixels);
		}

		[Fact]
		public void Aaf_WhatIsNotAFontIsRefused_WithoutAnyOtherError()
		{
			Byte[] data = BuildAaf();
			Assert.Throws<InvalidDataException>(() => AafReader.Read([1, 2, 3]));
			Byte[] wrong = (Byte[])data.Clone();
			wrong[0] = (Byte)'X';
			Assert.Throws<InvalidDataException>(() => AafReader.Read(wrong));

			// A picture that goes past the end of the file.
			Byte[] cut = data[..(data.Length - 2)];
			Assert.Throws<InvalidDataException>(() => AafReader.Read(cut));

			for (Int32 length = 0; length < data.Length; length += 97)
			{
				try
				{
					AafReader.Read(data[..length]);
				}
				catch (InvalidDataException)
				{
				}
			}
		}

		[Fact]
		public void Font_IsDrawnAsATableOfItsCharacters()
		{
			PixelImage sheet = ImageRenderer.Render(AafReader.Read(BuildAaf()));

			// 16 columns of cells (the widest character plus the margins), enlarged three times.
			Assert.Equal(((16 * 5) + 1) * 3, sheet.Width);
			Assert.Equal(((16 * 5) + 1) * 3, sheet.Height);

			Byte[] pixels = sheet.Pixels;

			// "A" is the second cell of the fifth row: its brightest pixel is lighter than its dimmest.
			Int32 left = (((65 % 16) * 5) + 2) * 3;
			Int32 top = (((65 / 16) * 5) + 2) * 3;
			Byte Gray(Int32 x, Int32 y) => pixels[(((top + (y * 3)) * sheet.Width) + left + (x * 3)) * 4];
			Assert.True(Gray(1, 0) > Gray(0, 0));
			Assert.True(Gray(0, 0) > Gray(0, 1) || Gray(0, 1) == 32);
		}

		#endregion

		#region FON

		private static Byte[] BuildFon(Boolean little)
		{
			Byte[] data = new Byte[20 + (2 * 8) + 4];
			void Put(Int32 offset, Int32 value)
			{
				if (little)
				{
					BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);
				}
				else
				{
					BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), value);
				}
			}

			Put(0, 2);
			Put(4, 2);
			Put(8, 1);
			Put(20, 8);
			Put(24, 0);
			Put(28, 3);
			Put(32, 2);
			new Byte[] { 0x81, 0x7E, 0xA0, 0x40 }.CopyTo(data, 20 + 16);
			return data;
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void Fon_IsRead_WhicheverWayItsNumbersAreWritten(Boolean little)
		{
			FontFile font = FonReader.Read(BuildFon(little));

			Assert.Equal(FontFormat.Fon, font.Format);
			Assert.Equal((2, 1), (font.Height, font.Spacing));
			Assert.Equal(2, font.Glyphs.Count);

			// The first character: 8 x 2, the most significant bit is the leftmost pixel.
			Assert.Equal([1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 1, 1, 0], font.Glyphs[0].Pixels);

			// The second: 3 x 2.
			Assert.Equal(3, font.Glyphs[1].Width);
			Assert.Equal([1, 0, 1, 0, 1, 0], font.Glyphs[1].Pixels);
		}

		[Fact]
		public void Fon_WhatIsNotAFontIsRefused_WithoutAnyOtherError()
		{
			Byte[] data = BuildFon(true);
			Assert.Throws<InvalidDataException>(() => FonReader.Read([1, 2, 3]));
			Assert.Throws<InvalidDataException>(() => FonReader.Read(new Byte[40]));
			Assert.Throws<InvalidDataException>(() => FonReader.Read(data[..(data.Length - 1)]));

			for (Int32 length = 0; length < data.Length; length++)
			{
				try
				{
					FonReader.Read(data[..length]);
				}
				catch (InvalidDataException)
				{
				}
			}
		}

		#endregion

		#region LIP

		private static Byte[] BuildLip()
		{
			Byte[] data = new Byte[0x2C + 3 + (4 * 8)];
			void Put(Int32 offset, Int32 value) => BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset), value);

			Put(0, 2);
			Put(4, 0x5800);
			Put(0x10, 1000);
			Put(0x14, 3);
			Put(0x1C, 4);
			Ascii("ABC").CopyTo(data, 0x20);
			Ascii("VOC").CopyTo(data, 0x28);
			new Byte[] { 0x05, 0x21, 0x00 }.CopyTo(data, 0x2C);
			Int32[] markers = [1, 0, 1, 190, 0, 400, 1, 1000];
			for (Int32 index = 0; index < markers.Length; index++)
			{
				Put(0x2C + 3 + (index * 4), markers[index]);
			}

			return data;
		}

		[Fact]
		public void Lip_IsRead_WithItsPhonemesAndMarkers()
		{
			LipFile lip = LipReader.Read(BuildLip());

			Assert.Equal(2, lip.Version);
			Assert.Equal(1000, lip.UnpackedLength);
			Assert.Equal("ABC", lip.AcmName);
			Assert.Equal([0x05, 0x21, 0x00], lip.Phonemes);
			Assert.Equal(4, lip.Markers.Count);
			Assert.Equal(new LipMarker(1, 0), lip.Markers[0]);
			Assert.Equal(new LipMarker(0, 400), lip.Markers[2]);
			Assert.Equal(new LipMarker(1, 1000), lip.Markers[3]);
		}

		[Fact]
		public void Lip_ThePhonemesOfTheGame_AreTheTableOfTheFormat()
		{
			Assert.Equal(0x2A, LipReader.Phonemes.Count);
			Assert.Equal(("æ", 1), LipReader.Phonemes[0x05]);
			Assert.Equal(("n", 2), LipReader.Phonemes[0x21]);
			Assert.Equal(("**", 8), LipReader.Phonemes[0x29]);
		}

		[Fact]
		public void Lip_WhatIsNotALipFileIsRefused_WithoutAnyOtherError()
		{
			Byte[] data = BuildLip();
			Assert.Throws<InvalidDataException>(() => LipReader.Read([1, 2, 3]));
			Assert.Throws<InvalidDataException>(() => LipReader.Read(data[..(data.Length - 1)]));

			for (Int32 length = 0; length < data.Length; length++)
			{
				try
				{
					LipReader.Read(data[..length]);
				}
				catch (InvalidDataException)
				{
				}
			}
		}

		[Fact]
		public async Task Lip_IsShownAsASheet_AndWrittenAsLinesToCompare()
		{
			Byte[] data = BuildLip();
			DatArchive archive = DatArchive.OpenFolder(Folder(("sound\\speech\\test", "abc.lip", data)));
			using (archive)
			using (ViewerViewModel viewer = new(new ManualUiThread()))
			{
				await viewer.LoadAsync(archive, archive.Find("sound\\speech\\test\\abc.lip")!);

				Assert.True(viewer.IsCharacter);
				Assert.False(viewer.HasMessage);
				Assert.Equal(3, viewer.Character.Sections.Count);
				Assert.Equal(3, viewer.Character.Sections[1].Rows.Count);
				Assert.Equal(4, viewer.Character.Sections[2].Rows.Count);
				Assert.Contains(viewer.Character.Sections[0].Rows, row => row.Value == "ABC");
			}

			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("abc.lip", data, null, out IReadOnlyList<String> lines));
			Assert.Contains(lines, line => line.StartsWith('['));
			Assert.True(lines.Count > 10);
		}

		#endregion

		#region GAM

		private const String GamText = "\r\n // Comments\r\n\r\n//MAP_GLOBAL_VARS:\r\nGAME_GLOBAL_VARS:\r\n//GLOBAL                                NUMBER\r\n\r\n"
			+ "CHILD_KILLER_SHADY        :=0;       //  (0)   // Has the player killed children in Shady Sands?\r\n"
			+ "NUM_RADSCORPIONS          :=9;       //  (1)   // How many are left\r\n"
			+ "PLAIN                     := -5 ;\r\n"
			+ "this is not a variable\r\n";

		[Fact]
		public void Gam_IsRead_WithTheVariablesInTheOrderOfTheirPlace()
		{
			GamFile gam = GamReader.Parse(GamText);

			Assert.Equal("GAME_GLOBAL_VARS", gam.Header);
			Assert.Equal(3, gam.Variables.Count);
			Assert.Equal(new GamVariable(0, "CHILD_KILLER_SHADY", "0", "Has the player killed children in Shady Sands?"), gam.Variables[0]);
			Assert.Equal(new GamVariable(1, "NUM_RADSCORPIONS", "9", "How many are left"), gam.Variables[1]);
			Assert.Equal(new GamVariable(2, "PLAIN", "-5", String.Empty), gam.Variables[2]);
			Assert.Equal(1, gam.SkippedCount);
		}

		[Fact]
		public void Gam_AVariableWithoutItsSemicolonIsAVariableAllTheSame()
		{
			// Some lines of the files of the game forget the semicolon; the places of the next variables depend on them.
			GamFile gam = GamReader.Parse("GAME_GLOBAL_VARS:\r\nA := 0 // (0)\r\nB := 1 ;  // (1)\r\nC := 2\r\n");

			Assert.Equal(["A", "B", "C"], gam.Variables.Select(variable => variable.Name));
			Assert.Equal(["0", "1", "2"], gam.Variables.Select(variable => variable.Value));
			Assert.Equal(0, gam.SkippedCount);
		}

		[Fact]
		public void Gam_AMapFileHasItsOwnHeader()
		{
			GamFile gam = GamReader.Parse("// Map vars\r\n\r\nMAP_GLOBAL_VARS:\r\n\r\nMVAR_Current_Gecko_Easy := 0;  // (1)\r\n");

			Assert.Equal("MAP_GLOBAL_VARS", gam.Header);
			Assert.Equal(new GamVariable(0, "MVAR_Current_Gecko_Easy", "0", String.Empty), Assert.Single(gam.Variables));
		}

		#endregion

		#region SVE

		[Fact]
		public void Sve_IsRead_InTheOrderOfThePictures()
		{
			SveFile sve = SveReader.Parse("70:The end of the world\r\n20:War. War never changes.\r\n\r\n125: Too many humans\r\nnot a subtitle\r\n");

			Assert.Equal([20, 70, 125], sve.Entries.Select(entry => entry.Frame));
			Assert.Equal("War. War never changes.", sve.Entries[0].Text);
			Assert.Equal("Too many humans", sve.Entries[2].Text);
			Assert.Equal(1, sve.SkippedCount);
		}

		[Fact]
		public void Sve_ATextMayHoldAColon()
		{
			SveFile sve = SveReader.Parse("5:Note: this is it");

			Assert.Equal("Note: this is it", Assert.Single(sve.Entries).Text);
		}

		#endregion

		#region Viewers

		[Theory]
		[InlineData("font0.aaf", FileViewerKind.Image)]
		[InlineData("FONT1.FON", FileViewerKind.Image)]
		[InlineData("ara_10.lip", FileViewerKind.Character)]
		[InlineData("vault13.gam", FileViewerKind.Table)]
		[InlineData("boil1.SVE", FileViewerKind.Table)]
		public void ViewerKind_OfTheNewFormats(String name, FileViewerKind expected)
		{
			Assert.True(FileViewers.TryGetKind(name, out FileViewerKind kind));
			Assert.Equal(expected, kind);
		}

		[Fact]
		public void TheVariablesAndTheSubtitles_AreComparedAsText()
		{
			Assert.True(FileViewers.IsPlainText("vault13.gam"));
			Assert.True(FileViewers.IsPlainText("boil1.sve"));
			Assert.True(FileViewers.IsPlainText("readme.txt"));
			Assert.False(FileViewers.IsPlainText("ara_10.lip"));
			Assert.False(FileViewers.IsPlainText("font0.aaf"));
			Assert.True(TextContentComparer.IsText("maps\\arcaves.gam"));

			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("a.gam", Encoding.ASCII.GetBytes(GamText), null, out IReadOnlyList<String> lines));
			Assert.Contains("GAME_GLOBAL_VARS:", lines);
		}

		[Fact]
		public async Task Table_ShowsTheVariablesAndTheSubtitles()
		{
			Byte[] gam = Encoding.ASCII.GetBytes(GamText);
			Byte[] sve = Encoding.ASCII.GetBytes("20:Hello\r\n70:Bye\r\n");
			DatArchive archive = DatArchive.OpenFolder(Folder(("data", "vault13.gam", gam), ("text\\english\\cuts", "boil1.sve", sve)));
			using (archive)
			using (ViewerViewModel viewer = new(new ManualUiThread()))
			{
				await viewer.LoadAsync(archive, archive.Find("data\\vault13.gam")!);
				Assert.True(viewer.IsTable);
				Assert.Equal(["Number", "Variable", "Value", "Comment"], viewer.Table.Rows!.Table!.Columns.Cast<DataColumn>().Select(column => column.ColumnName));
				Assert.Equal(3, viewer.Table.Rows.Count);

				await viewer.LoadAsync(archive, archive.Find("text\\english\\cuts\\boil1.sve")!);
				Assert.True(viewer.IsTable);
				Assert.Equal(["Frame", "Text"], viewer.Table.Rows!.Table!.Columns.Cast<DataColumn>().Select(column => column.ColumnName));
				Assert.Equal(2, viewer.Table.Rows.Count);
			}
		}

		[Fact]
		public async Task Font_IsShownAsAPicture_WithItsDescription()
		{
			DatArchive archive = DatArchive.OpenFolder(Folder(("data", "font0.aaf", BuildAaf())));
			using (archive)
			using (ViewerViewModel viewer = new(new ManualUiThread()))
			{
				await viewer.LoadAsync(archive, archive.Find("data\\font0.aaf")!);

				Assert.True(viewer.IsImage);
				Assert.False(viewer.HasMessage);
				Assert.NotNull(viewer.Image.Picture);
			}
		}

		#endregion

		#region Real files

		[Fact]
		public void RealGame_TheNewFormatsAreReadWithoutError()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_FALLOUT1_DIR");
			if (folder == null || !Directory.Exists(folder))
			{
				return;
			}

			Int32 count = 0;
			foreach (String path in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
			{
				String extension = Path.GetExtension(path).ToLowerInvariant();
				if (extension is not (".aaf" or ".fon" or ".lip" or ".gam" or ".sve"))
				{
					continue;
				}

				Byte[] data = File.ReadAllBytes(path);
				switch (extension)
				{
					case ".aaf":
						Assert.Equal(256, AafReader.Read(data).Glyphs.Count);
						break;
					case ".fon":
						Assert.NotEmpty(FonReader.Read(data).Glyphs);
						break;
					case ".lip":
						LipFile lip = LipReader.Read(data);
						Assert.Equal(lip.Phonemes.Count + 1, lip.Markers.Count);
						break;
					case ".gam":
						Assert.NotNull(GamReader.Read(data).Variables);
						break;
					default:
						Assert.NotNull(SveReader.Read(data).Entries);
						break;
				}

				count++;
			}

			Assert.True(count > 0);
		}

		#endregion

		#region Helpers

		private readonly List<String> _folders = new();

		/// <summary>A folder with the files, that is opened as an archive.</summary>
		private String Folder(params (String Directory, String Name, Byte[] Content)[] files)
		{
			String folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String full = Path.Combine(folder, directory, name);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllBytes(full, content);
			}

			_folders.Add(folder);
			return folder;
		}

		#endregion
	}
}