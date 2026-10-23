namespace DatExplorerIII.Formats.Movies
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using Models.Audio;
	using Formats.Audio;

	/// <summary>
	/// Reads the Interplay movies (.mve) of Fallout: 8 bit pictures made of 8x8 blocks with a palette, and sound that is
	/// plain or compressed (Interplay DPCM).
	/// </summary>
	/// <remarks>
	/// A movie is a list of chunks, each made of segments (a length, an opcode, a version and data). <see cref="Read"/>
	/// lists them, decodes the sound at once and keeps the segments of the pictures, which a <see cref="MveFrameReader"/>
	/// decodes one frame after the other.
	/// </remarks>
	public static class MveReader
	{
		private const Int32 EnvelopeLength = 1000;

		/// <summary>The largest picture accepted (a movie of Fallout is 640 by 320 or 640 by 480).</summary>
		private const Int64 MaxPixels = 4096L * 4096;

		/// <summary>The longest time accepted between two pictures, in microseconds.</summary>
		private const Int64 MaxPeriod = 5_000_000;

		/// <summary>Decoded sound larger than this is cut.</summary>
		private const Int64 MaxPcmBytes = 256L * 1024 * 1024;

		private static readonly Byte[] Signature = "Interplay MVE File\u001A\0"u8.ToArray();

		/// <summary>The change of the sound for each byte of a compressed sound.</summary>
		private static readonly Int16[] DeltaTable =
		[
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
			16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31,
			32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 47, 51, 56, 61,
			66, 72, 79, 86, 94, 102, 112, 122, 133, 145, 158, 173, 189, 206, 225, 245,
			267, 292, 318, 348, 379, 414, 452, 493, 538, 587, 640, 699, 763, 832, 908, 991,
			1081, 1180, 1288, 1405, 1534, 1673, 1826, 1993, 2175, 2373, 2590, 2826, 3084, 3365, 3672, 4008,
			4373, 4772, 5208, 5683, 6202, 6767, 7385, 8059, 8794, 9597, 10472, 11428, 12471, 13609, 14851, 16206,
			17685, 19298, 21060, 22981, 25078, 27367, 29864, 32589, -29973, -26728, -23186, -19322, -15105, -10503, -5481, -1,
			1, 1, 5481, 10503, 15105, 19322, 23186, 26728, 29973, -32589, -29864, -27367, -25078, -22981, -21060, -19298,
			-17685, -16206, -14851, -13609, -12471, -11428, -10472, -9597, -8794, -8059, -7385, -6767, -6202, -5683, -5208, -4772,
			-4373, -4008, -3672, -3365, -3084, -2826, -2590, -2373, -2175, -1993, -1826, -1673, -1534, -1405, -1288, -1180,
			-1081, -991, -908, -832, -763, -699, -640, -587, -538, -493, -452, -414, -379, -348, -318, -292,
			-267, -245, -225, -206, -189, -173, -158, -145, -133, -122, -112, -102, -94, -86, -79, -72,
			-66, -61, -56, -51, -47, -43, -42, -41, -40, -39, -38, -37, -36, -35, -34, -33,
			-32, -31, -30, -29, -28, -27, -26, -25, -24, -23, -22, -21, -20, -19, -18, -17,
			-16, -15, -14, -13, -12, -11, -10, -9, -8, -7, -6, -5, -4, -3, -2, -1,
		];

		/// <summary>True when the data starts like a movie.</summary>
		public static Boolean IsMve(ReadOnlySpan<Byte> data)
		{
			return data.Length >= 26 && data[..Signature.Length].SequenceEqual(Signature);
		}

		/// <summary>
		/// Lists the content of a movie and decodes its sound.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not a movie, or has no picture.</exception>
		public static MveMovie Read(Byte[] data)
		{
			if (!IsMve(data))
				throw new InvalidDataException("This is not a movie.");

			Int32 headerSize = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(Signature.Length));
			if (headerSize < 26 || headerSize > data.Length)
				throw new InvalidDataException("The header of the movie is damaged.");

			List<MveSegment> segments = [];
			Boolean incomplete = ListSegments(data, headerSize, segments);

			Int32 width = 0;
			Int32 height = 0;
			Int64 period = 0;
			Int32 frames = 0;
			AudioFormat? audioFormat = null;
			using MemoryStream pcm = new();
			Int32[] predictors = new Int32[2];

			foreach (MveSegment segment in segments)
			{
				ReadOnlySpan<Byte> body = data.AsSpan(segment.Offset, segment.Length);
				switch (segment.Opcode)
				{
					case 0x02 when body.Length >= 6:
						period = BinaryPrimitives.ReadUInt32LittleEndian(body) * (Int64)BinaryPrimitives.ReadUInt16LittleEndian(body[4..]);
						break;

					case 0x03 when body.Length >= 6:
						audioFormat = ReadAudioFormat(body, segment.Version);
						break;

					case 0x05 when body.Length >= 4:
						width = BinaryPrimitives.ReadUInt16LittleEndian(body) * 8;
						height = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]) * 8;
						if (segment.Version >= 2 && body.Length >= 8 && BinaryPrimitives.ReadUInt16LittleEndian(body[6..]) != 0)
							throw new InvalidDataException("Movies in true color are not supported.");

						break;

					case 0x07:
						frames++;
						break;

					case 0x08 or 0x09 when audioFormat != null && pcm.Length < MaxPcmBytes:
						AppendAudio(pcm, body, segment.Opcode == 0x09, audioFormat, predictors);
						break;
				}
			}

			if (width <= 0 || height <= 0 || frames == 0)
				throw new InvalidDataException("The movie has no picture.");

			if ((Int64)width * height > MaxPixels)
				throw new InvalidDataException("The pictures of the movie are too large.");

			if (period <= 0 || period > MaxPeriod)
			{
				// No timer, or a damaged one: the movies of Fallout run at 10 images per second.
				period = 100_000;
			}

			AudioClip? audio = null;
			if (audioFormat != null && pcm.Length > 0)
			{
				Byte[] samples = pcm.ToArray();
				TimeSpan length = TimeSpan.FromSeconds(samples.Length / 2.0 / audioFormat.Channels / audioFormat.SampleRate);
				audio = new AudioClip(audioFormat.SampleRate, audioFormat.Channels, length, WavWriter.Create(samples, audioFormat.SampleRate, audioFormat.Channels), WavWriter.CreateEnvelope(samples, EnvelopeLength), incomplete);
			}

			return new MveMovie(data, segments, width, height, TimeSpan.FromTicks(period * 10), frames, audio, incomplete);
		}

		/// <summary>Lists the segments of all the chunks. Returns true when the data ends inside a chunk.</summary>
		private static Boolean ListSegments(Byte[] data, Int32 start, List<MveSegment> segments)
		{
			Int32 position = start;
			while (position + 4 <= data.Length)
			{
				Int32 chunkLength = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
				Int32 chunkType = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position + 2));
				position += 4;

				if (chunkType > 5)
					return true;

				Int32 chunkEnd = position + chunkLength;
				Boolean cut = chunkEnd > data.Length;
				if (cut)
					chunkEnd = data.Length;

				while (position + 4 <= chunkEnd)
				{
					Int32 length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(position));
					Byte opcode = data[position + 2];
					Byte version = data[position + 3];
					position += 4;

					if (position + length > chunkEnd)
						return true;

					if (opcode == 0x00)
						return false;

					segments.Add(new MveSegment(opcode, version, position, length));
					position += length;
				}

				position = chunkEnd;
				if (cut)
					return true;

				if (chunkType == 4 || chunkType == 5)
					return false;
			}

			return false;
		}

		private static AudioFormat ReadAudioFormat(ReadOnlySpan<Byte> body, Int32 version)
		{
			Int32 flags = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
			Int32 rate = BinaryPrimitives.ReadUInt16LittleEndian(body[4..]);
			if (rate < 4000)
				throw new InvalidDataException("The sound of the movie is damaged.");

			return new AudioFormat(rate, (flags & 1) + 1, (((flags >> 1) & 1) + 1) * 8, version == 1 && (flags & 4) != 0);
		}

		/// <summary>Adds one block of sound as 16 bit samples.</summary>
		private static void AppendAudio(MemoryStream pcm, ReadOnlySpan<Byte> body, Boolean silence, AudioFormat format, Int32[] predictors)
		{
			if (body.Length < 6)
				return;

			// Bit 0 of the mask is the first sound stream; the others (other languages) are not played.
			Int32 mask = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
			if (mask != 0 && (mask & 1) == 0)
				return;

			if (silence)
			{
				Int32 bytes = BinaryPrimitives.ReadUInt16LittleEndian(body[4..]);
				pcm.Write(new Byte[format.Bits == 8 ? bytes * 2 : bytes]);
				return;
			}

			ReadOnlySpan<Byte> payload = body[6..];
			if (format.IsCompressed)
			{
				DecodeDpcm(pcm, payload, format.Channels, predictors);
			}
			else if (format.Bits == 16)
			{
				pcm.Write(payload[..(payload.Length & ~1)]);
			}
			else
			{
				foreach (Byte value in payload)
				{
					Int32 sample = (value - 128) << 8;
					pcm.WriteByte((Byte)(sample & 0xFF));
					pcm.WriteByte((Byte)((sample >> 8) & 0xFF));
				}
			}
		}

		/// <summary>
		/// Decodes a compressed block: the first sample of each channel, then one byte per sample that is the change from
		/// the previous sample of the channel (the channels alternate).
		/// </summary>
		private static void DecodeDpcm(MemoryStream pcm, ReadOnlySpan<Byte> payload, Int32 channels, Int32[] predictors)
		{
			if (payload.Length < 2 * channels)
				return;

			Int32 position = 0;
			for (Int32 channel = 0; channel < channels; channel++)
			{
				predictors[channel] = BinaryPrimitives.ReadInt16LittleEndian(payload[position..]);
				position += 2;
				WriteSample(pcm, predictors[channel]);
			}

			Int32 current = 0;
			while (position < payload.Length)
			{
				predictors[current] = Math.Clamp(predictors[current] + DeltaTable[payload[position++]], Int16.MinValue, Int16.MaxValue);
				WriteSample(pcm, predictors[current]);
				current = channels == 2 ? current ^ 1 : 0;
			}
		}

		private static void WriteSample(MemoryStream pcm, Int32 sample)
		{
			pcm.WriteByte((Byte)(sample & 0xFF));
			pcm.WriteByte((Byte)((sample >> 8) & 0xFF));
		}

		private sealed record AudioFormat(Int32 SampleRate, Int32 Channels, Int32 Bits, Boolean IsCompressed);
	}

	/// <summary>One segment of a movie: what it is for, and where its data is.</summary>
	/// <param name="Opcode">What the segment holds (7: show the picture, 12: palette, 15: map of the blocks, 17: pictures...).</param>
	/// <param name="Version">The version of the format of the data.</param>
	/// <param name="Offset">Where the data starts in the file.</param>
	/// <param name="Length">The length of the data.</param>
	internal readonly record struct MveSegment(Byte Opcode, Byte Version, Int32 Offset, Int32 Length);
}