namespace DatExplorerIII.Services.Media
{
	using System;
	using System.IO;
	using System.Media;

	/// <summary>
	/// Plays a WAV file held in memory, with the sound card of the computer.
	/// </summary>
	public sealed class WavPlayer : ISoundPlayer
	{
		private SoundPlayer? _player;
		private MemoryStream? _stream;

		public void Play(Byte[] wav)
		{
			Stop();

			_stream = new MemoryStream(wav);
			_player = new SoundPlayer(_stream);
			_player.Play();
		}

		public void Stop()
		{
			_player?.Stop();
			_player?.Dispose();
			_stream?.Dispose();
			_player = null;
			_stream = null;
		}

		public void Dispose()
		{
			Stop();
		}
	}
}