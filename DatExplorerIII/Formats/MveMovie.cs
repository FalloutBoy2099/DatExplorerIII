namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Models;

	/// <summary>
	/// A movie (.mve) that was read: its size, its speed, its sound, and what is needed to decode its pictures.
	/// </summary>
	public sealed class MveMovie
	{
		/// <summary>The distance in pictures between two saved states of the decoding, that make jumping in a movie quick.</summary>
		public const Int32 CheckpointInterval = 100;

		internal MveMovie(Byte[] data, IReadOnlyList<MveSegment> segments, Int32 width, Int32 height, TimeSpan framePeriod, Int32 frameCount, AudioClip? audio, Boolean isIncomplete)
		{
			Data = data;
			Segments = segments;
			Width = width;
			Height = height;
			FramePeriod = framePeriod;
			FrameCount = frameCount;
			Audio = audio;
			IsIncomplete = isIncomplete;
		}

		public Int32 Width { get; }

		public Int32 Height { get; }

		/// <summary>The time between two pictures.</summary>
		public TimeSpan FramePeriod { get; }

		public Int32 FrameCount { get; }

		public Double FramesPerSecond => 1 / FramePeriod.TotalSeconds;

		public TimeSpan Duration => FramePeriod * FrameCount;

		/// <summary>The sound of the movie, or null when it has none.</summary>
		public AudioClip? Audio { get; }

		/// <summary>True when the data ended before the end of the movie.</summary>
		public Boolean IsIncomplete { get; }

		internal MveCheckpoints Checkpoints { get; } = new();

		internal Byte[] Data { get; }

		internal IReadOnlyList<MveSegment> Segments { get; }

		/// <summary>
		/// Decodes the whole movie to save the states of the decoding that let a <see cref="MveFrameReader"/> jump to any
		/// picture quickly (about 1.5 ms a picture for the whole movie). Meant to be run on another thread; it stops when the
		/// token is cancelled, and what was saved is kept.
		/// </summary>
		public void BuildCheckpoints(CancellationToken token)
		{
			MveFrameReader reader = CreateFrameReader();
			while (!token.IsCancellationRequested && reader.ReadFrame(null))
			{
			}
		}

		/// <summary>The sound from a moment of the movie, as a WAV file; null when there is no sound at that time.</summary>
		public Byte[]? CreateAudioFrom(TimeSpan start)
		{
			if (Audio == null)
			{
				return null;
			}

			return WavWriter.Slice(Audio.WavData, start);
		}

		/// <summary>Starts decoding the pictures from the first one.</summary>
		public MveFrameReader CreateFrameReader()
		{
			return new MveFrameReader(this);
		}
	}
}