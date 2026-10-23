namespace DatExplorerIII.Formats.Audio
{
	using System;
	using System.IO;
	using Models.Audio;
	using NLayer;

	/// <summary>
	/// Reads MP3 sounds and decodes them to a playable WAV, with the decoder NLayer (MIT license): the sound does not depend
	/// on a codec or a player of Windows.
	/// </summary>
	public static class Mp3Reader
	{
		private const Int32 EnvelopeLength = 1000;

		/// <summary>Decoded sound larger than this is cut (a 20 minute stereo sound at 44.1 kHz is about 211 MB).</summary>
		private const Int64 MaxPcmBytes = 256L * 1024 * 1024;

		/// <summary>How far from the start of the data the first frame is looked for: some files start with padding.</summary>
		private const Int32 SyncWindow = 16 * 1024;

		/// <summary>
		/// True when the data is an MP3 file: it starts with an ID3 tag, or an audio frame header is found near its start
		/// (the synchronization bits, then an MPEG version, a layer, a bit rate and a sample rate that exist).
		/// </summary>
		public static Boolean IsMp3(ReadOnlySpan<Byte> data)
		{
			if (data.Length >= 3 && data[0] == (Byte)'I' && data[1] == (Byte)'D' && data[2] == (Byte)'3')
				return true;

			Int32 last = Math.Min(data.Length - 4, SyncWindow);
			for (Int32 i = 0; i <= last; i++)
			{
				if (data[i] == 0xFF && (data[i + 1] & 0xE0) == 0xE0)
				{
					Int32 version = (data[i + 1] >> 3) & 3;
					Int32 layer = (data[i + 1] >> 1) & 3;
					Int32 bitRate = data[i + 2] >> 4;
					Int32 sampleRate = (data[i + 2] >> 2) & 3;
					if (version != 1 && layer != 0 && bitRate != 15 && bitRate != 0 && sampleRate != 3)
						return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Decodes a sound.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not an MP3 sound, or its first frames cannot be decoded.</exception>
		public static AudioClip Read(Byte[] data)
		{
			if (!IsMp3(data))
				throw new InvalidDataException("This is not an MP3 sound.");

			try
			{
				using MemoryStream stream = new(data);
				using MpegFile file = new(stream);
				Int32 rate = file.SampleRate;
				Int32 channels = file.Channels;
				if (rate < 4000 || channels is < 1 or > 2)
					throw new InvalidDataException("The sound is damaged.");

				using MemoryStream pcm = new();
				Single[] buffer = new Single[rate * channels / 4];
				Byte[] bytes = new Byte[buffer.Length * 2];
				Boolean incomplete = false;

				try
				{
					Int32 read;
					while ((read = file.ReadSamples(buffer, 0, buffer.Length)) > 0 && pcm.Length < MaxPcmBytes)
					{
						for (Int32 i = 0; i < read; i++)
						{
							Int32 sample = (Int32)Math.Round(Math.Clamp(buffer[i], -1f, 1f) * 32767);
							bytes[i * 2] = (Byte)(sample & 0xFF);
							bytes[(i * 2) + 1] = (Byte)((sample >> 8) & 0xFF);
						}

						pcm.Write(bytes, 0, read * 2);
					}
				}
				catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
				{
					// A damaged frame ends the sound; what was decoded before it can still be heard.
					incomplete = true;
				}

				if (pcm.Length == 0)
					throw new InvalidDataException("The sound contains no data.");

				Byte[] samples = pcm.ToArray();
				TimeSpan duration = TimeSpan.FromSeconds(samples.Length / 2.0 / channels / rate);
				return new AudioClip(rate, channels, duration, WavWriter.Create(samples, rate, channels), WavWriter.CreateEnvelope(samples, EnvelopeLength), incomplete);
			}
			catch (Exception exception) when (exception is not InvalidDataException && exception is InvalidOperationException or IOException or ArgumentException or NullReferenceException or NotSupportedException)
			{
				throw new InvalidDataException("This is not an MP3 sound that can be read.", exception);
			}
		}
	}
}