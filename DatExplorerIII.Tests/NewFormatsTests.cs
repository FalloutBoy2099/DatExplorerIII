namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using System.Text;
	using DatExplorerIII.Formats.Audio;
	using DatExplorerIII.Formats.GameFiles;
	using DatExplorerIII.Formats.Graphics;
	using DatExplorerIII.Models.Audio;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.Models.Graphics;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Texts;
	using Xunit;
	using ZstdSharp;

	/// <summary>The pictures QOI and KTX2, the town maps (.tmi, .tma) and the sounds .snd of Arcanum.</summary>
	public sealed class NewFormatsTests
	{
		private static Byte[] Qoi(UInt32 width, UInt32 height, Byte channels, params Byte[] chunks)
		{
			Byte[] data = new Byte[14 + chunks.Length + 8];
			Encoding.ASCII.GetBytes("qoif").CopyTo(data, 0);
			BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(4), width);
			BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), height);
			data[12] = channels;
			chunks.CopyTo(data, 14);
			data[^1] = 1;
			return data;
		}

		[Fact]
		public void Qoi_ReadsEveryKindOfChunk()
		{
			PixelImage picture = QoiReader.Read(Qoi(
				5,
				1,
				4,
				0xFE, 10, 20, 30, // RGB
				0b01_11_10_01, // DIFF: red +1, green 0, blue -1
				0b10_100001, 0x9F, // LUMA: green +1, red +1 more, blue +7 more
				0xFF, 1, 2, 3, 128, // RGBA
				0b11_000000)); // RUN: the same pixel once more

			Assert.Equal(5, picture.Width);
			Assert.Equal(
				new Byte[] { 30, 20, 10, 255, 29, 20, 11, 255, 37, 21, 13, 255, 3, 2, 1, 128, 3, 2, 1, 128 },
				picture.Pixels);
		}

		[Fact]
		public void Qoi_ReadsThePixelOfAnIndex()
		{
			PixelImage picture = QoiReader.Read(Qoi(2, 1, 3, 0xFE, 10, 20, 30, (Byte)(((10 * 3) + (20 * 5) + (30 * 7) + (255 * 11)) % 64)));
			Assert.Equal(picture.Pixels[..4], picture.Pixels[4..]);
		}

		[Fact]
		public void Qoi_RejectsWhatIsNotAPicture()
		{
			Assert.Throws<InvalidDataException>(() => QoiReader.Read([1, 2, 3, 4]));
			Assert.Throws<InvalidDataException>(() => QoiReader.Read(Qoi(3, 1, 4, 0xFE, 1, 2, 3)));
			Assert.Throws<InvalidDataException>(() => QoiReader.Read(Qoi(1, 1, 2, 0xFE, 1, 2, 3)));
		}

		private static Byte[] Ktx2(UInt32 format, UInt32 width, UInt32 height, UInt32 scheme, Byte[] level, Int32 uncompressed)
		{
			Byte[] data = new Byte[104 + level.Length];
			new Byte[] { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(data, 0);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), format);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(20), width);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(24), height);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(36), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(40), 1);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(44), scheme);
			BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(80), 104);
			BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(88), (UInt64)level.Length);
			BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(96), (UInt64)uncompressed);
			level.CopyTo(data, 104);
			return data;
		}

		[Fact]
		public void Ktx2_ReadsRgbaStoredAsItIs()
		{
			PixelImage picture = Ktx2Reader.Read(Ktx2(37, 2, 1, 0, [1, 2, 3, 4, 5, 6, 7, 8], 8));
			Assert.Equal(new Byte[] { 3, 2, 1, 4, 7, 6, 5, 8 }, picture.Pixels);
		}

		[Fact]
		public void Ktx2_ReadsBgraAndRgb()
		{
			Assert.Equal(new Byte[] { 1, 2, 3, 4 }, Ktx2Reader.Read(Ktx2(44, 1, 1, 0, [1, 2, 3, 4], 4)).Pixels);
			Assert.Equal(new Byte[] { 3, 2, 1, 255 }, Ktx2Reader.Read(Ktx2(23, 1, 1, 0, [1, 2, 3], 3)).Pixels);
		}

		[Fact]
		public void Ktx2_ReadsZstandard()
		{
			Byte[] pixels = new Byte[16 * 16 * 4];
			for (Int32 i = 0; i < pixels.Length; i++)
			{
				pixels[i] = (Byte)(i / 4);
			}

			Byte[] packed = new Compressor().Wrap(pixels).ToArray();
			PixelImage picture = Ktx2Reader.Read(Ktx2(37, 16, 16, 2, packed, pixels.Length));
			Assert.Equal(16, picture.Width);
			Assert.Equal(pixels[0], picture.Pixels[2]);
			Assert.Equal(pixels[^4], picture.Pixels[^2]);
		}

		[Fact]
		public void Ktx2_RejectsWhatItCannotRead()
		{
			Assert.Throws<InvalidDataException>(() => Ktx2Reader.Read([1, 2, 3]));
			Assert.Throws<InvalidDataException>(() => Ktx2Reader.Read(Ktx2(145, 1, 1, 0, [1, 2, 3, 4], 4)));
			Assert.Throws<InvalidDataException>(() => Ktx2Reader.Read(Ktx2(37, 1, 1, 1, [1, 2, 3, 4], 4)));
			Assert.Throws<InvalidDataException>(() => Ktx2Reader.Read(Ktx2(37, 2, 2, 0, [1, 2, 3, 4], 4)));
			Assert.Throws<InvalidDataException>(() => Ktx2Reader.Read(Ktx2(37, 1, 1, 2, [1, 2, 3, 4], 4)));
		}

		private static Byte[] Tma(UInt32 width, UInt32 height, params Byte[] bits)
		{
			Byte[] data = new Byte[32 + bits.Length];
			Encoding.ASCII.GetBytes("TMA2").CopyTo(data, 0);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), width);
			BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), height);
			bits.CopyTo(data, 32);
			return data;
		}

		[Fact]
		public void Tma_ReadsBitsLowestFirstWithoutPaddingBetweenTheLines()
		{
			// 3 by 3: the bits 0, 4 and 8 are set, that is (0, 0), (1, 1) and (2, 2).
			MaskImage mask = TmaReader.Read(Tma(3, 3, 0b0001_0001, 0b0000_0001));
			Assert.Equal(3, mask.SetCount);
			Assert.True(mask.Pixels[0]);
			Assert.True(mask.Pixels[4]);
			Assert.True(mask.Pixels[8]);
			Assert.False(mask.Pixels[1]);
		}

		[Fact]
		public void Tma_RejectsWhatIsNotAnArea()
		{
			Assert.Throws<InvalidDataException>(() => TmaReader.Read([1, 2, 3]));
			Assert.Throws<InvalidDataException>(() => TmaReader.Read(Tma(16, 16, 0xFF)));
			Assert.Throws<InvalidDataException>(() => TmaReader.Read(Tma(0, 4)));
		}

		private static Byte[] Tmi(Int32 map, Int32 tiles)
		{
			Int32[] values = [2, 0, map, tiles * 256, tiles * 128, tiles, tiles, -1, 96, 160, BitConverter.SingleToInt32Bits(0.25f), 7];
			Byte[] data = new Byte[values.Length * 4];
			for (Int32 i = 0; i < values.Length; i++)
			{
				BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), values[i]);
			}

			return data;
		}

		[Fact]
		public void Tmi_ReadsItsFields()
		{
			TmiFile file = TmiReader.Read(Tmi(21, 140));
			Assert.Equal(21, file.MapId);
			Assert.Equal(35840, file.PictureWidth);
			Assert.Equal(17920, file.PictureHeight);
			Assert.Equal(140, file.TilesWidth);
			Assert.Equal(-1, file.Flag);
			Assert.Equal(0.25f, file.Scale);
		}

		[Fact]
		public void Tmi_IsShownAsText()
		{
			Assert.True(ArcanumFileText.Handles(".tmi"));
			Assert.Equal(2, ArcanumFileText.Render(".tmi", Tmi(21, 140)).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
			Assert.Throws<InvalidDataException>(() => TmiReader.Read([2, 0, 0, 0]));
		}

		[Fact]
		public void Snd_ReadsEightBitSamples()
		{
			Byte[] data = new Byte[4 + 22050];
			BinaryPrimitives.WriteUInt32LittleEndian(data, 22050);
			Array.Fill(data, (Byte)128, 4, 22050);
			AudioClip clip = SndReader.Read(data);
			Assert.Equal(22050, clip.SampleRate);
			Assert.Equal(1, clip.Channels);
			Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
		}

		[Fact]
		public void Snd_RejectsASizeThatIsNotTheOneOfTheFile()
		{
			Assert.Throws<InvalidDataException>(() => SndReader.Read([5, 0, 0, 0, 128]));
			Assert.Throws<InvalidDataException>(() => SndReader.Read([1, 2]));
		}

		[Theory]
		[InlineData("a.qoi", FileViewerKind.Image)]
		[InlineData("a.ktx2", FileViewerKind.Image)]
		[InlineData("a.tma", FileViewerKind.Image)]
		[InlineData("a.tmi", FileViewerKind.Text)]
		[InlineData("a.snd", FileViewerKind.Audio)]
		public void NewExtensions_AreShown(String name, FileViewerKind expected)
		{
			Assert.True(FileViewers.TryGetKind(name, out FileViewerKind kind));
			Assert.Equal(expected, kind);
		}

		[Theory]
		[InlineData("Arcanum.PATCH9", true)]
		[InlineData("Arcanum.PATCH10", true)]
		[InlineData("Arcanum.patch99", true)]
		[InlineData("Arcanum.patch1a", false)]
		[InlineData("Arcanum.patch100", false)]
		[InlineData("Arcanum.patch", false)]
		public void Patches_CanHaveTwoDigits(String path, Boolean expected)
		{
			Assert.Equal(expected, DatFiles.IsDatFile(path));
		}
	}
}
