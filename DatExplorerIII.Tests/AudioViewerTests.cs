namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using DatExplorerIII.Formats.Audio;
	using DatExplorerIII.Services.Media;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>The viewer of a sound: play and pause, a bar to move in it, and the sound that goes on from where the bar was left.</summary>
	public sealed class AudioViewerTests
	{
		/// <summary>A sound card that records what it is given.</summary>
		private sealed class FakePlayer : ISoundPlayer
		{
			public List<Byte[]> Played { get; } = [];

			public Int32 Stops { get; private set; }

			public void Play(Byte[] wav) => Played.Add(wav);

			public void Stop() => Stops++;

			public void Dispose()
			{ }
		}

		/// <summary>A clock that only moves when a test says so.</summary>
		private sealed class FakeClock : TimeProvider
		{
			private Int64 _ticks;

			public override Int64 TimestampFrequency => TimeSpan.TicksPerSecond;

			public override Int64 GetTimestamp() => _ticks;

			public void Advance(TimeSpan time) => _ticks += time.Ticks;
		}

		/// <summary>Four seconds of mono sound at 1000 samples per second: each sample holds its own number, so a slice can be told.</summary>
		private static Byte[] FourSeconds()
		{
			Byte[] pcm = new Byte[4000 * 2];
			for (Int32 i = 0; i < 4000; i++)
			{
				BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(i * 2), (Int16)i);
			}

			return WavWriter.Create(pcm, 1000, 1);
		}

		private static async Task<(AudioViewerViewModel Viewer, FakePlayer Player, FakeClock Clock, ManualUiThread Ui)> CreateAsync()
		{
			FakePlayer player = new();
			FakeClock clock = new();
			ManualUiThread ui = new();
			AudioViewerViewModel viewer = new(ui, player, clock);
			await viewer.ShowAsync(FourSeconds(), ".wav");
			return (viewer, player, clock, ui);
		}

		[Fact]
		public void Slice_KeepsTheFormat_AndStartsAtTheTimeAsked()
		{
			Byte[] wav = FourSeconds();

			Byte[] slice = WavWriter.Slice(wav, TimeSpan.FromSeconds(1.5))!;

			// 1500 samples are gone: the slice starts with the sample 1500 and has what is left (2500 samples).
			Assert.Equal(44 + (2500 * 2), slice.Length);
			Assert.Equal(1500, BinaryPrimitives.ReadInt16LittleEndian(slice.AsSpan(44)));
			Assert.Equal(3999, BinaryPrimitives.ReadInt16LittleEndian(slice.AsSpan(slice.Length - 2)));
			Assert.Equal(slice.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(slice.AsSpan(4)));
			Assert.Equal(2500 * 2, BinaryPrimitives.ReadInt32LittleEndian(slice.AsSpan(40)));

			// It is still a sound that can be read.
			Assert.Equal(TimeSpan.FromSeconds(2.5), WavReader.Read(slice).Duration);
			Assert.Same(wav, WavWriter.Slice(wav, TimeSpan.Zero));
			Assert.Null(WavWriter.Slice(wav, TimeSpan.FromSeconds(4)));
			Assert.Null(WavWriter.Slice(new Byte[10], TimeSpan.FromSeconds(1)));
		}

		[Fact]
		public async Task Playing_FollowsTheClock_AndPausingKeepsTheTimeReached()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, ManualUiThread ui) = await CreateAsync();
			ManualTimer timer = Assert.Single(ui.Created);
			Assert.Equal(4000, viewer.Maximum);
			Assert.Equal("0:00.0 / 0:04.0", viewer.PositionText);

			viewer.PlayStopCommand.Execute(null);
			Assert.True(viewer.IsPlaying);
			Assert.True(timer.IsRunning);
			Assert.Single(player.Played);

			clock.Advance(TimeSpan.FromSeconds(1.5));
			timer.Raise();
			Assert.Equal(1500, viewer.Position);
			Assert.Equal("0:01.5 / 0:04.0", viewer.PositionText);

			// Pausing stops the sound and the bar where they are; playing goes on from there, with the sound from there.
			viewer.PlayStopCommand.Execute(null);
			Assert.False(viewer.IsPlaying);
			Assert.False(timer.IsRunning);
			Assert.Equal(1500, viewer.Position);
			viewer.PlayStopCommand.Execute(null);
			Assert.Equal(2, player.Played.Count);
			Assert.Equal(1500, BinaryPrimitives.ReadInt16LittleEndian(player.Played[1].AsSpan(44)));
		}

		[Fact]
		public async Task AtTheEnd_TheSoundStops_AndTheBarGoesBackToTheBeginning()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, ManualUiThread ui) = await CreateAsync();
			ManualTimer timer = Assert.Single(ui.Created);
			viewer.PlayStopCommand.Execute(null);

			clock.Advance(TimeSpan.FromSeconds(4.1));
			timer.Raise();

			Assert.False(viewer.IsPlaying);
			Assert.Equal(0, viewer.Position);
			Assert.False(timer.IsRunning);

			// The next play starts from the beginning, with the whole sound.
			viewer.PlayStopCommand.Execute(null);
			Assert.Equal(FourSeconds().Length, player.Played[^1].Length);
		}

		[Fact]
		public async Task MovingTheBar_WhilePlaying_GoesOnFromThere_AndWhilePausedOnlyMovesTheBar()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, ManualUiThread ui) = await CreateAsync();

			// Paused: the bar moves, nothing plays.
			viewer.Position = 2000;
			Assert.Equal(2000, viewer.Position);
			Assert.Empty(player.Played);

			// Playing: a click on the bar goes on from the place that was clicked.
			viewer.PlayStopCommand.Execute(null);
			Assert.Equal(2000, BinaryPrimitives.ReadInt16LittleEndian(player.Played[0].AsSpan(44)));
			clock.Advance(TimeSpan.FromSeconds(0.5));
			viewer.Position = 500;
			Assert.True(viewer.IsPlaying);
			Assert.Equal(2, player.Played.Count);
			Assert.Equal(500, BinaryPrimitives.ReadInt16LittleEndian(player.Played[1].AsSpan(44)));

			// A position out of the sound is brought back into it; the end of the sound ends it.
			viewer.Position = 99999;
			Assert.Equal(4000, viewer.Position);
			Assert.False(viewer.IsPlaying);
			Assert.Equal(2, player.Played.Count);
			Assert.Single(ui.Created);
		}

		[Fact]
		public async Task HoldingTheBar_PausesTheSound_AndLettingGoResumesIt()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, _) = await CreateAsync();
			viewer.PlayStopCommand.Execute(null);
			clock.Advance(TimeSpan.FromSeconds(1));

			viewer.BeginSeek();
			Assert.False(viewer.IsPlaying);
			Assert.Equal(1000, viewer.Position);

			// While the bar is held it moves without any sound.
			viewer.Position = 3000;
			Assert.Single(player.Played);
			Assert.False(viewer.IsPlaying);

			viewer.EndSeek();
			Assert.True(viewer.IsPlaying);
			Assert.Equal(2, player.Played.Count);
			Assert.Equal(3000, BinaryPrimitives.ReadInt16LittleEndian(player.Played[1].AsSpan(44)));
		}

		private static Int16 SampleAt(Byte[] wav, Int32 index) => BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(44 + (index * 2)));

		[Fact]
		public void ApplyGain_ScalesThePlainSamples_WithoutTouchingTheOriginal()
		{
			Byte[] wav = FourSeconds();

			Byte[] half = WavWriter.ApplyGain(wav, 0.5);

			Assert.NotSame(wav, half);
			Assert.Equal(2000, SampleAt(wav, 2000));
			Assert.Equal(1000, SampleAt(half, 2000));
			Assert.Equal(wav.Length, half.Length);
			Assert.Equal(0, SampleAt(WavWriter.ApplyGain(wav, 0), 3000));

			// Full volume (or more) and what is not plain samples are returned as they are.
			Assert.Same(wav, WavWriter.ApplyGain(wav, 1));
			Byte[] other = (Byte[])wav.Clone();
			BinaryPrimitives.WriteInt16LittleEndian(other.AsSpan(20), 2);
			Assert.False(WavWriter.IsPcm16(other));
			Assert.Same(other, WavWriter.ApplyGain(other, 0.5));
			Assert.True(WavWriter.IsPcm16(wav));
			Assert.False(WavWriter.IsPcm16(new Byte[5]));
		}

		[Fact]
		public async Task Volume_IsInTheSoundThatPlays_AndChangesItAtOnce()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, _) = await CreateAsync();
			Assert.True(viewer.CanChangeVolume);
			Assert.Equal(100, viewer.Volume);

			viewer.Volume = 50;
			viewer.PlayStopCommand.Execute(null);
			Assert.Equal(1000, SampleAt(player.Played[0], 2000));

			// A change while it plays: from where it is, with the new volume.
			clock.Advance(TimeSpan.FromSeconds(1));
			viewer.Volume = 25;
			Assert.True(viewer.IsPlaying);
			Assert.Equal(2, player.Played.Count);
			Assert.Equal(1000 / 4, SampleAt(player.Played[1], 0));

			// The volume stays between 0 and 100.
			viewer.Volume = 500;
			Assert.Equal(100, viewer.Volume);
			viewer.Volume = -5;
			Assert.Equal(0, viewer.Volume);
		}

		[Fact]
		public async Task ASoundThatIsNotPlainSamples_HasNoVolume()
		{
			Byte[] wav = FourSeconds();
			BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 2);
			AudioViewerViewModel viewer = new(new ManualUiThread(), new FakePlayer(), new FakeClock());

			await viewer.ShowAsync(wav, ".wav");

			Assert.True(viewer.HasClip);
			Assert.False(viewer.CanChangeVolume);
		}

		[Fact]
		public async Task Looping_StartsTheSoundAgainWhenItEnds()
		{
			(AudioViewerViewModel viewer, FakePlayer player, FakeClock clock, ManualUiThread ui) = await CreateAsync();
			ManualTimer timer = Assert.Single(ui.Created);
			viewer.IsLooping = true;
			viewer.PlayStopCommand.Execute(null);

			clock.Advance(TimeSpan.FromSeconds(4.2));
			timer.Raise();

			Assert.True(viewer.IsPlaying);
			Assert.Equal(0, viewer.Position);
			Assert.Equal(2, player.Played.Count);

			// Without it the sound stops.
			viewer.IsLooping = false;
			clock.Advance(TimeSpan.FromSeconds(4.2));
			timer.Raise();
			Assert.False(viewer.IsPlaying);
		}

		[Fact]
		public async Task TheMarkOnTheShape_FollowsThePosition()
		{
			(AudioViewerViewModel viewer, _, _, _) = await CreateAsync();
			List<String?> changes = [];
			viewer.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
			Assert.Equal(0, viewer.PlayedFraction);

			viewer.Position = 1000;

			Assert.Equal(0.25, viewer.PlayedFraction, 3);
			Assert.Contains(nameof(AudioViewerViewModel.PlayedFraction), changes);
			viewer.Position = 4000;
			Assert.Equal(1, viewer.PlayedFraction);
		}

		[Fact]
		public async Task ASoundThatIsNotThere_HasNoBarToMove()
		{
			AudioViewerViewModel viewer = new(new ManualUiThread(), new FakePlayer(), new FakeClock());
			viewer.Position = 500;
			Assert.Equal(0, viewer.Position);
			Assert.False(viewer.PlayStopCommand.CanExecute(null));

			await viewer.ShowAsync(FourSeconds(), ".wav");
			Assert.True(viewer.PlayStopCommand.CanExecute(null));

			// Another file: the bar is back at the beginning.
			viewer.Position = 700;
			viewer.Clear();
			Assert.Equal(0, viewer.Position);
			Assert.Equal(String.Empty, viewer.PositionText);
		}
	}
}