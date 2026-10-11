namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The viewer of a sound: play and pause, a bar to move in it, and the sound that goes on from where the bar was left.</summary>
	public sealed class AudioViewerTests
	{
		/// <summary>A sound card that records what it is given.</summary>
		private sealed class FakePlayer : ISoundPlayer
		{
			public List<Byte[]> Played { get; } = new();

			public Int32 Stops { get; private set; }

			public void Play(Byte[] wav) => Played.Add(wav);

			public void Stop() => Stops++;

			public void Dispose()
			{
			}
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