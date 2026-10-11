namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using System.Windows.Threading;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>A palette the user can show a picture with.</summary>
	public sealed record PaletteChoice(String Display, Palette Palette)
	{
		public override String ToString() => Display;
	}

	/// <summary>A magnification of the picture; a factor of 0 fits the picture to the window.</summary>
	public sealed record ZoomOption(String Display, Double Factor)
	{
		public override String ToString() => Display;
	}

	/// <summary>
	/// A picture shown in the viewer: an animation or sprite (with the choice of the palette, the direction and the frame),
	/// a full screen picture, or a palette.
	/// </summary>
	public sealed class ImageViewerViewModel : ObservableObject, IDisposable
	{
		private readonly Dictionary<(Int32 Direction, Int32 Frame, Int32 Palette), BitmapSource> _cache = new();
		private readonly DispatcherTimer _timer = new();

		private FrmImage? _animation;
		private Int32 _canvasWidth;
		private Int32 _canvasHeight;
		private BitmapSource? _bitmap;
		private IReadOnlyList<PaletteChoice> _paletteChoices = Array.Empty<PaletteChoice>();
		private PaletteChoice? _selectedPalette;
		private Int32 _direction;
		private Int32 _frame;
		private Boolean _isPlaying;
		private ZoomOption _selectedZoom;
		private String _info = String.Empty;

		public ImageViewerViewModel()
		{
			ZoomOptions =
			[
				new ZoomOption(Loc.Get("Str.ZoomFit"), 0),
				new ZoomOption("1×", 1),
				new ZoomOption("2×", 2),
				new ZoomOption("3×", 3),
				new ZoomOption("4×", 4),
				new ZoomOption("6×", 6),
				new ZoomOption("8×", 8),
			];
			_selectedZoom = ZoomOptions[1];

			_timer.Tick += (_, _) => NextFrame(true);
			PlayCommand = new RelayCommand(TogglePlay, () => IsAnimated);
			PreviousCommand = new RelayCommand(() => NextFrame(false, -1), () => IsAnimated);
			NextCommand = new RelayCommand(() => NextFrame(false, 1), () => IsAnimated);
		}

		public ICommand PlayCommand { get; }

		public ICommand PreviousCommand { get; }

		public ICommand NextCommand { get; }

		public IReadOnlyList<ZoomOption> ZoomOptions { get; }

		public BitmapSource? Bitmap
		{
			get => _bitmap;
			private set
			{
				if (SetProperty(ref _bitmap, value))
				{
					OnPropertyChanged(nameof(DisplayWidth));
					OnPropertyChanged(nameof(DisplayHeight));
				}
			}
		}

		public IReadOnlyList<PaletteChoice> PaletteChoices
		{
			get => _paletteChoices;
			private set
			{
				SetProperty(ref _paletteChoices, value);
				OnPropertyChanged(nameof(HasPaletteChoices));
			}
		}

		public Boolean HasPaletteChoices => _paletteChoices.Count > 1;

		public PaletteChoice? SelectedPalette
		{
			get => _selectedPalette;
			set
			{
				if (SetProperty(ref _selectedPalette, value))
				{
					RenderCurrent();
				}
			}
		}

		public Int32 DirectionCount => _animation?.Directions.Count ?? 0;

		public Boolean HasDirections => DirectionCount > 1;

		/// <summary>The numbers of the directions, from 1.</summary>
		public IReadOnlyList<Int32> Directions => Enumerable.Range(1, DirectionCount).ToList();

		/// <summary>The direction shown, from 0.</summary>
		public Int32 SelectedDirection
		{
			get => _direction;
			set
			{
				if (_animation != null && value >= 0 && value < DirectionCount && value != _direction)
				{
					_direction = value;
					_frame = 0;
					OnPropertyChanged();
					RenderCurrent();
				}
			}
		}

		public Int32 FrameCount => _animation?.FramesPerDirection ?? 0;

		public Boolean IsAnimated => FrameCount > 1;

		/// <summary>The frame shown, from 0.</summary>
		public Int32 CurrentFrame
		{
			get => _frame;
			set
			{
				if (_animation != null && value >= 0 && value < FrameCount && value != _frame)
				{
					_frame = value;
					OnPropertyChanged();
					RenderCurrent();
				}
			}
		}

		public String FrameLabel => FrameCount > 0 ? $"{_frame + 1} / {FrameCount}" : String.Empty;

		public Boolean IsPlaying
		{
			get => _isPlaying;
			private set => SetProperty(ref _isPlaying, value);
		}

		public ZoomOption SelectedZoom
		{
			get => _selectedZoom;
			set
			{
				// The box that shows the choices gives up its selection when they are replaced: that is not a choice.
				if (value is not null && SetProperty(ref _selectedZoom, value))
				{
					OnPropertyChanged(nameof(IsFit));
					OnPropertyChanged(nameof(DisplayWidth));
					OnPropertyChanged(nameof(DisplayHeight));
				}
			}
		}

		public Boolean IsFit => _selectedZoom.Factor == 0;

		/// <summary>The width of the picture at the chosen magnification.</summary>
		public Double DisplayWidth => (_bitmap?.PixelWidth ?? 0) * Math.Max(_selectedZoom.Factor, 1);

		public Double DisplayHeight => (_bitmap?.PixelHeight ?? 0) * Math.Max(_selectedZoom.Factor, 1);

		public String Info
		{
			get => _info;
			private set => SetProperty(ref _info, value);
		}

		/// <summary>Shows an animation or a sprite.</summary>
		/// <param name="choices">The palettes it can be shown with; the first one is used.</param>
		public void ShowAnimation(FrmImage image, IReadOnlyList<PaletteChoice> choices)
		{
			Reset();
			_animation = image;
			_canvasWidth = image.Directions.SelectMany(direction => direction.Frames).Max(frame => frame.Width);
			_canvasHeight = image.Directions.SelectMany(direction => direction.Frames).Max(frame => frame.Height);

			_paletteChoices = choices;
			_selectedPalette = choices[0];
			OnAnimationChanged();

			Info = Loc.Format("Str.ViewerImageFrm", _canvasWidth, _canvasHeight, image.Directions.Count, image.FramesPerDirection, image.FramesPerSecond);
			RenderCurrent();
		}

		/// <summary>Shows a picture file (PNG, BMP, JPEG, GIF) that was decoded.</summary>
		public void ShowBitmap(BitmapSource bitmap, String? info = null)
		{
			Reset();
			Bitmap = bitmap;
			Info = info ?? Loc.Format("Str.ViewerImageSize", bitmap.PixelWidth, bitmap.PixelHeight);
		}

		/// <summary>Shows a full screen picture.</summary>
		public void ShowPicture(RixImage image)
		{
			Reset();
			Bitmap = ImageRenderer.Render(image);
			Info = Loc.Format("Str.ViewerImageSize", image.Width, image.Height);
		}

		/// <summary>Shows the colors of a palette.</summary>
		public void ShowPalette(Palette palette)
		{
			Reset();
			Bitmap = ImageRenderer.RenderPalette(palette);
			Info = Loc.Get("Str.ViewerPaletteInfo");
		}

		public void Clear()
		{
			Reset();
			Info = String.Empty;
		}

		public void Stop()
		{
			_timer.Stop();
			IsPlaying = false;
		}

		public void Dispose()
		{
			Stop();
		}

		private void Reset()
		{
			Stop();
			_cache.Clear();
			_animation = null;
			_direction = 0;
			_frame = 0;
			_paletteChoices = Array.Empty<PaletteChoice>();
			_selectedPalette = null;
			Bitmap = null;
			OnAnimationChanged();
		}

		private void OnAnimationChanged()
		{
			OnPropertyChanged(nameof(PaletteChoices));
			OnPropertyChanged(nameof(HasPaletteChoices));
			OnPropertyChanged(nameof(SelectedPalette));
			OnPropertyChanged(nameof(DirectionCount));
			OnPropertyChanged(nameof(HasDirections));
			OnPropertyChanged(nameof(Directions));
			OnPropertyChanged(nameof(SelectedDirection));
			OnPropertyChanged(nameof(FrameCount));
			OnPropertyChanged(nameof(IsAnimated));
			OnPropertyChanged(nameof(CurrentFrame));
			OnPropertyChanged(nameof(FrameLabel));
		}

		private void RenderCurrent()
		{
			if (_animation == null || _selectedPalette == null)
			{
				return;
			}

			Int32 paletteIndex = _paletteChoices.ToList().IndexOf(_selectedPalette);
			(Int32, Int32, Int32) key = (_direction, _frame, paletteIndex);
			if (!_cache.TryGetValue(key, out BitmapSource? bitmap))
			{
				FrmFrame frame = _animation.Directions[_direction].Frames[_frame];
				bitmap = ImageRenderer.Render(frame, _canvasWidth, _canvasHeight, _selectedPalette.Palette);
				_cache[key] = bitmap;
			}

			Bitmap = bitmap;
			OnPropertyChanged(nameof(FrameLabel));
		}

		/// <param name="wrap">True to go back to the first frame after the last one (animation), false to stop at the ends.</param>
		private void NextFrame(Boolean wrap, Int32 step = 1)
		{
			if (!IsAnimated)
			{
				return;
			}

			Int32 next = _frame + step;
			if (wrap || next < 0 || next >= FrameCount)
			{
				next = ((next % FrameCount) + FrameCount) % FrameCount;
			}

			CurrentFrame = next;
		}

		private void TogglePlay()
		{
			if (_isPlaying)
			{
				Stop();
				return;
			}

			Int32 fps = _animation?.FramesPerSecond ?? 0;
			_timer.Interval = TimeSpan.FromMilliseconds(1000.0 / (fps > 0 ? Math.Min(fps, 30) : 10));
			_timer.Start();
			IsPlaying = true;
		}
	}
}