namespace DatExplorerIII.Formats.Audio
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models.Audio;

	/// <summary>
	/// Reads the small sounds of the interface of Arcanum (.snd, in tig.dat): the count of samples (32 bits), then the samples, mono, of
	/// 8 bits without sign. The file does not say how fast they are played: the rate of the sounds of the game is assumed.
	/// </summary>
	public static class SndReader
	{
		/// <summary>The rate (samples for a second) that the sounds are played at.</summary>
		public const Int32 SampleRate = 22050;

		private const Int32 HeaderSize = 4;

		/// <exception cref="InvalidDataException">The data is not a sound of the interface.</exception>
		public static AudioClip Read(Byte[] data)
		{
			if (data.Length <= HeaderSize)
				throw new InvalidDataException("This is not an SND sound.");

			Int64 count = BinaryPrimitives.ReadUInt32LittleEndian(data);
			if (count != data.Length - HeaderSize)
				throw new InvalidDataException("This is not an SND sound.");

			// The player and the shape of the sound work on 16 bit samples: the samples are made so.
			Byte[] pcm = new Byte[(data.Length - HeaderSize) * 2];
			for (Int32 i = 0; i < data.Length - HeaderSize; i++)
			{
				BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (Int16)((data[HeaderSize + i] - 128) << 8));
			}

			return WavReader.Read(WavWriter.Create(pcm, SampleRate, 1));
		}
	}
}
