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