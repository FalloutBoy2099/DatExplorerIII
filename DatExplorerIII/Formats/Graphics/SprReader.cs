namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Text;
	using Models.Graphics;

	/// <summary>
	/// Reads the sprites of Fallout Tactics (.spr): the signature "&lt;sprite&gt;" and a zero, a version (16 bits), the sizes of the sprite
	/// in tiles (3 bytes), its offset (2 times 32 bits), 3 bytes, then the animations ("sequences": the count of frames, 16 bits that are not
	/// understood, a 16 bit frame number for each, 4 bytes for each that are not understood, the name as a string, and the collection it
	/// draws from) and the collections ("&lt;spranim&gt;" a zero, a version and a zero, the offset of the pictures in the file, the name, the
	/// count of frames, the count of directions, and the rectangle (4 times 32 bits) of each frame and direction).
	/// The pictures of a collection start with "&lt;spranim_img&gt;" and a zero, a version (16 bits: '1' as it is, '2' the size and a zlib
	/// stream), four palettes (a count and 4 bytes a color), then four layers for each picture (a byte: 0 none, 1 the position, 32 bits
	/// twice, and a picture of the format <see cref="ZarReader"/> without palette), which are drawn one over the other in the rectangle.
	/// The layout is the one of the map editor of luboshorak, which follows FreeFT.
	/// </summary>
	public static class SprReader
	{
		private const Int32 MaxCount = 100_000;
		private const Int32 MaxDirections = 128;
		private const Int32 MaxSide = 16_384;
		private const Int32 MaxPayload = 512 * 1024 * 1024;
		private const Int32 PaletteColors = 256;
		private const Int32 Layers = 4;

		private static ReadOnlySpan<Byte> SpriteTag => "<sprite>\0"u8;

		private static ReadOnlySpan<Byte> AnimationTag => "<spranim>\01\0"u8;

		private static ReadOnlySpan<Byte> ImageTag => "<spranim_img>\0"u8;

		/// <summary>True when the data starts like a SPR file.</summary>
		public static Boolean IsSpr(ReadOnlySpan<Byte> data)
		{
			return data.StartsWith(SpriteTag);
		}

		/// <exception cref="InvalidDataException">The data is not a valid SPR file.</exception>
		public static SprFile Read(Byte[] data)
		{
			if (!IsSpr(data))
				throw new InvalidDataException("This is not a SPR sprite.");

			Cursor cursor = new(data, SpriteTag.Length);
			cursor.Skip(2 + 3);
			Int32 offsetX = cursor.ReadInt32();
			Int32 offsetY = cursor.ReadInt32();
			cursor.Skip(3);
			Int32 sequenceCount = cursor.ReadInt32();
			if (sequenceCount is < 1 or > MaxCount)
				throw new InvalidDataException("The count of animations is impossible.");

			List<RawSequence> raw = [];
			for (Int32 i = 0; i < sequenceCount; i++)
			{
				Int32 frameCount = cursor.ReadInt16();
				cursor.Skip(2);
				if (frameCount is < 1 or > MaxCount)
					throw new InvalidDataException("The count of frames of an animation is impossible.");

				Int32[] frames = new Int32[frameCount];
				for (Int32 f = 0; f < frameCount; f++)
				{
					frames[f] = cursor.ReadInt16();
				}

				cursor.Skip(frameCount * 4);
				String name = cursor.ReadString();
				raw.Add(new RawSequence(name, cursor.ReadInt16(), frames));
			}

			Int32 collectionCount = cursor.ReadInt32();
			if (collectionCount is < 1 or > MaxCount)
				throw new InvalidDataException("The count of collections is impossible.");

			List<RawCollection> collections = [];
			for (Int32 i = 0; i < collectionCount; i++)
			{
				cursor.Expect(AnimationTag);
				Int32 dataOffset = cursor.ReadInt32();
				String name = cursor.ReadString();
				Int32 frameCount = cursor.ReadInt32();
				Int32 directionCount = cursor.ReadInt32();
				if (frameCount is < 1 or > MaxCount || directionCount is < 1 or > MaxDirections || (Int64)frameCount * directionCount > MaxCount)
					throw new InvalidDataException("The size of a collection is impossible.");

				Rectangle[] rectangles = new Rectangle[frameCount * directionCount];
				for (Int32 r = 0; r < rectangles.Length; r++)
				{
					rectangles[r] = new Rectangle(cursor.ReadInt32(), cursor.ReadInt32(), cursor.ReadInt32(), cursor.ReadInt32());
				}

				collections.Add(new RawCollection(name, dataOffset, frameCount, directionCount, rectangles));
			}

			// Only the animations that have a collection and a frame to draw can be shown.
			List<SprSequence> sequences = [];
			foreach (RawSequence sequence in raw)
			{
				if (sequence.CollectionIndex < 0 || sequence.CollectionIndex >= collectionCount)
					continue;

				RawCollection collection = collections[sequence.CollectionIndex];
				List<Int32> frames = sequence.Frames.Where(frame => frame >= 0 && frame < collection.FrameCount).ToList();
				if (frames.Count > 0)
					sequences.Add(new SprSequence(sequence.Name, sequence.CollectionIndex, frames));
			}

			if (sequences.Count == 0)
				throw new InvalidDataException("The sprite has no animation to show.");

			// The origin of the sprite: its offset, corrected by its size in tiles.
			Int32 originX = offsetX - (6 * (data[SpriteTag.Length + 4] - data[SpriteTag.Length + 2]));
			Int32 originY = offsetY - (3 * (data[SpriteTag.Length + 4] + data[SpriteTag.Length + 2]));
			return new SprFile(sequences, collectionCount, index => ReadCollection(data, collections, index, originX, originY));
		}

		private static SprCollection ReadCollection(Byte[] data, List<RawCollection> collections, Int32 index, Int32 originX, Int32 originY)
		{
			RawCollection collection = collections[index];
			Int32 end = collections.Select(other => other.DataOffset).Where(offset => offset > collection.DataOffset).DefaultIfEmpty(data.Length).Min();
			Byte[] payload = ReadPayload(data, collection, end, out Char type);

			Int32 frames = collection.FrameCount;
			Int32 directions = collection.DirectionCount;
			Int32 perLayer = frames * directions;
			Byte[]?[] pictures = new Byte[perLayer][];
			for (Int32 i = 0; i < perLayer; i++)
			{
				Rectangle rectangle = collection.Rectangles[i];
				if (rectangle.Width is >= 1 and <= MaxSide && rectangle.Height is >= 1 and <= MaxSide)
					pictures[i] = new Byte[rectangle.Width * rectangle.Height * 4];
			}

			Cursor cursor = new(payload, 0);
			Byte[][] palettes = new Byte[Layers][];
			for (Int32 layer = 0; layer < Layers; layer++)
			{
				Int32 colors = cursor.ReadInt32();
				if (colors is < 0 or > PaletteColors)
					throw new InvalidDataException("A palette of the sprite is impossible.");

				palettes[layer] = new Byte[PaletteColors * 4];
				cursor.ReadBytes(colors * 4).CopyTo(palettes[layer]);
			}

			Int32 position = cursor.Position;
			for (Int32 image = 0; image < perLayer * Layers && position < payload.Length; image++)
			{
				Byte item = payload[position++];
				if (item == 0)
					continue;

				if (item == (Byte)'<')
					break;

				if (item != 1 || payload.Length - position < 8)
					throw new InvalidDataException("An image of the sprite is damaged.");

				Int32 pointX = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(position));
				Int32 pointY = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(position + 4));
				position += 8;

				// Where the image goes: the images of the old kind come picture by picture, the others layer by layer.
				Int32 layer, picture;
				if (type == '1')
				{
					layer = image % Layers;
					picture = image / Layers;
				}
				else
				{
					layer = image / perLayer;
					Int32 rest = image % perLayer;
					picture = ((rest % frames) * directions) + (rest / frames);
				}

				ZarImage layerImage = ZarReader.Decode(payload, ref position, palettes[layer], true);
				Byte[]? target = pictures[picture];
				if (target != null)
					Blend(target, collection.Rectangles[picture].Width, collection.Rectangles[picture].Height, layerImage, pointX, pointY);
			}

			List<SprFrame> result = [];
			for (Int32 i = 0; i < perLayer; i++)
			{
				Rectangle rectangle = collection.Rectangles[i];
				Byte[]? bgra = pictures[i];
				result.Add(bgra == null
					? new SprFrame(0, 0, new ZarImage(0, 0, []))
					: new SprFrame(rectangle.Left - originX, rectangle.Top - originY, new ZarImage(rectangle.Width, rectangle.Height, bgra)));
			}

			return new SprCollection(collection.Name, directions, frames, result);
		}

		/// <summary>The data of a collection, inflated when its version says so.</summary>
		private static Byte[] ReadPayload(Byte[] data, RawCollection collection, Int32 end, out Char type)
		{
			Int32 at = collection.DataOffset;
			if (at < 0 || end <= at || end > data.Length || end - at < ImageTag.Length + 2 || !data.AsSpan(at).StartsWith(ImageTag))
				throw new InvalidDataException($"The pictures of \"{collection.Name}\" are missing.");

			at += ImageTag.Length;
			type = (Char)BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(at));
			at += 2;
			if (type == '1')
				return data.AsSpan(at, end - at).ToArray();

			if (type != '2')
				throw new InvalidDataException("The version of the pictures of the sprite is not supported.");

			if (end - at < 4)
				throw new InvalidDataException("The pictures of the sprite are cut.");

			Int32 size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at));
			at += 4;
			if (size is < 1 or > MaxPayload)
				throw new InvalidDataException("The size of the pictures of the sprite is impossible.");

			// The stream is read for the size it declares: the pictures of the next collection follow it.
			using MemoryStream compressed = new(data, at, end - at, false);
			using ZLibStream inflater = new(compressed, CompressionMode.Decompress);
			Byte[] payload = new Byte[size];
			try
			{
				inflater.ReadExactly(payload);
			}
			catch (Exception exception) when (exception is EndOfStreamException or InvalidDataException)
			{
				throw new InvalidDataException("The pictures of the sprite cannot be inflated.", exception);
			}

			return payload;
		}

		/// <summary>Draws a layer over a picture, mixing the colors where the layer is partly transparent.</summary>
		private static void Blend(Byte[] target, Int32 targetWidth, Int32 targetHeight, ZarImage source, Int32 pointX, Int32 pointY)
		{
			for (Int32 y = 0; y < source.Height; y++)
			{
				Int32 targetY = pointY + y;
				if (targetY < 0 || targetY >= targetHeight)
					continue;

				for (Int32 x = 0; x < source.Width; x++)
				{
					Int32 targetX = pointX + x;
					if (targetX < 0 || targetX >= targetWidth)
						continue;

					Int32 from = ((y * source.Width) + x) * 4;
					Int32 alpha = source.Bgra[from + 3];
					if (alpha == 0)
						continue;

					Int32 to = ((targetY * targetWidth) + targetX) * 4;
					Int32 below = target[to + 3];
					if (alpha == 255 || below == 0)
					{
						Array.Copy(source.Bgra, from, target, to, 4);
						continue;
					}

					Int32 inverse = 255 - alpha;
					Int32 outAlpha = alpha + (below * inverse / 255);
					for (Int32 channel = 0; channel < 3; channel++)
					{
						Int32 over = source.Bgra[from + channel] * alpha;
						Int32 under = target[to + channel] * below * inverse / 255;
						target[to + channel] = (Byte)((over + under) / outAlpha);
					}

					target[to + 3] = (Byte)outAlpha;
				}
			}
		}

		private sealed record RawSequence(String Name, Int32 CollectionIndex, Int32[] Frames);

		private sealed record RawCollection(String Name, Int32 DataOffset, Int32 FrameCount, Int32 DirectionCount, Rectangle[] Rectangles);

		private readonly record struct Rectangle(Int32 Left, Int32 Top, Int32 Right, Int32 Bottom)
		{
			public Int32 Width => Right - Left;

			public Int32 Height => Bottom - Top;
		}

		/// <summary>Reads the fields of the header one after the other, and refuses what is outside of the data.</summary>
		private sealed class Cursor(Byte[] data, Int32 position)
		{
			public Int32 Position { get; private set; } = position;

			public void Skip(Int32 count)
			{
				Require(count);
				Position += count;
			}

			public ReadOnlySpan<Byte> ReadBytes(Int32 count)
			{
				Require(count);
				ReadOnlySpan<Byte> bytes = data.AsSpan(Position, count);
				Position += count;
				return bytes;
			}

			public Int32 ReadInt16()
			{
				return BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));
			}

			public Int32 ReadInt32()
			{
				return BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));
			}

			public String ReadString()
			{
				Int32 length = ReadInt32();
				if (length is < 0 or > 1_048_576)
					throw new InvalidDataException("A name of the sprite is impossible.");

				return Encoding.ASCII.GetString(ReadBytes(length)).TrimEnd('\0');
			}

			public void Expect(ReadOnlySpan<Byte> expected)
			{
				if (!ReadBytes(expected.Length).SequenceEqual(expected))
					throw new InvalidDataException("The sprite is damaged.");
			}

			private void Require(Int32 count)
			{
				if (count < 0 || Position > data.Length - count)
					throw new InvalidDataException("The sprite is cut.");
			}
		}
	}
}
