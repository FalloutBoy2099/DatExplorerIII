namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads Fallout animations and pictures (.frm, and the .fr0 to .fr5 files that hold one direction each).
	/// A file has a header, then for each of one to six directions a series of frames; every pixel is the index
	/// of a color of a palette (see <see cref="PaletteReader"/>), the index 0 being transparent. All numbers are big endian.
	/// </summary>
	public static class FrmReader
	{
		private const Int32 Directions = 6;
		private const Int32 HeaderSize = 62;
		private const Int32 FrameHeaderSize = 12;
		private const Int32 MaxFramesPerDirection = 4096;
		private const Int32 MaxPixels = 4096 * 4096;

		/// <exception cref="InvalidDataException">The data is not a valid FRM file.</exception>
		public static FrmImage Read(Byte[] data)
		{
			if (data.Length < HeaderSize)
				throw new InvalidDataException("The file is too short to be a FRM file.");

			Int32 version = (Int32)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0));
			Int32 fps = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
			Int32 actionFrame = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(6));
			Int32 framesPerDirection = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(8));

			if (framesPerDirection == 0 || framesPerDirection > MaxFramesPerDirection)
				throw new InvalidDataException("The number of frames is impossible.");

			Int32[] shiftX = new Int32[Directions];
			Int32[] shiftY = new Int32[Directions];
			Int64[] offsets = new Int64[Directions];
			for (Int32 d = 0; d < Directions; d++)
			{
				shiftX[d] = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(10 + (d * 2)));
				shiftY[d] = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(22 + (d * 2)));
				offsets[d] = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(34 + (d * 4)));
			}

			// The offset of the first direction is 0; a direction that is not used has an offset of 0 too.
			Int32 directionCount = 1;
			while (directionCount < Directions && offsets[directionCount] != 0)
			{
				directionCount++;
			}

			List<FrmDirection> directions = [with(directionCount)];
			for (Int32 d = 0; d < directionCount; d++)
			{
				directions.Add(new FrmDirection(shiftX[d], shiftY[d], ReadFrames(data, HeaderSize + offsets[d], framesPerDirection)));
			}

			return new FrmImage(version, fps, actionFrame, framesPerDirection, directions);
		}

		private static List<FrmFrame> ReadFrames(Byte[] data, Int64 start, Int32 count)
		{
			List<FrmFrame> frames = [with(count)];
			Int64 position = start;

			for (Int32 i = 0; i < count; i++)
			{
				if (position < HeaderSize || position + FrameHeaderSize > data.Length)
					throw new InvalidDataException("A frame lies outside of the file.");

				Int32 at = (Int32)position;
				Int32 width = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
				Int32 height = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at + 2));
				Int64 size = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at + 4));
				Int32 offsetX = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at + 8));
				Int32 offsetY = BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at + 10));

				Int64 pixelCount = (Int64)width * height;
				if (pixelCount > MaxPixels || at + FrameHeaderSize + pixelCount > data.Length)
					throw new InvalidDataException("A frame is larger than the file.");

				Byte[] pixels = new Byte[pixelCount];
				Array.Copy(data, at + FrameHeaderSize, pixels, 0, pixelCount);
				frames.Add(new FrmFrame(width, height, offsetX, offsetY, pixels));

				// The size field normally equals the number of pixels.
				position += FrameHeaderSize + Math.Max(size, pixelCount);
			}

			return frames;
		}
	}
}