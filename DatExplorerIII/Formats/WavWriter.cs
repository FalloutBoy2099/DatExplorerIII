namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;
	using System.Text;

	/// <summary>
	/// Builds WAV files and the shape of a sound from 16 bit samples.
	/// </summary>
	internal static class WavWriter
	{
		private const Int32 HeaderSize = 44;

		/// <summary>A complete WAV file (16 bit PCM) around the samples.</summary>
		public static Byte[] Create(Byte[] pcm, Int32 sampleRate, Int32 channels)
		{
			Byte[] wav = new Byte[HeaderSize + pcm.Length];
			Span<Byte> header = wav;

			Encoding.ASCII.GetBytes("RIFF").CopyTo(header);
			BinaryPrimitives.WriteInt32LittleEndian(header[4..], 36 + pcm.Length);
			Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(header[8..]);
			BinaryPrimitives.WriteInt32LittleEndian(header[16..], 16);
			BinaryPrimitives.WriteInt16LittleEndian(header[20..], 1);
			BinaryPrimitives.WriteInt16LittleEndian(header[22..], (Int16)channels);
			BinaryPrimitives.WriteInt32LittleEndian(header[24..], sampleRate);
			BinaryPrimitives.WriteInt32LittleEndian(header[28..], sampleRate * channels * 2);
			BinaryPrimitives.WriteInt16LittleEndian(header[32..], (Int16)(channels * 2));
			BinaryPrimitives.WriteInt16LittleEndian(header[34..], 16);
			Encoding.ASCII.GetBytes("data").CopyTo(header[36..]);
			BinaryPrimitives.WriteInt32LittleEndian(header[40..], pcm.Length);

			pcm.CopyTo(wav, HeaderSize);
			return wav;
		}

		/// <summary>
		/// The same sound from <paramref name="start"/> on, as a complete WAV file (the format of the sound is kept). Null when the
		/// sound ends before that, or when its header cannot be read.
		/// </summary>
		public static Byte[]? Slice(Byte[] wav, TimeSpan start)
		{
			if (start <= TimeSpan.Zero)
			{
				return wav;
			}

			Int32 blockAlign = 0;
			Int32 byteRate = 0;
			Int32 dataStart = -1;
			Int32 dataLength = 0;
			Int32 position = 12;
			while (position + 8 <= wav.Length)
			{
				Int32 size = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(position + 4));
				Int32 body = position + 8;
				String id = Encoding.ASCII.GetString(wav, position, 4);
				if (id == "fmt " && size >= 16 && body + 16 <= wav.Length)
				{
					byteRate = BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(body + 8));
					blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(wav.AsSpan(body + 12));
				}
				else if (id == "data")
				{
					dataStart = body;
					dataLength = size < 0 || body + (Int64)size > wav.Length ? wav.Length - body : size;
					break;
				}

				if (size < 0 || body + (Int64)size > wav.Length)
				{
					break;
				}

				position = body + size + (size & 1);
			}

			if (blockAlign <= 0 || byteRate <= 0 || dataStart < 12 || wav.Length < 12)
			{
				return null;
			}

			// A whole number of blocks: a sample is never cut in two.
			Int64 offset = (Int64)Math.Round(start.TotalSeconds * byteRate);
			offset -= offset % blockAlign;
			Int64 remaining = dataLength - offset;
			if (remaining <= 0)
			{
				return null;
			}

			Byte[] result = new Byte[dataStart + remaining];
			Array.Copy(wav, 0, result, 0, dataStart);
			Array.Copy(wav, dataStart + offset, result, dataStart, remaining);
			BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), result.Length - 8);
			BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(dataStart - 4), (Int32)remaining);
			return result;
		}

		/// <summary>The loudest sample of each of <paramref name="length"/> slices of the sound, from 0 to 255.</summary>
		public static Byte[] CreateEnvelope(Byte[] pcm, Int32 length)
		{
			Int32 samples = pcm.Length / 2;
			Int32 slices = Math.Min(length, samples);
			Byte[] envelope = new Byte[slices];

			for (Int32 slice = 0; slice < slices; slice++)
			{
				Int32 start = (Int32)((Int64)slice * samples / slices);
				Int32 end = (Int32)((Int64)(slice + 1) * samples / slices);
				Int32 peak = 0;

				for (Int32 i = start; i < end; i++)
				{
					Int32 value = Math.Abs((Int32)BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(i * 2)));
					if (value > peak)
					{
						peak = value;
					}
				}

				envelope[slice] = (Byte)Math.Min(255, peak * 255 / 32767);
			}

			return envelope;
		}
	}
}