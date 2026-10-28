namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using DatExplorerIII.Formats.GameFiles;
	using DatExplorerIII.Formats.Prototypes;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Models.Prototypes;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Texts;

	/// <summary>
	/// The small files of Arcanum: jump points (.jmp), index and data of a saved game (.tfai, .tfaf), information of a save (.gsi), fog of a
	/// town map (.tmf), and the objects of a map (.mob).
	/// </summary>
	public sealed class ArcanumSaveFormatsTests
	{
		private static void Add(List<Byte> bytes, Int32 value)
		{
			Byte[] buffer = new Byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
			bytes.AddRange(buffer);
		}

		private static void AddName(List<Byte> bytes, String name)
		{
			Add(bytes, name.Length);
			bytes.AddRange(Encoding.ASCII.GetBytes(name));
		}

		[Fact]
		public void Jmp_IsRead()
		{
			List<Byte> bytes = [];
			Add(bytes, 2);
			for (Int32 i = 0; i < 2; i++)
			{
				Add(bytes, i);
				Add(bytes, 0);
				Add(bytes, 100 + i);
				Add(bytes, 200 + i);
				Add(bytes, 7 + i);
				Add(bytes, 0);
				Add(bytes, 300 + i);
				Add(bytes, 400 + i);
			}

			JmpFile file = ArcanumSaveReaders.ReadJmp([.. bytes]);
			Assert.Equal(2, file.Jumps.Count);
			Assert.Equal(new JumpPoint(1, 101, 201, 8, 301, 401), file.Jumps[1]);
			Assert.Equal(3, ArcanumFileText.Render(".jmp", [.. bytes]).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
		}

		[Fact]
		public void Jmp_ThatIsDamaged_IsRefused()
		{
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadJmp([1, 2]));
			List<Byte> bytes = [];
			Add(bytes, 5);
			bytes.AddRange(new Byte[40]);
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadJmp([.. bytes]));
		}

		/// <summary>An index with a folder that holds a file, a file after it, then the end.</summary>
		private static Byte[] BuildIndex()
		{
			List<Byte> bytes = [];
			Add(bytes, 1);
			AddName(bytes, "maps");
			Add(bytes, 0);
			AddName(bytes, "map.jmp");
			Add(bytes, 36);
			Add(bytes, 2);
			Add(bytes, 0);
			AddName(bytes, "data.sav");
			Add(bytes, 10);
			Add(bytes, 3);
			return [.. bytes];
		}

		[Fact]
		public void Tfai_IsRead()
		{
			TfaiFile index = ArcanumSaveReaders.ReadTfai(BuildIndex());
			Assert.Equal(2, index.Root.Count);
			Assert.True(index.Root[0].IsFolder);
			Assert.Equal("maps", index.Root[0].Name);
			TfaiNode inside = Assert.Single(index.Root[0].Children);
			Assert.Equal(("map.jmp", 36), (inside.Name, inside.Size));
			Assert.Equal(10, index.Root[1].Size);

			String text = ArcanumFileText.Render(".tfai", BuildIndex());
			Assert.Contains("maps/", text);
			Assert.Contains("  map.jmp", text);
		}

		[Fact]
		public void Tfai_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildIndex();
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadTfai(valid[..^4]));
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadTfai([2, 0, 0, 0]));
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadTfai([9, 0, 0, 0]));
			List<Byte> open = [];
			Add(open, 1);
			AddName(open, "x");
			Add(open, 3);
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadTfai([.. open]));
		}

		[Fact]
		public void Tfaf_IsListedWithItsIndex()
		{
			Byte[] data = new Byte[46];
			String text = ArcanumFileText.Render(".tfaf", data, BuildIndex());

			// One line for each file of the index (the texts of the lines are those of the language, which tests do not load).
			Assert.Equal(2, text.Split("TfafLine").Length - 1);
			Assert.DoesNotContain("TfafTooShort", text);
			Assert.Contains("TfafTooShort", ArcanumFileText.Render(".tfaf", new Byte[10], BuildIndex()));
			Assert.Contains("TfafNoIndex", ArcanumFileText.Render(".tfaf", data));
		}

		[Fact]
		public void Gsi_IsRead()
		{
			List<Byte> bytes = [];
			Add(bytes, 25);
			AddName(bytes, "Arcanum");
			AddName(bytes, "Solomon Doone");
			Add(bytes, 3);
			Add(bytes, 12);
			Add(bytes, 3_600_000);
			Add(bytes, 1005);
			Add(bytes, 9);
			Add(bytes, 62093);
			Add(bytes, 65601);
			Add(bytes, 1);
			AddName(bytes, "Quick save");

			GsiFile file = ArcanumSaveReaders.ReadGsi([.. bytes]);
			Assert.Equal(25, file.Version);
			Assert.Equal("Arcanum", file.ModuleName);
			Assert.Equal("Solomon Doone", file.LeaderName);
			Assert.Equal("Quick save", file.DisplayName);
			Assert.Equal((3, 12, 3_600_000, 1005, 9), (file.MapId, file.GameTimeDays, file.GameTimeMs, file.LeaderPortrait, file.LeaderLevel));
			Assert.Equal((62093, 65601), (file.LeaderTileX, file.LeaderTileY));
			Assert.Contains("Quick save", ArcanumFileText.Render(".gsi", [.. bytes]));

			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadGsi(bytes.ToArray()[..40]));
			bytes[0] = 99;
			Assert.Throws<InvalidDataException>(() => ArcanumSaveReaders.ReadGsi([.. bytes]));
		}

		[Fact]
		public void Tmf_CountsTheDiscoveredTiles()
		{
			TmfFile fog = ArcanumSaveReaders.ReadTmf([0xFF, 0x0F, 0x00, 0x01]);
			Assert.Equal(32, fog.TotalTiles);
			Assert.Equal(13, fog.RevealedTiles);
			Assert.Contains("TmfSummary", ArcanumFileText.Render(".tmf", [0xFF, 0x0F, 0x00, 0x01]));
		}

		/// <summary>A scenery of a map with its location set: the header of a prototype, a count, a bitmap of 12 bytes, then the field.</summary>
		private static Byte[] BuildMob(Boolean withExtra)
		{
			List<Byte> bytes = [];
			Add(bytes, 0x77);
			bytes.AddRange(new Byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
			Add(bytes, 4242);
			bytes.AddRange(new Byte[12]);
			bytes.AddRange(new Byte[] { 2, 0, 0, 0, 0, 0, 0, 0 });
			bytes.AddRange(new Byte[16]);
			Add(bytes, (Int32)ArcanumObjectType.Scenery);
			bytes.AddRange(new Byte[] { 0, 0 });
			Byte[] bitmap = new Byte[12];
			bitmap[0] = 0x02;
			bytes.AddRange(bitmap);
			bytes.Add(1);
			bytes.AddRange(BitConverter.GetBytes(0x0000_0007_0000_0009L));
			if (withExtra)
				bytes.AddRange(new Byte[5]);

			return [.. bytes];
		}

		[Fact]
		public void Mob_ReadsTheFieldsOfTheBitmapOnly()
		{
			Byte[] data = BuildMob(false);
			Assert.True(ArcanumProReader.IsArcanumMob(data));
			Assert.False(ArcanumProReader.IsArcanumPro(data));

			ArcanumProto mob = ArcanumProReader.ReadMob(data);
			Assert.Equal(ArcanumObjectType.Scenery, mob.Type);
			Assert.Equal(4242, mob.Number);
			ArcanumProperty location = Assert.Single(mob.Properties);
			Assert.Equal("Location", location.Name);
			Assert.Equal((0x0000_0007_0000_0009L).ToString(), location.Value);
			Assert.Null(mob.Note);

			Assert.NotNull(ArcanumProReader.ReadMob(BuildMob(true)).Note);
			Assert.Throws<InvalidDataException>(() => ArcanumProReader.ReadMob(new Byte[100]));
		}

		[Theory]
		[InlineData("G_8E1D1951.mob", FileViewerKind.Prototype)]
		[InlineData("map.jmp", FileViewerKind.Text)]
		[InlineData("Slot0001.tfai", FileViewerKind.Text)]
		[InlineData("Slot0001.tfaf", FileViewerKind.Text)]
		[InlineData("Slot0001Quick.gsi", FileViewerKind.Text)]
		[InlineData("Ashbury.tmf", FileViewerKind.Text)]
		public void ViewerKind_OfTheFilesOfArcanum(String name, FileViewerKind expected)
		{
			Assert.True(FileViewers.TryGetKind(name, out FileViewerKind kind));
			Assert.Equal(expected, kind);

			// These are compared as the text they are shown as.
			if (expected == FileViewerKind.Text)
				Assert.False(FileViewers.IsPlainText(name));
		}
	}
}
