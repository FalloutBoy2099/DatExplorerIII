namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;
	using DatExplorerIII.Formats.GameFiles;
	using DatExplorerIII.Formats.Graphics;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.Models.Graphics;
	using DatExplorerIII.Services.Media;
	using DatExplorerIII.Services.Platform;

	/// <summary>
	/// The pictures (.zar), tiles (.til) and sprites (.spr) of Fallout Tactics.
	/// </summary>
	public sealed class TacticsFormatsTests
	{
		private static readonly Byte[] Runs =
		[
			(1 << 2) | 0, // one transparent pixel
			(2 << 2) | 1, 1, 2, // two opaque pixels: colors 1 and 2
			(1 << 2) | 2, 3, 128, // a pixel of color 3 with half of the opacity
			(1 << 2) | 3, 64, // a pixel of the default color with an alpha
		];

		private static void Add(List<Byte> bytes, UInt32 value)
		{
			Byte[] buffer = new Byte[4];
			BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
			bytes.AddRange(buffer);
		}

		private static void AddPalette(List<Byte> bytes, Int32 count)
		{
			Add(bytes, (UInt32)count);
			for (Int32 i = 0; i < count; i++)
			{
				// Blue, green, red, alpha.
				bytes.AddRange([(Byte)(i * 10), (Byte)((i * 10) + 1), (Byte)((i * 10) + 2), 255]);
			}
		}

		/// <summary>A picture of 5 by 1 pixels; its palette comes with the type 0x34 and a default color of 4.</summary>
		private static Byte[] BuildZar(Boolean withPalette = true, Byte type = 0x34, Byte[]? runs = null)
		{
			runs ??= Runs;
			List<Byte> bytes = [];
			bytes.AddRange(Encoding.ASCII.GetBytes("<zar>\0"));
			bytes.Add(type);
			bytes.Add(0);
			Add(bytes, 5);
			Add(bytes, 1);
			bytes.Add(withPalette ? (Byte)1 : (Byte)0);
			if (withPalette)
			{
				AddPalette(bytes, 8);
				if (type == 0x34)
					bytes.Add(4);
			}

			Add(bytes, (UInt32)runs.Length);
			bytes.AddRange(runs);
			return [.. bytes];
		}

		[Fact]
		public void Zar_IsDecoded()
		{
			ZarImage image = ZarReader.Read(BuildZar());
			Assert.Equal(5, image.Width);
			Assert.Equal(1, image.Height);

			// The transparent pixel stays clear.
			Assert.Equal(new Byte[] { 0, 0, 0, 0 }, image.Bgra[..4]);

			// Blue, green, red then the alpha: the palette is stored in this order, and an index gives an opaque pixel.
			Assert.Equal(new Byte[] { 10, 11, 12, 255 }, image.Bgra[4..8]);
			Assert.Equal(new Byte[] { 20, 21, 22, 255 }, image.Bgra[8..12]);

			// An index with an alpha.
			Assert.Equal(new Byte[] { 30, 31, 32, 128 }, image.Bgra[12..16]);

			// The default color (4) with an alpha.
			Assert.Equal(new Byte[] { 40, 41, 42, 64 }, image.Bgra[16..20]);
		}

		[Fact]
		public void Zar_OfTheTypeWithoutDefaultColor_UsesTheFirstColor()
		{
			ZarImage image = ZarReader.Read(BuildZar(type: 0x33));
			Assert.Equal(new Byte[] { 0, 1, 2, 64 }, image.Bgra[16..20]);
		}

		[Fact]
		public void Zar_WithoutPalette_IsShownInGray()
		{
			ZarImage image = ZarReader.Read(BuildZar(withPalette: false));
			Assert.Equal(new Byte[] { 1, 1, 1, 255 }, image.Bgra[4..8]);
		}

		[Fact]
		public void Zar_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildZar();
			Assert.True(ZarReader.IsZar(valid));
			Assert.False(ZarReader.IsZar(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(valid[..^3]));
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(valid[..10]));

			Byte[] tooBig = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt32LittleEndian(tooBig.AsSpan(8), 0x7FFFFFFF);
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(tooBig));

			Byte[] empty = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt32LittleEndian(empty.AsSpan(8), 0);
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(empty));

			Byte[] tooManyColors = (Byte[])valid.Clone();
			BinaryPrimitives.WriteUInt32LittleEndian(tooManyColors.AsSpan(17), 300);
			Assert.Throws<InvalidDataException>(() => ZarReader.Read(tooManyColors));
		}

		[Fact]
		public void Zar_WithRunsThatLeaveTheData_IsCutShort()
		{
			// A run that announces more indexes than the data holds, and a run of palette indexes outside of the palette.
			Byte[] cut = BuildZar(runs: [(30 << 2) | 1, 1, 2]);
			ZarImage image = ZarReader.Read(cut);
			Assert.Equal(255, image.Bgra[3]);

			Byte[] outside = BuildZar(runs: [(1 << 2) | 1, 200]);
			Assert.Equal(0, ZarReader.Read(outside).Bgra[3]);
		}

		private static Byte[] BuildTil(Int32 frames)
		{
			List<Byte> bytes = [];
			bytes.AddRange(Encoding.ASCII.GetBytes("<tile>\0"));
			bytes.AddRange(Encoding.ASCII.GetBytes("7\0"));
			bytes.AddRange(new Byte[11]);
			Add(bytes, 5);
			Add(bytes, 1);
			bytes.AddRange(Encoding.ASCII.GetBytes("<tiledata>"));
			bytes.AddRange(new Byte[3]);
			Add(bytes, (UInt32)frames);
			for (Int32 i = 0; i < frames; i++)
			{
				bytes.AddRange(BuildZar());
				bytes.AddRange(new Byte[8]);
			}

			return [.. bytes];
		}

		[Fact]
		public void Til_IsRead()
		{
			TilImage tile = TilReader.Read(BuildTil(3));
			Assert.Equal(5, tile.Width);
			Assert.Equal(1, tile.Height);
			Assert.Equal(3, tile.Frames.Count);
			Assert.Equal(5, tile.Frames[2].Width);
			Assert.Equal(255, tile.Frames[2].Bgra[7]);
		}

		[Fact]
		public void Til_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildTil(2);
			Assert.True(TilReader.IsTil(valid));
			Assert.False(TilReader.IsTil(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => TilReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => TilReader.Read(valid[..40]));
			Assert.Throws<InvalidDataException>(() => TilReader.Read(valid[..^40]));
		}

		/// <summary>
		/// A sprite with one animation of 2 frames and 2 directions. The first layer of each picture is a pixel of the color
		/// (frame * 2 + direction + 1) of the palette; the other layers are empty.
		/// </summary>
		private static Byte[] BuildSprite(Char version)
		{
			const Int32 frames = 2;
			const Int32 directions = 2;
			List<Byte> payload = [];
			for (Int32 i = 0; i < 4; i++)
			{
				AddPalette(payload, 256);
			}

			List<Byte> items = [];
			for (Int32 image = 0; image < frames * directions * 4; image++)
			{
				// 'Old' pictures come picture by picture (frame, direction), the others layer by layer (direction, frame).
				Boolean firstLayer = version == '1' ? image % 4 == 0 : image < frames * directions;
				if (!firstLayer)
				{
					items.Add(0);
					continue;
				}

				Int32 frame, direction;
				if (version == '1')
				{
					frame = (image / 4) / directions;
					direction = (image / 4) % directions;
				}
				else
				{
					direction = image / frames;
					frame = image % frames;
				}

				items.Add(1);
				Add(items, 0);
				Add(items, 0);
				items.AddRange(Encoding.ASCII.GetBytes("<zar>\0"));
				items.AddRange(new Byte[] { (Byte)'4', 0 });
				Add(items, 5);
				Add(items, 1);
				items.Add(0);
				Add(items, 2);
				items.AddRange(new Byte[] { (1 << 2) | 1, (Byte)((frame * 2) + direction + 1) });
			}

			payload.AddRange(items);
			List<Byte> block = [];
			block.AddRange(Encoding.ASCII.GetBytes("<spranim_img>\0"));
			block.AddRange(new Byte[] { (Byte)version, 0 });
			if (version == '2')
			{
				Add(block, (UInt32)payload.Count);
				using MemoryStream compressed = new();
				using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, true))
				{
					zlib.Write([.. payload]);
				}

				block.AddRange(compressed.ToArray());
			}
			else
			{
				block.AddRange(payload);
			}

			List<Byte> file = [];
			file.AddRange(Encoding.ASCII.GetBytes("<sprite>\0"));
			file.AddRange(new Byte[] { (Byte)'4', 0, 1, 1, 1 });
			Add(file, 0);
			Add(file, 0);
			file.AddRange(new Byte[3]);
			Add(file, 2);

			// "Walk" plays the frames 1 and 0, and has a frame without picture; "Empty" has no frame to show.
			file.AddRange(new Byte[] { 3, 0, 0, 0 });
			file.AddRange(BitConverter.GetBytes((Int16)1));
			file.AddRange(BitConverter.GetBytes((Int16)(-1)));
			file.AddRange(BitConverter.GetBytes((Int16)0));
			file.AddRange(new Byte[12]);
			AddString(file, "Walk");
			file.AddRange(BitConverter.GetBytes((Int16)0));
			file.AddRange(new Byte[] { 1, 0, 0, 0 });
			file.AddRange(BitConverter.GetBytes((Int16)(-1)));
			file.AddRange(new Byte[4]);
			AddString(file, "Empty");
			file.AddRange(BitConverter.GetBytes((Int16)0));

			Add(file, 1);
			file.AddRange(Encoding.ASCII.GetBytes("<spranim>\0" + "1\0"));
			Int32 offsetAt = file.Count;
			Add(file, 0);
			AddString(file, "walking");
			Add(file, frames);
			Add(file, directions);
			for (Int32 i = 0; i < frames * directions; i++)
			{
				Add(file, 0);
				Add(file, 0);
				Add(file, 5);
				Add(file, 1);
			}

			Byte[] offset = BitConverter.GetBytes(file.Count);
			for (Int32 i = 0; i < 4; i++)
			{
				file[offsetAt + i] = offset[i];
			}

			file.AddRange(block);
			return [.. file];
		}

		[Theory]
		[InlineData('1')]
		[InlineData('2')]
		public void Spr_IsRead(Char version)
		{
			SprFile sprite = SprReader.Read(BuildSprite(version));

			// The animation without frame to show is left out.
			SprSequence walk = Assert.Single(sprite.Sequences);
			Assert.Equal("Walk", walk.Name);
			Assert.Equal([1, 0], walk.Frames);
			Assert.Equal(0, sprite.DefaultSequence);

			SprCollection collection = sprite.GetCollection(walk.CollectionIndex);
			Assert.Equal("walking", collection.Name);
			Assert.Equal(2, collection.Directions);
			Assert.Equal(2, collection.FramesPerDirection);
			Assert.Same(collection, sprite.GetCollection(walk.CollectionIndex));

			// Each picture holds the color that was written for its frame and its direction.
			for (Int32 frame = 0; frame < 2; frame++)
			{
				for (Int32 direction = 0; direction < 2; direction++)
				{
					SprFrame picture = collection.GetFrame(direction, frame);
					Assert.Equal(5, picture.Image.Width);
					Byte color = (Byte)(((frame * 2) + direction + 1) * 10);
					Assert.Equal(new Byte[] { color, (Byte)(color + 1), (Byte)(color + 2), 255 }, picture.Image.Bgra[..4]);
				}
			}

			PixelImage drawn = ImageRenderer.Render(collection, collection.GetFrame(1, 1));
			Assert.Equal(5, drawn.Width);
			Assert.Equal(1, drawn.Height);
		}

		[Fact]
		public void Spr_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildSprite('2');
			Assert.True(SprReader.IsSpr(valid));
			Assert.False(SprReader.IsSpr(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => SprReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => SprReader.Read(valid[..30]));

			// The pictures of a collection are cut: the header reads, the collection does not.
			SprFile cut = SprReader.Read(valid[..^20]);
			Assert.Throws<InvalidDataException>(() => cut.GetCollection(0));
		}

		private static Byte[] BuildMis(Boolean withPicture = true)
		{
			List<Byte> content = [];
			content.AddRange(Encoding.ASCII.GetBytes("<mph>\0" + "8\0"));
			Add(content, 2);
			foreach (String team in new[] { "BOS", "Raiders" })
			{
				Add(content, (UInt32)team.Length);
				content.AddRange(Encoding.ASCII.GetBytes(team));
			}

			content.AddRange(new Byte[8]);
			String text = "locale/missions/test/Test.txt";
			Add(content, (UInt32)text.Length);
			content.AddRange(Encoding.ASCII.GetBytes(text));
			content.AddRange(new Byte[12]);
			if (withPicture)
				content.AddRange(BuildZar());

			using MemoryStream compressed = new();
			using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, true))
			{
				zlib.Write([.. content]);
			}

			List<Byte> file = [];
			file.AddRange(Encoding.ASCII.GetBytes("<world>\0" + "68\0"));
			Add(file, (UInt32)content.Count);
			Add(file, (UInt32)content.Count);
			file.AddRange(compressed.ToArray());
			return [.. file];
		}

		[Fact]
		public void Mis_IsRead()
		{
			MisFile mission = MisReader.Read(BuildMis());
			Assert.Equal("68", mission.Version);
			Assert.Equal(["BOS", "Raiders"], mission.Teams);
			Assert.Equal("locale/missions/test/Test.txt", mission.TextFile);
			Assert.Equal(5, mission.Preview!.Width);
		}

		[Fact]
		public void Mis_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildMis();
			Assert.True(MisReader.IsMis(valid));
			Assert.False(MisReader.IsMis(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => MisReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => MisReader.Read(valid[..30]));
			Assert.Null(MisReader.Read(BuildMis(false)).Preview);
		}

		private static void AddString(List<Byte> bytes, String text)
		{
			Add(bytes, (UInt32)text.Length);
			bytes.AddRange(Encoding.ASCII.GetBytes(text));
		}

		private static void AddProperty(List<Byte> bytes, String name, UInt32 type, Byte[] value)
		{
			AddString(bytes, name);
			Add(bytes, type);
			Add(bytes, (UInt32)value.Length);
			bytes.AddRange(value);
		}

		private static Byte[] Lengthened(String text)
		{
			List<Byte> bytes = [];
			AddString(bytes, text);
			return [.. bytes];
		}

		private static Byte[] BuildEnt()
		{
			List<Byte> inner = [];
			inner.AddRange(Encoding.ASCII.GetBytes("<entity>\0" + "2\0"));
			AddString(inner, "Weapon");
			inner.AddRange(Encoding.ASCII.GetBytes("<esh>\0" + "1\0"));
			Add(inner, 1);
			AddProperty(inner, "Damage", 3, BitConverter.GetBytes(14));

			List<Byte> bytes = [];
			bytes.AddRange(Encoding.ASCII.GetBytes("<entity>\0" + "2\0"));
			AddString(bytes, "Actor");
			bytes.AddRange(Encoding.ASCII.GetBytes("<esh>\0" + "1\0"));
			Add(bytes, 6);
			AddProperty(bytes, "XP Reward", 3, BitConverter.GetBytes(6));
			AddProperty(bytes, "Race Type", 9, Lengthened("Tribal"));
			AddProperty(bytes, "Is Open", 1, [1]);
			AddProperty(bytes, "ImageRect", 14, [.. BitConverter.GetBytes(-13), .. BitConverter.GetBytes(-74), .. BitConverter.GetBytes(15), .. BitConverter.GetBytes(-2)]);
			AddProperty(bytes, "Hand1", 4, Lengthened(Encoding.ASCII.GetString([.. inner])));
			AddProperty(bytes, "esbin", 11, BitConverter.GetBytes(0));
			return [.. bytes];
		}

		[Fact]
		public void Ent_IsRead()
		{
			EntFile entity = EntReader.Read(BuildEnt());
			Assert.Equal("Actor", entity.ClassName);
			Assert.Equal(6, entity.Properties.Count);
			Assert.Equal("6", entity.Properties[0].Value);
			Assert.Equal("Tribal", entity.Properties[1].Value);
			Assert.Equal("-13, -74, 15, -2", entity.Properties[3].Value);
			Assert.Equal("Weapon", entity.Properties[4].Value);
			Assert.Equal("14", entity.Properties[4].Children[0].Value);

			String text = EntReader.ToText(entity);
			Assert.Contains("  Hand1: Weapon", text);
			Assert.Contains("    Damage: 14", text);
		}

		[Fact]
		public void Ent_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildEnt();
			Assert.True(EntReader.IsEnt(valid));
			Assert.False(EntReader.IsEnt(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => EntReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => EntReader.Read(valid[..40]));
			Assert.Throws<InvalidDataException>(() => EntReader.Read(valid[..^20]));
		}

		[Fact]
		public void UnknownExtensions_AreShownAsText_WhenAsked()
		{
			Assert.False(FileViewers.TryGetKind("data.xyz", false, out _));
			Assert.True(FileViewers.TryGetKind("data.xyz", true, out Models.Interface.FileViewerKind kind));
			Assert.Equal(Models.Interface.FileViewerKind.Text, kind);
			Assert.True(FileViewers.TryGetKind("noextension", true, out _));
			Assert.True(FileViewers.IsViewable("data.xyz", true));
			Assert.False(FileViewers.IsViewable("data.xyz", false));

			// A known extension keeps its viewer.
			Assert.True(FileViewers.TryGetKind("hero.spr", true, out kind));
			Assert.Equal(Models.Interface.FileViewerKind.Image, kind);
		}

		[Fact]
		public void Settings_ByDefault_ShowEveryExtensionAndOpenUnknownOnesAsText()
		{
			Models.Interface.SettingsData data = new();
			Assert.True(data.ShowAllExtensions);
			Assert.True(data.UnknownExtensionsAsText);
			Assert.Null(data.GetVisibleExtensionSet());
			Assert.Contains(".txt", FileViewers.AllExtensions);
			Assert.Contains(".ini", FileViewers.AllExtensions);
			Assert.Contains(".spr", FileViewers.AllExtensions);

			// Without "all", the list shows the formats that are chosen, or all the known ones when none was chosen yet.
			data.ShowAllExtensions = false;
			HashSet<String> known = data.GetVisibleExtensionSet()!;
			Assert.Contains(".SPR", known);
			data.VisibleExtensions = [".txt"];
			HashSet<String> chosen = data.GetVisibleExtensionSet()!;
			Assert.Contains(".TXT", chosen);
			Assert.DoesNotContain(".spr", chosen);
		}

		[Fact]
		public void SettingsDialog_KeepsTheChoiceOfTheExtensions()
		{
			Models.Interface.SettingsData data = new() { ShowAllExtensions = false, VisibleExtensions = [".ini", ".txt"], UnknownExtensionsAsText = false };
			ViewModels.SettingsDialogViewModel model = new(data);
			Assert.False(model.ShowAllExtensions);
			Assert.True(model.ExtensionsEditable);
			Assert.False(model.UnknownExtensionsAsText);
			Assert.Equal([".ini", ".txt"], model.VisibleExtensionList());

			model.ShowAllExtensions = true;
			Assert.False(model.ExtensionsEditable);
			model.SetAllExtensions(true);
			Assert.Equal(FileViewers.AllExtensions.Count, model.VisibleExtensionList().Count);
			model.SetAllExtensions(false);
			Assert.Empty(model.VisibleExtensionList());
		}

		[Fact]
		public void Sheet_PutsThePicturesInATable()
		{
			ZarImage first = new(2, 1, [1, 1, 1, 255, 2, 2, 2, 255]);
			ZarImage second = new(1, 2, [3, 3, 3, 255, 4, 4, 4, 255]);
			PixelImage sheet = ImageRenderer.RenderSheet([first, second], 2);

			// A cell is as large as the largest picture, and a gap of 2 pixels lies between the cells.
			Assert.Equal(6, sheet.Width);
			Assert.Equal(2, sheet.Height);
			Assert.Equal(1, sheet.Pixels[0]);
			Assert.Equal(3, sheet.Pixels[4 * 4]);
		}

		[Theory]
		[InlineData("button.zar")]
		[InlineData("floor.TIL")]
		[InlineData("critter.spr")]
		[InlineData("bunker01.MIS")]
		public void ViewerKind_OfTheFilesOfFalloutTactics_IsImage(String name)
		{
			Assert.True(FileViewers.TryGetKind(name, out Models.Interface.FileViewerKind kind));
			Assert.Equal(Models.Interface.FileViewerKind.Image, kind);
		}

		[Fact]
		public void ViewerKind_OfAnEntity_IsText()
		{
			Assert.True(FileViewers.TryGetKind("beast_01.ent", out Models.Interface.FileViewerKind kind));
			Assert.Equal(Models.Interface.FileViewerKind.Text, kind);

			// Compared as the text it is shown as, not as the bytes.
			Assert.False(FileViewers.IsPlainText("beast_01.ent"));
		}
	}
}
