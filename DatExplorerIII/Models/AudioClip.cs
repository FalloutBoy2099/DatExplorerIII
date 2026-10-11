namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A sound ready to be played: a complete WAV file in memory and what is known about it.
	/// </summary>
	/// <param name="SampleRate">Samples per second and channel.</param>
	/// <param name="Channels">1 for mono, 2 for stereo.</param>
	/// <param name="Duration">Length of the sound.</param>
	/// <param name="WavData">The sound as a WAV file.</param>
	/// <param name="Envelope">The loudness along the sound, 0 to 255, for drawing its shape (empty when unknown).</param>
	/// <param name="IsIncomplete">True when the data ended before the announced length.</param>
	public sealed record AudioClip(Int32 SampleRate, Int32 Channels, TimeSpan Duration, Byte[] WavData, Byte[] Envelope, Boolean IsIncomplete);
}