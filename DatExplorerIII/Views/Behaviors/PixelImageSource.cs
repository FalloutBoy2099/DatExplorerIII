namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using Models;

	/// <summary>
	/// Draws a <see cref="PixelImage"/> in an image control. The bitmap is kept and written again when the picture has the same size
	/// (the frames of an animation, the pictures of a movie), so that nothing is made for every picture.
	/// </summary>
	public static class PixelImageSource
	{
		public static readonly DependencyProperty ImageProperty = DependencyProperty.RegisterAttached(
			"Image",
			typeof(PixelImage),
			typeof(PixelImageSource),
			new PropertyMetadata(null, OnImageChanged));

		private static readonly DependencyProperty FollowerProperty = DependencyProperty.RegisterAttached(
			"Follower",
			typeof(Follower),
			typeof(PixelImageSource));

		public static PixelImage? GetImage(DependencyObject element)
		{
			return (PixelImage?)element.GetValue(ImageProperty);
		}

		public static void SetImage(DependencyObject element, PixelImage? value)
		{
			element.SetValue(ImageProperty, value);
		}

		private static void OnImageChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not Image control)
			{
				return;
			}

			if (control.GetValue(FollowerProperty) is Follower previous)
			{
				previous.Detach();
				control.ClearValue(FollowerProperty);
			}

			if (e.NewValue is PixelImage picture)
			{
				Follower follower = new(control, picture);
				control.SetValue(FollowerProperty, follower);
				follower.Draw();
			}
			else
			{
				control.Source = null;
			}
		}

		/// <summary>Draws a picture in a control, and again each time the picture says its pixels changed.</summary>
		private sealed class Follower
		{
			private readonly Image _control;
			private readonly PixelImage _picture;

			public Follower(Image control, PixelImage picture)
			{
				_control = control;
				_picture = picture;
				picture.Changed += OnChanged;
			}

			public void Detach()
			{
				_picture.Changed -= OnChanged;
			}

			public void Draw()
			{
				if (_control.Source is not WriteableBitmap bitmap || bitmap.PixelWidth != _picture.Width || bitmap.PixelHeight != _picture.Height)
				{
					bitmap = new WriteableBitmap(_picture.Width, _picture.Height, 96, 96, PixelFormats.Bgra32, null);
					_control.Source = bitmap;
				}

				bitmap.WritePixels(new Int32Rect(0, 0, _picture.Width, _picture.Height), _picture.Pixels, _picture.Stride, 0);
			}

			private void OnChanged(Object? sender, EventArgs e)
			{
				// The pictures of a movie are made in the background thread of the timer or on the interface: draw on the one of the control.
				if (_control.CheckAccess())
				{
					Draw();
				}
				else
				{
					_control.Dispatcher.Invoke(Draw);
				}
			}
		}
	}
}