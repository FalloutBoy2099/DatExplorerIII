namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Threading;
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
	/// A sound shown in the viewer: its shape, its description and a button to play it.
	/// </summary>
	public sealed class AudioViewerViewModel : ObservableObject, IDisposable
	{
		private readonly WavPlayer _player = new();
		private readonly DispatcherTimer _endTimer = new();

		private Byte[] _source = Array.Empty<Byte>();
		private String _extension = String.Empty;
		private AudioClip? _clip;
		private Geometry? _shape;
		private String _info = String.Empty;
		private String _warning = String.Empty;
		private Boolean _isPlaying;
		private IReadOnlyList<ChannelChoice> _channelChoices;
		private ChannelChoice _selectedChannels;
		private Int32 _generation;

		public AudioViewerViewModel()
		{
			_channelChoices = CreateChannelChoices();
			_selectedChannels = _channelChoices[0];
			_endTimer.Tick += (_, _) => Stop();
			PlayStopCommand = new RelayCommand(PlayOrStop, () => _clip != null);
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

		/// <summary>The shape of the sound, drawn in a box of 1000 by 100 units.</summary>
		public Geometry? Shape
		{
			get => _shape;
			private set => SetProperty(ref _shape, value);
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

		public void Stop()
		{
			_endTimer.Stop();
			_player.Stop();
			IsPlaying = false;
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
			Shape = null;
			Info = String.Empty;
			Warning = String.Empty;
			OnPropertyChanged(nameof(HasClip));
			OnPropertyChanged(nameof(CanChooseChannels));
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
				Shape = CreateShape(clip.Envelope);
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
					Shape = null;
					Info = String.Empty;
					Warning = Loc.Get("Str.ViewerInvalid");
				}
			}

			OnPropertyChanged(nameof(HasClip));
			OnPropertyChanged(nameof(CanChooseChannels));
			CommandManager.InvalidateRequerySuggested();
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

		/// <summary>The outline of the loudness, mirrored around the middle line of a 1000 by 100 box.</summary>
		private static Geometry? CreateShape(Byte[] envelope)
		{
			if (envelope.Length == 0)
			{
				return null;
			}

			StreamGeometry geometry = new();
			using (StreamGeometryContext context = geometry.Open())
			{
				Double step = 1000.0 / envelope.Length;
				context.BeginFigure(new Point(0, 50), true, true);

				for (Int32 i = 0; i < envelope.Length; i++)
				{
					context.LineTo(new Point(i * step, 50 - (envelope[i] / 255.0 * 50)), true, false);
				}

				for (Int32 i = envelope.Length - 1; i >= 0; i--)
				{
					context.LineTo(new Point(i * step, 50 + (envelope[i] / 255.0 * 50)), true, false);
				}
			}

			geometry.Freeze();
			return geometry;
		}

		private void PlayOrStop()
		{
			if (_isPlaying)
			{
				Stop();
				return;
			}

			if (_clip == null)
			{
				return;
			}

			try
			{
				_player.Play(_clip.WavData);
				IsPlaying = true;
				_endTimer.Interval = _clip.Duration + TimeSpan.FromMilliseconds(300);
				_endTimer.Start();
			}
			catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or TimeoutException or Win32Exception)
			{
				Warning = Loc.Get("Str.ViewerCannotPlay");
			}
		}
	}
}