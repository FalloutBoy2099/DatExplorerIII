namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Formats;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>
	/// A movie shown in the viewer: the picture, a button to play or pause it with its sound, a bar to move in it, and its
	/// description. The pictures are decoded as the movie plays.
	/// </summary>
	public sealed class VideoViewerViewModel : ObservableObject, ISeekable, IDisposable
	{
		private readonly WavPlayer _player = new();
		private readonly IUiTimer _timer;
		private readonly Stopwatch _clock = new();

		private CancellationTokenSource? _building;
		private MveMovie? _movie;
		private MveFrameReader? _reader;
		private Byte[] _pixels = Array.Empty<Byte>();
		private PixelImage? _frame;
		private Int32 _shown;
		private TimeSpan _clockStart;
		private Boolean _isPlaying;
		private Boolean _isSeeking;
		private Boolean _resumeAfterSeek;
		private String _info = String.Empty;
		private String _warning = String.Empty;
		private SveFile? _subtitles;
		private Boolean _showSubtitles = true;
		private String _subtitle = String.Empty;

		public VideoViewerViewModel(IUiThread uiThread)
		{
			_timer = uiThread.CreateTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(10);
			_timer.Tick += (_, _) => OnTick();
			PlayStopCommand = new RelayCommand(PlayOrPause, () => _movie != null);
		}

		public ICommand PlayStopCommand { get; }

		public Boolean HasMovie => _movie != null;

		/// <summary>The picture of the movie that is shown; its pixels change as the movie plays.</summary>
		public PixelImage? Frame
		{
			get => _frame;
			private set => SetProperty(ref _frame, value);
		}

		public String Info
		{
			get => _info;
			private set => SetProperty(ref _info, value);
		}

		public String Warning
		{
			get => _warning;
			private set => SetProperty(ref _warning, value);
		}

		public Boolean IsPlaying
		{
			get => _isPlaying;
			private set => SetProperty(ref _isPlaying, value);
		}

		/// <summary>Whether the subtitles of the movie (its .sve file) are shown over the picture, when it has some.</summary>
		public Boolean ShowSubtitles
		{
			get => _showSubtitles;
			set
			{
				if (SetProperty(ref _showSubtitles, value))
				{
					UpdateSubtitle();
				}
			}
		}

		/// <summary>The subtitle of the picture that is shown; empty when there is none to show.</summary>
		public String Subtitle
		{
			get => _subtitle;
			private set
			{
				if (SetProperty(ref _subtitle, value))
				{
					OnPropertyChanged(nameof(HasSubtitle));
				}
			}
		}

		public Boolean HasSubtitle => _subtitle.Length > 0;

		/// <summary>The number of the last picture: the end of the position bar.</summary>
		public Double LastFrame => Math.Max(1, (_movie?.FrameCount ?? 1) - 1);

		/// <summary>The picture shown, from 0. Setting it jumps to that picture.</summary>
		public Double Position
		{
			get => _shown;
			set
			{
				Int32 frame = (Int32)Math.Round(value);
				if (_movie != null && frame != _shown)
				{
					SeekTo(frame);
				}
			}
		}

		/// <summary>The time of the picture and the length of the movie.</summary>
		public String PositionText => _movie == null ? String.Empty : $"{FormatDuration(_movie.FramePeriod * _shown)} / {FormatDuration(_movie.Duration)}";

		/// <summary>Shows the first picture of a movie.</summary>
		/// <param name="movie">The movie.</param>
		/// <param name="subtitles">Its subtitles (the .sve file of the same name), when it has some.</param>
		/// <param name="subtitlesName">Where the subtitles come from, to tell it.</param>
		public void Show(MveMovie movie, SveFile? subtitles = null, String? subtitlesName = null)
		{
			Reset();
			_movie = movie;
			_subtitles = subtitles;
			_reader = movie.CreateFrameReader();
			_pixels = new Byte[_reader.FrameBytes];
			Frame = new PixelImage(movie.Width, movie.Height, _pixels);

			ShowFrame(0);
			Info = Loc.Format(
				"Str.ViewerVideoInfo",
				movie.Width,
				movie.Height,
				movie.FrameCount.ToString("N0", CultureInfo.CurrentCulture),
				movie.FramesPerSecond.ToString("0.#", CultureInfo.CurrentCulture),
				FormatDuration(movie.Duration));
			if (subtitles != null)
			{
				Info += " · " + Loc.Format("Str.ViewerVideoSubtitles", subtitles.Entries.Count, subtitlesName ?? String.Empty);
			}

			Warning = movie.IsIncomplete ? Loc.Get("Str.ViewerVideoIncomplete") : movie.Audio == null ? Loc.Get("Str.ViewerVideoSilent") : String.Empty;
			UpdateSubtitle();
			OnPropertyChanged(nameof(HasMovie));
			OnPropertyChanged(nameof(LastFrame));
			CommandRequery.Invalidate();

			// Jumping needs the states of the decoding that are saved as the movie is decoded: made in the background.
			_building = new CancellationTokenSource();
			CancellationToken token = _building.Token;
			_ = Task.Run(() => movie.BuildCheckpoints(token), token);
		}

		public void Clear()
		{
			Reset();
		}

		/// <summary>Pauses the movie (when another file is shown, for example).</summary>
		public void Stop()
		{
			Pause();
		}

		/// <summary>The user takes the bar: the movie waits while the pictures follow the bar.</summary>
		public void BeginSeek()
		{
			_isSeeking = true;
			_resumeAfterSeek = _isPlaying;
			Pause();
		}

		/// <summary>The user lets go of the bar: the movie goes on from there if it was playing.</summary>
		public void EndSeek()
		{
			_isSeeking = false;
			if (_resumeAfterSeek)
			{
				_resumeAfterSeek = false;
				Play();
			}
		}

		public void Dispose()
		{
			_building?.Cancel();
			_building?.Dispose();
			_building = null;
			_timer.Stop();
			_player.Dispose();
		}

		private void Reset()
		{
			_building?.Cancel();
			_building = null;
			_timer.Stop();
			_clock.Reset();
			_player.Stop();
			IsPlaying = false;
			_isSeeking = false;
			_resumeAfterSeek = false;
			_movie = null;
			_subtitles = null;
			Subtitle = String.Empty;
			_reader = null;
			_pixels = Array.Empty<Byte>();
			_shown = 0;
			Frame = null;
			Info = String.Empty;
			Warning = String.Empty;
			OnPropertyChanged(nameof(HasMovie));
			OnPropertyChanged(nameof(Position));
			OnPropertyChanged(nameof(PositionText));
		}

		/// <summary>Decodes and shows a picture.</summary>
		private void ShowFrame(Int32 frame)
		{
			if (_reader == null)
			{
				return;
			}

			_reader.SeekTo(frame);
			if (_reader.ReadFrame(_pixels))
			{
				_shown = _reader.FramePosition - 1;
				_frame?.Invalidate();
			}

			OnPropertyChanged(nameof(Position));
			OnPropertyChanged(nameof(PositionText));
			UpdateSubtitle();
		}

		/// <summary>The subtitle that was shown last at the picture: the one of the latest line that is not after it.</summary>
		private void UpdateSubtitle()
		{
			if (!_showSubtitles || _subtitles == null)
			{
				Subtitle = String.Empty;
				return;
			}

			SveEntry? current = null;
			foreach (SveEntry entry in _subtitles.Entries)
			{
				if (entry.Frame > _shown)
				{
					break;
				}

				current = entry;
			}

			Subtitle = current?.Text ?? String.Empty;
		}

		private void SeekTo(Int32 frame)
		{
			ShowFrame(frame);

			// Not dragging the bar, but playing: the sound and the clock go on from the new place.
			if (_isPlaying && !_isSeeking)
			{
				Pause();
				Play();
			}
		}

		private void PlayOrPause()
		{
			if (_isPlaying)
			{
				Pause();
			}
			else
			{
				Play();
			}
		}

		private void Play()
		{
			if (_movie == null || _reader == null || _isPlaying)
			{
				return;
			}

			if (_shown >= _movie.FrameCount - 1)
			{
				ShowFrame(0);
			}

			Warning = _movie.IsIncomplete ? Loc.Get("Str.ViewerVideoIncomplete") : String.Empty;
			_clockStart = _movie.FramePeriod * _shown;

			Byte[]? sound = _movie.CreateAudioFrom(_clockStart);
			if (sound != null)
			{
				try
				{
					_player.Play(sound);
				}
				catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or TimeoutException or Win32Exception)
				{
					Warning = Loc.Get("Str.ViewerCannotPlay");
				}
			}

			IsPlaying = true;
			_clock.Restart();
			_timer.Start();
		}

		private void Pause()
		{
			_timer.Stop();
			_clock.Reset();
			_player.Stop();
			IsPlaying = false;
		}

		/// <summary>Shows the picture that belongs to the time elapsed; pictures that are late are skipped over.</summary>
		private void OnTick()
		{
			if (_reader == null || _movie == null)
			{
				return;
			}

			Int32 target = (Int32)((_clockStart + _clock.Elapsed).Ticks / _movie.FramePeriod.Ticks);
			if (target <= _shown)
			{
				return;
			}

			if (target >= _movie.FrameCount)
			{
				// The end: back to the first picture.
				Pause();
				ShowFrame(0);
				return;
			}

			// The next pictures are decoded without making the images that nobody sees.
			while (_reader.FramePosition < target)
			{
				if (!_reader.ReadFrame(null))
				{
					Pause();
					ShowFrame(0);
					return;
				}
			}

			ShowFrame(target);
		}

		private static String FormatDuration(TimeSpan duration)
		{
			return duration.TotalHours >= 1
				? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
				: duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
		}
	}
}