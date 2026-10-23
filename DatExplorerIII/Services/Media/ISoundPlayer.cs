namespace DatExplorerIII.Services.Media
{
	using System;

	/// <summary>Plays a WAV file held in memory, so that the view models do not depend on the sound card.</summary>
	public interface ISoundPlayer : IDisposable
	{
		/// <summary>Plays the sound from its start, in place of what was playing.</summary>
		void Play(Byte[] wav);

		void Stop();
	}
}