namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Formats;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>The number of channels to decode a sound with; 0 follows the header of the file.</summary>
	public sealed record ChannelChoice(String Display, Int32 Channels)
	{
		public override String ToString() => Display;
	}

	/// <summary>
	/// A sound shown in the viewer: its shape, a button to play or pause it, a bar to move in it, and its description.
	/// </summary>
	public sealed class AudioViewerViewModel : ObservableObject, ISeekable, IDisposable
	{
		/// <summary>The sound is that close to its end (or closer): playing it again starts from the beginning.</summary>
		private static readonly TimeSpan EndMargin = TimeSpan.FromMilliseconds(50);

		private readonly ISoundPlayer _player;
		private readonly IUiTimer _timer;
		private readonly TimeProvider _time;

		private Byte[] _source = Array.Empty<Byte>();
		private String _extension = String.Empty;
		private AudioClip? _clip;
		private Byte[] _envelope = Array.Empty<Byte>();
		private String _info = String.Empty;
		private String _warning = String.Empty;
		private Boolean _isPlaying;
		private IReadOnlyList<ChannelChoice> _channelChoices;
		private ChannelChoice _selectedChannels;
		private Int32 _generation;
		private TimeSpan _position;
		private TimeSpan _clockStart;
		private Int64 _clockTimestamp;
		private Boolean _isSeeking;
		private Boolean _resumeAfterSeek;
		private Double _volume = 100;
		private Boolean _isLooping;

		/// <param name="uiThread">Gives the timer that follows the sound as it plays.</param>
		/// <param name="player">What plays the sound (the sound card of the computer when null).</param>
		/// <param name="time">The clock that tells how far the sound has played (the system clock when null).</param>
		public AudioViewerViewModel(IUiThread uiThread, ISoundPlayer? player = null, TimeProvider? time = null)
		{
			_player = player ?? new WavPlayer();
			_time = time ?? TimeProvider.System;
			_timer = uiThread.CreateTimer();
			_timer.Interval = TimeSpan.FromMilliseconds(50);
			_channelChoices = CreateChannelChoices();
			_selectedChannels = _channelChoices[0];
			_timer.Tick += (_, _) => OnTick();
			PlayStopCommand = new RelayCommand(PlayOrPause, () => _clip != null);
		}

		public ICommand PlayStopCommand { get; }

		public Boolean HasClip => _clip != null;

		/// <summary>The channel choice only applies to ACM sounds, whose header is often wrong.</summary>
		public Boolean CanChooseChannels => IsAcm && _clip != null;

		private Boolean IsAcm => _extension.Equals(".acm", StringComparison.OrdinalIgnoreCase);

		public IReadOnlyList<ChannelChoice> ChannelChoices
		{
			get => _channelChoices;
			private set => SetProperty(ref _channelChoices, value);
		}

		/// <summary>True while the list of choices is replaced: the box that shows it then empties its selection, which is not a choice.</summary>
		private Boolean _refreshingLanguage;

		public ChannelChoice SelectedChannels
		{
			get => _selectedChannels;
			set
			{
				if (_refreshingLanguage || value is null)
				{
					return;
				}

				if (SetProperty(ref _selectedChannels, value) && IsAcm && _source.Length > 0)
				{
					_ = DecodeAsync();
				}
			}
		}

		/// <summary>The loudness along the sound, 0 to 255 (the view draws the shape of the sound from it); empty when unknown.</summary>
		public Byte[] Envelope
		{
			get => _envelope;
			private set => SetProperty(ref _envelope, value);
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

		/// <summary>The volume, 0 to 100. It only applies to sounds made of plain 16 bit samples (<see cref="CanChangeVolume"/>).</summary>
		public Double Volume
		{
			get => _volume;
			set
			{
				if (SetProperty(ref _volume, Math.Clamp(value, 0, 100)) && _isPlaying && CanChangeVolume)
				{
					// The sound that plays has the old volume: it goes on from where it is with the new one.
					Pause();
					Play();
				}
			}
		}

		/// <summary>Whether the sound that is shown can be played louder or softer.</summary>
		public Boolean CanChangeVolume => _clip != null && WavWriter.IsPcm16(_clip.WavData);

		/// <summary>Whether the sound starts again when it ends.</summary>
		public Boolean IsLooping
		{
			get => _isLooping;
			set => SetProperty(ref _isLooping, value);
		}

		/// <summary>How far the sound has played, 0 to 1 (the mark on its shape).</summary>
		public Double PlayedFraction => _clip == null || _clip.Duration <= TimeSpan.Zero ? 0 : Math.Clamp(_position.TotalMilliseconds / _clip.Duration.TotalMilliseconds, 0, 1);

		/// <summary>The length of the sound in milliseconds: the end of the position bar.</summary>
		public Double Maximum => Math.Max(1, _clip?.Duration.TotalMilliseconds ?? 1);

		/// <summary>Where the sound is, in milliseconds. Setting it goes there, and the sound goes on from there if it was playing.</summary>
		public Double Position
		{
			get => _position.TotalMilliseconds;
			set
			{
				if (_clip == null)
				{
					return;
				}

				TimeSpan target = TimeSpan.FromMilliseconds(Math.Clamp(value, 0, Maximum));
				if (target == _position)
				{
					return;
				}

				SetPosition(target);
				if (_isPlaying && !_isSeeking)
				{
					// Going to the end ends the sound; anywhere else it goes on from there.
					StopPlayback();
					if (target < _clip.Duration - EndMargin)
					{
						Play();
					}
				}
			}
		}

		/// <summary>The time reached and the length of the sound.</summary>
		public String PositionText => _clip == null ? String.Empty : $"{FormatDuration(_position)} / {FormatDuration(_clip.Duration)}";

		/// <summary>Decodes and shows a sound.</summary>
		/// <param name="data">The content of the sound file.</param>
		/// <param name="extension">The extension of the file (.acm, .wav or .mp3), which tells how it is decoded.</param>
		public async Task ShowAsync(Byte[] data, String extension)
		{
			Reset();
			_source = data;
			_extension = extension;
			_selectedChannels = _channelChoices[0];
			OnPropertyChanged(nameof(SelectedChannels));
			await DecodeAsync();
		}

		public void Clear()
		{
			Reset();
		}

		/// <summary>Pauses the sound (when another file is shown, for example).</summary>
		public void Stop()
		{
			Pause();
		}

		/// <summary>The user takes the bar: the sound waits while the bar moves.</summary>
		public void BeginSeek()
		{
			_isSeeking = true;
			_resumeAfterSeek = _isPlaying;
			Pause();
		}

		/// <summary>The user lets go of the bar: the sound goes on from there if it was playing.</summary>
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
			Stop();
			_player.Dispose();
		}

		/// <summary>Names the channel choices again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			Int32 channels = _selectedChannels.Channels;
			IReadOnlyList<ChannelChoice> choices = CreateChannelChoices();

			_refreshingLanguage = true;
			try
			{
				_selectedChannels = choices[channels];
				ChannelChoices = choices;
			}
			finally
			{
				_refreshingLanguage = false;
			}

			OnPropertyChanged(nameof(SelectedChannels));
		}

		private static IReadOnlyList<ChannelChoice> CreateChannelChoices()
		{
			return
			[
				new ChannelChoice(Loc.Get("Str.ChannelsAuto"), 0),
				new ChannelChoice(Loc.Get("Str.ChannelsMono"), 1),
				new ChannelChoice(Loc.Get("Str.ChannelsStereo"), 2),
			];
		}

		private void Reset()
		{
			_generation++;
			Stop();
			_source = Array.Empty<Byte>();
			_extension = String.Empty;
			_clip = null;
			_isSeeking = false;
			_resumeAfterSeek = false;
			Envelope = Array.Empty<Byte>();
			Info = String.Empty;
			Warning = String.Empty;
			SetPosition(TimeSpan.Zero);
			OnPropertyChanged(nameof(Maximum));
			OnPropertyChanged(nameof(HasClip));
			OnPropertyChanged(nameof(CanChooseChannels));
			OnPropertyChanged(nameof(CanChangeVolume));
		}

		private async Task DecodeAsync()
		{
			Stop();
			Int32 generation = ++_generation;
			Byte[] source = _source;
			String extension = _extension;
			Int32 channels = _selectedChannels.Channels;

			try
			{
				AudioClip clip = await Task.Run(() => Decode(source, extension, channels));
				if (generation != _generation)
				{
					return;
				}

				_clip = clip;
				SetPosition(TimeSpan.Zero);
				OnPropertyChanged(nameof(Maximum));
				Envelope = clip.Envelope;
				Info = Loc.Format(
					"Str.ViewerAudioInfo",
					clip.SampleRate.ToString("N0", CultureInfo.CurrentCulture),
					clip.Channels == 1 ? Loc.Get("Str.ChannelsMono") : Loc.Get("Str.ChannelsStereo"),
					FormatDuration(clip.Duration));
				Warning = clip.IsIncomplete ? Loc.Get("Str.ViewerAudioIncomplete") : String.Empty;
			}
			catch (Exception exception) when (exception is InvalidDataException or ArgumentException or IndexOutOfRangeException or OverflowException or NotSupportedException or InvalidOperationException)
			{
				// A damaged sound: the decoders do not all say so in the same way.
				if (exception is not InvalidDataException)
				{
					ErrorLog.Write(exception);
				}

				if (generation == _generation)
				{
					_clip = null;
					SetPosition(TimeSpan.Zero);
					OnPropertyChanged(nameof(Maximum));
					Envelope = Array.Empty<Byte>();
					Info = String.Empty;
					Warning = Loc.Get("Str.ViewerInvalid");
				}
			}

			OnPropertyChanged(nameof(HasClip));
			OnPropertyChanged(nameof(CanChooseChannels));
			OnPropertyChanged(nameof(CanChangeVolume));
			CommandRequery.Invalidate();
		}

		private static AudioClip Decode(Byte[] source, String extension, Int32 channels)
		{
			return extension.ToLowerInvariant() switch
			{
				".acm" => AcmReader.Read(source, channels),
				".mp3" => Mp3Reader.Read(source),
				_ => WavReader.Read(source),
			};
		}

		private static String FormatDuration(TimeSpan duration)
		{
			return duration.TotalHours >= 1
				? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
				: duration.ToString(@"m\:ss\.f", CultureInfo.InvariantCulture);
		}

		private void SetPosition(TimeSpan position)
		{
			_position = position;
			OnPropertyChanged(nameof(Position));
			OnPropertyChanged(nameof(PositionText));
			OnPropertyChanged(nameof(PlayedFraction));
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

		/// <summary>Plays from the position; from the beginning when the sound is at its end.</summary>
		private void Play()
		{
			if (_clip == null || _isPlaying)
			{
				return;
			}

			if (_position >= _clip.Duration - EndMargin)
			{
				SetPosition(TimeSpan.Zero);
			}

			Byte[]? slice = WavWriter.Slice(_clip.WavData, _position);
			TimeSpan start = slice == null ? TimeSpan.Zero : _position;
			Byte[] wav = WavWriter.ApplyGain(slice ?? _clip.WavData, _volume / 100);
			try
			{
				_player.Play(wav);
			}
			catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or TimeoutException or Win32Exception)
			{
				Warning = Loc.Get("Str.ViewerCannotPlay");
				return;
			}

			_clockStart = start;
			_clockTimestamp = _time.GetTimestamp();
			IsPlaying = true;
			_timer.Start();
		}

		/// <summary>Stops the sound where it is.</summary>
		private void Pause()
		{
			if (_isPlaying && _clip != null)
			{
				SetPosition(TimeSpan.FromTicks(Math.Min((_clockStart + _time.GetElapsedTime(_clockTimestamp)).Ticks, _clip.Duration.Ticks)));
			}

			StopPlayback();
		}

		private void StopPlayback()
		{
			_timer.Stop();
			_player.Stop();
			IsPlaying = false;
		}

		/// <summary>Moves the bar along with the sound; at the end the sound stops and the bar goes back to the beginning.</summary>
		private void OnTick()
		{
			if (_clip == null || !_isPlaying)
			{
				return;
			}

			TimeSpan reached = _clockStart + _time.GetElapsedTime(_clockTimestamp);
			if (reached >= _clip.Duration)
			{
				StopPlayback();
				SetPosition(TimeSpan.Zero);
				if (_isLooping)
				{
					Play();
				}

				return;
			}

			SetPosition(reached);
		}
	}
}