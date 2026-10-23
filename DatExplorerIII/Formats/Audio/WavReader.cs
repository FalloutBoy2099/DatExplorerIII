namespace DatExplorerIII.Formats.Audio
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using System.Text;
	using Models.Audio;

	/// <summary>
	/// Reads what the program needs to know about a WAV file to play it and to draw its shape.
	/// The file itself is played as it is.
	/// </summary>
	public static class WavReader
	{
		private const Int32 EnvelopeLength = 1000;

		/// <exception cref="InvalidDataException">The data is not a WAV file.</exception>
		public static AudioClip Read(Byte[] data)
		{
			if (data.Length < 12 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
				throw new InvalidDataException("This is not a WAV sound.");

			Int32 formatTag = 0;
			Int32 channels = 0;
			Int32 sampleRate = 0;
			Int32 bitsPerSample = 0;
			Int32 byteRate = 0;
			Int32 dataStart = -1;
			Int32 dataLength = 0;

			Int32 position = 12;
			while (position + 8 <= data.Length)
			{
				String id = Encoding.ASCII.GetString(data, position, 4);
				Int32 size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position + 4));
				Int32 body = position + 8;

				if (id == "fmt " && size >= 16 && body + 16 <= data.Length)
				{
					formatTag = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body));
					channels = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 2));
					sampleRate = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(body + 4));
					byteRate = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(body + 8));
					bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 14));
				}
				else if (id == "data")
				{
					dataStart = body;
					dataLength = size < 0 || body + (Int64)size > data.Length ? data.Length - body : size;
					break;
				}

				if (size < 0 || body + (Int64)size > data.Length)
					break;

				position = body + size + (size & 1);
			}

			if (channels == 0 || sampleRate == 0 || dataStart < 0 || byteRate <= 0)
				throw new InvalidDataException("The WAV file has no usable header.");

			TimeSpan duration = TimeSpan.FromSeconds((Double)dataLength / byteRate);

			// The shape can only be drawn from plain 16 bit samples.
			Byte[] envelope = formatTag == 1 && bitsPerSample == 16
				? WavWriter.CreateEnvelope(data.AsSpan(dataStart, dataLength - (dataLength & 1)).ToArray(), EnvelopeLength)
				: Array.Empty<Byte>();

			return new AudioClip(sampleRate, channels, duration, data, envelope, false);
		}
	}
}