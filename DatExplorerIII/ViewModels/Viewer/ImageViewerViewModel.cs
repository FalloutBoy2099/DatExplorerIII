namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Input;
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
		private readonly Dictionary<(Int32 Direction, Int32 Frame, Int32 Palette), PixelImage> _cache = new();
		private readonly IUiTimer _timer;

		private FrmImage? _animation;
		private Int32 _canvasWidth;
		private Int32 _canvasHeight;
		private PixelImage? _picture;
		private IReadOnlyList<PaletteChoice> _paletteChoices = Array.Empty<PaletteChoice>();
		private PaletteChoice? _selectedPalette;
		private Int32 _direction;
		private IReadOnlyList<Int32> _directions = Array.Empty<Int32>();
		private Int32 _frame;
		private Boolean _isPlaying;
		private ZoomOption _selectedZoom;
		private String _info = String.Empty;
		private EndingImageCheck? _ending;
		private Func<Boolean, String?>? _endingTextSource;
		private Boolean _showEndingText;
		private String _endingText = String.Empty;

		public ImageViewerViewModel(IUiThread uiThread)
		{
			_timer = uiThread.CreateTimer();
			ZoomOptions =
			[
				new ZoomOption(Loc.Get("Str.ZoomFit"), 0),
				new ZoomOption("1×", 1),
				new ZoomOption("2×", 2),
				new ZoomOption("3×", 3),
				new ZoomOption("4×", 4),
				new ZoomOption("6×", 6),
				new ZoomOption("8×", 8),
				new ZoomOption("10×", 10),
				new ZoomOption("12×", 12),
				new ZoomOption("16×", 16),
			];
			_selectedZoom = ZoomOptions.First(option => option.Factor == 1);

			_timer.Tick += (_, _) => NextFrame(true);
			PlayCommand = new RelayCommand(TogglePlay, () => IsAnimated);
			PreviousCommand = new RelayCommand(() => NextFrame(false, -1), () => IsAnimated);
			NextCommand = new RelayCommand(() => NextFrame(false, 1), () => IsAnimated);
			NewEndingTextCommand = new RelayCommand(PickEndingText, () => EndingTextVisible);
			PreviousDirectionCommand = new RelayCommand(() => StepDirection(-1), () => HasDirections);
			NextDirectionCommand = new RelayCommand(() => StepDirection(1), () => HasDirections);
		}

		/// <summary>The previous direction (the last one after the first).</summary>
		public ICommand PreviousDirectionCommand { get; }

		/// <summary>The next direction (the first one after the last).</summary>
		public ICommand NextDirectionCommand { get; }

		/// <summary>Goes to the next (or previous) direction, round the circle.</summary>
		private void StepDirection(Int32 step)
		{
			if (DirectionCount > 1)
			{
				SelectedDirection = (((_direction + step) % DirectionCount) + DirectionCount) % DirectionCount;
			}
		}

		/// <summary>How many notches of the wheel make one step of magnification (1 to 3): the program gives the choice of the user (3 when it gives none).</summary>
		public Func<Int32>? WheelNotchesSource { get; set; }

		/// <summary>A notch of a wheel, in the units of the mouse messages.</summary>
		private const Int32 WheelNotch = 120;

		/// <summary>What the wheel turned since the last step (a wheel that turns smoothly sends less than a notch at a time).</summary>
		private Int32 _wheelRemainder;

		/// <summary>Takes a turn of the wheel: after enough notches in the same way the magnification changes by one step.</summary>
		public void Wheel(Int32 delta)
		{
			// A turn the other way starts again.
			if (_wheelRemainder != 0 && Math.Sign(_wheelRemainder) != Math.Sign(delta))
			{
				_wheelRemainder = 0;
			}

			_wheelRemainder += delta;
			Int32 needed = Math.Clamp(WheelNotchesSource?.Invoke() ?? 3, 1, 3) * WheelNotch;
			while (Math.Abs(_wheelRemainder) >= needed)
			{
				Int32 step = Math.Sign(_wheelRemainder);
				_wheelRemainder -= step * needed;
				StepZoom(step);
			}
		}

		/// <summary>
		/// One step of magnification more (the wheel of the mouse turned up) or less, among the magnifications of the list. "Fit" is
		/// not one of the steps: from it the wheel goes to 1×.
		/// </summary>
		public void StepZoom(Int32 step)
		{
			if (IsFit)
			{
				ActualSize();
				return;
			}

			List<ZoomOption> steps = ZoomOptions.Where(option => option.Factor > 0).ToList();
			Int32 index = Math.Clamp(steps.IndexOf(_selectedZoom) + step, 0, steps.Count - 1);
			SelectedZoom = steps[index];
		}

		/// <summary>The picture at its real size (100%).</summary>
		public void ActualSize()
		{
			SelectedZoom = ZoomOptions.First(option => option.Factor == 1);
		}

		/// <summary>Picks another text of the endings to show over the picture.</summary>
		public ICommand NewEndingTextCommand { get; }

		public ICommand PlayCommand { get; }

		public ICommand PreviousCommand { get; }

		public ICommand NextCommand { get; }

		public IReadOnlyList<ZoomOption> ZoomOptions { get; }

		public PixelImage? Picture
		{
			get => _picture;
			private set
			{
				if (SetProperty(ref _picture, value))
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
		public IReadOnlyList<Int32> Directions => _directions;

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
					OnPropertyChanged(nameof(SelectedDirectionNumber));
					RenderCurrent();
				}
				else if (value < 0)
				{
					// The box gave up its selection (its list was replaced): it gets the direction that is shown back.
					OnPropertyChanged();
				}
			}
		}

		/// <summary>The direction shown as its number (from 1): the box of the directions holds these numbers.</summary>
		public Int32 SelectedDirectionNumber
		{
			get => _direction + 1;
			set
			{
				if (value >= 1)
				{
					SelectedDirection = value - 1;
				}
				else
				{
					// The box gave up its selection (its list was replaced): it gets the direction that is shown back.
					OnPropertyChanged();
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
		public Double DisplayWidth => (_picture?.Width ?? 0) * ScaleShown;

		public Double DisplayHeight => (_picture?.Height ?? 0) * ScaleShown;

		/// <summary>The magnification of the picture when it is not fitted to the window.</summary>
		private Double ScaleShown => _selectedZoom.Factor > 0 ? _selectedZoom.Factor : 1;

		public String Info
		{
			get => _info;
			private set => SetProperty(ref _info, value);
		}

		/// <summary>The check of a picture that has its own palette: whether the game can use it as an image of the ending (null: not checked).</summary>
		public EndingImageCheck? Ending => _ending;

		public Boolean HasEnding => _ending != null;

		/// <summary>The picture can be an image of the ending: the text of the endings can be shown over it.</summary>
		public Boolean IsEndingUsable => _ending is { IsUsable: true };

		/// <summary>The name of the check ("Ending image"): a tick says that the picture can be one, a cross that it cannot (<see cref="EndingTip"/> says why).</summary>
		public String EndingStatus => _ending == null ? String.Empty : Loc.Get("Str.ViewerEnding");

		/// <summary>Why: what the game needs and what the picture is.</summary>
		public String EndingTip
		{
			get
			{
				return _ending?.Kind switch
				{
					EndingKind.Standard => Loc.Get("Str.ViewerEndingTipYes"),
					EndingKind.Panning => Loc.Get("Str.ViewerEndingTipPan"),
					EndingKind.WrongFolder => Loc.Format("Str.ViewerEndingTipFolder", _ending.Folder),
					EndingKind.WrongSize => Loc.Format("Str.ViewerEndingTipSize", _ending.Width, _ending.Height),
					_ => String.Empty,
				};
			}
		}

		/// <summary>Whether a random text of the endings is shown over the picture (to see how it fits); it only shows on a picture that can be an image of the ending.</summary>
		public Boolean ShowEndingText
		{
			get => _showEndingText;
			set
			{
				if (SetProperty(ref _showEndingText, value))
				{
					if (value && IsEndingUsable)
					{
						PickEndingText();
					}

					OnEndingTextChanged();
				}
			}
		}

		public Boolean EndingTextVisible => _showEndingText && IsEndingUsable && _endingText.Length > 0;

		public String EndingText => _endingText;

		/// <summary>The width the text is wrapped to, in the pixels of the picture.</summary>
		public Double EndingTextWidth => _ending?.TextWidth ?? EndingImages.SlideTextWidth;

		/// <summary>The distance of the text from the left of the picture: centered on the screen of 640 pixels (the left of a wider picture).</summary>
		public Double EndingTextLeft => (EndingImages.ScreenWidth - EndingTextWidth) / 2;

		/// <summary>Shows an animation or a sprite.</summary>
		/// <param name="choices">The palettes it can be shown with; the first one is used.</param>
		/// <param name="ending">The check of the picture as an image of the ending (when it has its own palette).</param>
		/// <param name="endingText">Gives a text of the endings (the parameter tells the death screen from a slide).</param>
		public void ShowAnimation(FrmImage image, IReadOnlyList<PaletteChoice> choices, EndingImageCheck? ending = null, Func<Boolean, String?>? endingText = null)
		{
			Reset();
			_ending = ending;
			_endingTextSource = endingText;
			if (_showEndingText && IsEndingUsable)
			{
				PickEndingText();
			}

			OnEndingTextChanged();
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
		public void ShowBitmap(PixelImage bitmap, String? info = null)
		{
			Reset();
			Picture = bitmap;
			Info = info ?? Loc.Format("Str.ViewerImageSize", bitmap.Width, bitmap.Height);
		}

		/// <summary>Shows a full screen picture.</summary>
		public void ShowPicture(RixImage image)
		{
			Reset();
			Picture = ImageRenderer.Render(image);
			Info = Loc.Format("Str.ViewerImageSize", image.Width, image.Height);
		}

		/// <summary>Shows the colors of a palette.</summary>
		public void ShowPalette(Palette palette)
		{
			Reset();
			Picture = ImageRenderer.RenderPalette(palette);
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

		private void PickEndingText()
		{
			_endingText = (_ending == null ? null : _endingTextSource?.Invoke(_ending.IsDeathScreen)) ?? String.Empty;
			OnEndingTextChanged();
		}

		private void OnEndingTextChanged()
		{
			OnPropertyChanged(nameof(Ending));
			OnPropertyChanged(nameof(HasEnding));
			OnPropertyChanged(nameof(IsEndingUsable));
			OnPropertyChanged(nameof(EndingStatus));
			OnPropertyChanged(nameof(EndingTip));
			OnPropertyChanged(nameof(EndingText));
			OnPropertyChanged(nameof(EndingTextVisible));
			OnPropertyChanged(nameof(EndingTextWidth));
			OnPropertyChanged(nameof(EndingTextLeft));
		}

		private void Reset()
		{
			Stop();
			_ending = null;
			_endingTextSource = null;
			_endingText = String.Empty;
			OnEndingTextChanged();
			_cache.Clear();
			_animation = null;
			_direction = 0;
			_frame = 0;
			_paletteChoices = Array.Empty<PaletteChoice>();
			_selectedPalette = null;
			Picture = null;
			OnAnimationChanged();
		}

		private void OnAnimationChanged()
		{
			// The list is the same object until the picture changes: a box that gets a new list forgets its selection.
			if (_directions.Count != DirectionCount)
			{
				_directions = Enumerable.Range(1, DirectionCount).ToList();
			}
			OnPropertyChanged(nameof(PaletteChoices));
			OnPropertyChanged(nameof(HasPaletteChoices));
			OnPropertyChanged(nameof(SelectedPalette));
			OnPropertyChanged(nameof(DirectionCount));
			OnPropertyChanged(nameof(HasDirections));
			OnPropertyChanged(nameof(Directions));
			OnPropertyChanged(nameof(SelectedDirection));
			OnPropertyChanged(nameof(SelectedDirectionNumber));
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
			if (!_cache.TryGetValue(key, out PixelImage? bitmap))
			{
				FrmFrame frame = _animation.Directions[_direction].Frames[_frame];
				bitmap = ImageRenderer.Render(frame, _canvasWidth, _canvasHeight, _selectedPalette.Palette);
				_cache[key] = bitmap;
			}

			Picture = bitmap;
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