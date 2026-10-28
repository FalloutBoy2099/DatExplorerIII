namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using DatExplorerIII.Formats.GameFiles;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.Models.Graphics;
	using DatExplorerIII.Services.Media;
	using DatExplorerIII.Services.Platform;

	/// <summary>
	/// The sectors of the maps of Arcanum (.sec).
	/// </summary>
	public sealed class SecFormatTests
	{
		private static void Add(List<Byte> bytes, Int32 value)
		{
			Byte[] buffer = new Byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
			bytes.AddRange(buffer);
		}

		/// <summary>A sector with the given version: 2 lights, tiles numbered from 1, 3 scripts of tiles, and the given count of objects.</summary>
		private static Byte[] BuildSector(Int32 version, Boolean withRoof, Int32 objects)
		{
			List<Byte> bytes = [];
			Add(bytes, 2);
			for (Int32 light = 0; light < 2; light++)
			{
				bytes.AddRange(new Byte[8]);
				Add(bytes, 10 + light);
				Add(bytes, 20 + light);
				Add(bytes, 1);
				Add(bytes, 2);
				Add(bytes, 3);
				Add(bytes, 4);
				Add(bytes, 0xFF0000);
				Add(bytes, 0x0000FF);
				Add(bytes, 7);
				Add(bytes, 8);
			}

			for (Int32 tile = 0; tile < 4096; tile++)
			{
				Add(bytes, tile + 1);
			}

			Add(bytes, withRoof ? 0 : 1);
			if (withRoof)
			{
				for (Int32 roof = 0; roof < 256; roof++)
				{
					Add(bytes, 1000 + roof);
				}
			}

			Add(bytes, 0xAA0000 + version);
			if (version >= 1)
			{
				Add(bytes, 3);
				for (Int32 i = 0; i < 18; i++)
				{
					Add(bytes, i);
				}
			}

			if (version >= 2)
			{
				bytes.AddRange(new Byte[4]);
				Add(bytes, 0);
				Add(bytes, 77);
			}

			if (version >= 3)
			{
				Add(bytes, 5);
				Add(bytes, 6);
				Add(bytes, 9);
				bytes.AddRange(new Byte[12]);
			}

			if (version >= 4)
				bytes.AddRange(new Byte[512]);

			// Objects are not read: their bytes are only counted.
			bytes.AddRange(new Byte[objects * 10]);
			Add(bytes, objects);
			return [.. bytes];
		}

		[Theory]
		[InlineData(0, true)]
		[InlineData(1, false)]
		[InlineData(2, true)]
		[InlineData(3, false)]
		[InlineData(4, true)]
		public void Sector_IsRead(Int32 version, Boolean withRoof)
		{
			SecFile sector = SecReader.Read(BuildSector(version, withRoof, 3));
			Assert.Equal(version, sector.Version);
			Assert.Equal(2, sector.Lights.Count);
			Assert.Equal(new SecLight(11, 21, 1, 2, 3, 4, 0xFF0000, 0x0000FF), sector.Lights[1]);
			Assert.Equal(4096, sector.Tiles.Length);
			Assert.Equal(4096u, sector.Tiles[4095]);
			Assert.Equal(withRoof, sector.Roof != null);
			if (withRoof)
				Assert.Equal(1255u, sector.Roof![255]);

			Assert.Equal(version >= 1 ? 3 : 0, sector.TileScripts.Count);
			Assert.Equal(version >= 2 ? 77 : 0, sector.ScriptId);
			Assert.Equal(version >= 3 ? 5 : 0, sector.TownMapInfo);
			Assert.Equal(version >= 3 ? 9 : 0, sector.LightScheme);
			Assert.Equal(3, sector.ObjectCount);
			Assert.Equal(30, sector.ObjectBytes);
		}

		[Fact]
		public void Sector_ThatIsDamaged_IsRefused()
		{
			Byte[] valid = BuildSector(4, true, 0);
			Assert.True(SecReader.IsSec(valid));
			Assert.False(SecReader.IsSec(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => SecReader.Read(new Byte[100]));
			Assert.Throws<InvalidDataException>(() => SecReader.Read(valid[..2000]));

			// A version that does not exist.
			Byte[] otherVersion = (Byte[])valid.Clone();
			BinaryPrimitives.WriteInt32LittleEndian(otherVersion.AsSpan(4 + (2 * 48) + 16384 + 4 + 1024), 0xAA0009);
			Assert.Throws<InvalidDataException>(() => SecReader.Read(otherVersion));

			// An absurd count of lights.
			Byte[] lights = (Byte[])valid.Clone();
			BinaryPrimitives.WriteInt32LittleEndian(lights, 0x7FFFFFFF);
			Assert.Throws<InvalidDataException>(() => SecReader.Read(lights));
		}

		[Fact]
		public void Sector_IsDrawnWithAColorForEachKindOfTile()
		{
			UInt32[] tiles = new UInt32[4096];
			tiles[0] = 0x4101C0;
			tiles[1] = 0x4105C0;
			tiles[2] = 0x4141C0;
			tiles[3] = UInt32.MaxValue;
			SecFile sector = new(4, [], tiles, null, [], 0, 0, 0, 0, 0, 0);
			PixelImage picture = ImageRenderer.Render(sector);
			Assert.Equal(256, picture.Width);
			Assert.Equal(256, picture.Height);

			// The same kind has the same color, another kind has another one, an empty tile is clear.
			Assert.Equal(picture.Pixels[..4], picture.Pixels[16..20]);
			Assert.NotEqual(picture.Pixels[..4], picture.Pixels[32..36]);
			Assert.Equal(255, picture.Pixels[3]);
			Assert.Equal(0, picture.Pixels[(3 * 16) + 3]);
		}

		[Fact]
		public void ViewerKind_OfASector_IsImage()
		{
			Assert.True(FileViewers.TryGetKind("73685534090.sec", out DatExplorerIII.Models.Interface.FileViewerKind kind));
			Assert.Equal(DatExplorerIII.Models.Interface.FileViewerKind.Image, kind);
		}
	}
}
