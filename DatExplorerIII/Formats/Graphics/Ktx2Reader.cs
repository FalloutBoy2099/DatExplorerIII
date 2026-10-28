namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using System.IO.Compression;
	using Models.Graphics;
	using ZstdSharp;

	/// <summary>
	/// Reads the textures of the Khronos format KTX 2.0 (.ktx2), as the town maps of the patches of Arcanum use them: only the first
	/// picture, the biggest one, of a texture of 8 bits for each channel, stored as it is or compressed with Zstandard or zlib. The
	/// textures that are compressed for the graphics card (BasisU, BCn, ASTC...) are not read.
	/// </summary>
	public static class Ktx2Reader
	{
		/// <summary>The pictures larger than this (in pixels) are not decoded: they would take too much memory.</summary>
		private const Int64 MaxPixels = 100L * 1024 * 1024;

		private const Int32 HeaderSize = 80;
		private const Int32 LevelSize = 24;

		private const UInt32 SchemeNone = 0;
		private const UInt32 SchemeZstd = 2;
		private const UInt32 SchemeZlib = 3;

		// The formats of Vulkan (vkFormat) that are read: the channels of the pixels, in the order of the file.
		private const UInt32 R8Unorm = 9;
		private const UInt32 R8Srgb = 15;
		private const UInt32 R8G8B8Unorm = 23;
		private const UInt32 R8G8B8Srgb = 29;
		private const UInt32 R8G8B8A8Unorm = 37;
		private const UInt32 R8G8B8A8Srgb = 43;
		private const UInt32 B8G8R8A8Unorm = 44;
		private const UInt32 B8G8R8A8Srgb = 50;

		private static ReadOnlySpan<Byte> Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

		/// <summary>True when the data starts like a KTX 2.0 file.</summary>
		public static Boolean IsKtx2(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize && data.StartsWith(Identifier);
		}

		/// <exception cref="InvalidDataException">The data is not a valid KTX2 file, or its format is not read.</exception>
		public static PixelImage Read(Byte[] data)
		{
			if (!IsKtx2(data))
				throw new InvalidDataException("This is not a KTX2 texture.");

			ReadOnlySpan<Byte> span = data;
			UInt32 format = BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
			UInt32 width = BinaryPrimitives.ReadUInt32LittleEndian(span[20..]);
			UInt32 height = BinaryPrimitives.ReadUInt32LittleEndian(span[24..]);
			UInt32 scheme = BinaryPrimitives.ReadUInt32LittleEndian(span[44..]);

			// A height of 0 would be a texture of one dimension.
			if (width == 0 || (Int64)width * Math.Max(height, 1) > MaxPixels)
				throw new InvalidDataException("The size of the texture is impossible.");

			height = Math.Max(height, 1);
			Int32 channels = format switch
			{
				R8Unorm or R8Srgb => 1,
				R8G8B8Unorm or R8G8B8Srgb => 3,
				R8G8B8A8Unorm or R8G8B8A8Srgb or B8G8R8A8Unorm or B8G8R8A8Srgb => 4,
				_ => throw new InvalidDataException("The format of the texture is not read."),
			};

			// The first level of the index is the biggest picture.
			UInt64 offset = BinaryPrimitives.ReadUInt64LittleEndian(span[HeaderSize..]);
			UInt64 length = BinaryPrimitives.ReadUInt64LittleEndian(span[(HeaderSize + 8)..]);
			if (data.Length < HeaderSize + LevelSize || offset > (UInt64)data.Length || length > (UInt64)data.Length - offset)
				throw new InvalidDataException("The texture is incomplete.");

			Int32 size = checked((Int32)(width * height * (UInt32)channels));
			Byte[] raw = new Byte[size];
			Int32 written;
			try
			{
				written = scheme switch
				{
					SchemeNone => Copy(span.Slice((Int32)offset, (Int32)length), raw),
					SchemeZstd => new Decompressor().Unwrap(span.Slice((Int32)offset, (Int32)length), raw),
					SchemeZlib => InflateZlib(data, (Int32)offset, (Int32)length, raw),
					_ => throw new InvalidDataException("The compression of the texture is not read."),
				};
			}
			catch (Exception exception) when (exception is ZstdException or IOException or ArgumentException or InvalidOperationException)
			{
				throw new InvalidDataException("The texture cannot be decompressed.", exception);
			}

			if (written != size)
				throw new InvalidDataException("The texture is not the size it says.");

			Byte[] pixels = new Byte[(Int32)(width * height) * 4];
			Boolean swap = format is B8G8R8A8Unorm or B8G8R8A8Srgb;
			for (Int32 i = 0, count = (Int32)(width * height); i < count; i++)
			{
				Int32 target = i * 4;
				Int32 source = i * channels;
				switch (channels)
				{
					case 1:
						pixels[target] = pixels[target + 1] = pixels[target + 2] = raw[source];
						pixels[target + 3] = 255;
						break;
					case 3:
						pixels[target] = raw[source + 2];
						pixels[target + 1] = raw[source + 1];
						pixels[target + 2] = raw[source];
						pixels[target + 3] = 255;
						break;
					default:
						// The pixels are drawn as blue, green, red, alpha: that is the order of the file for B8G8R8A8.
						pixels[target] = swap ? raw[source] : raw[source + 2];
						pixels[target + 1] = raw[source + 1];
						pixels[target + 2] = swap ? raw[source + 2] : raw[source];
						pixels[target + 3] = raw[source + 3];
						break;
				}
			}

			return new PixelImage((Int32)width, (Int32)height, pixels);
		}

		private static Int32 Copy(ReadOnlySpan<Byte> stored, Byte[] target)
		{
			stored[..Math.Min(stored.Length, target.Length)].CopyTo(target);
			return Math.Min(stored.Length, target.Length);
		}

		private static Int32 InflateZlib(Byte[] data, Int32 offset, Int32 length, Byte[] target)
		{
			using ZLibStream zlib = new(new MemoryStream(data, offset, length), CompressionMode.Decompress);
			return zlib.ReadAtLeast(target, target.Length, false);
		}
	}
}
