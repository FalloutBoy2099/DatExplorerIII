namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using System.Windows.Media.Imaging;
	using DatExplorerIII.Infrastructure;
	using DatExplorerIII.Models.Graphics;
	using DatExplorerIII.Views.Behaviors;

	/// <summary>The view models know nothing of WPF (they could be used by another kind of interface), and the views draw what they hold.</summary>
	public sealed class ViewModelIndependenceTests
	{
		[Fact]
		public void TheViewModels_DoNotUseWpf()
		{
			// The commands (System.Windows.Input) are the one thing a view model shares with the interface.
			Regex forbidden = new("using System\\.Windows(\\.(?!Input;)[A-Za-z.]+)?;|Application\\.Current|Dispatcher|BitmapSource|WriteableBitmap|\\bGeometry\\b|DependencyObject|Visibility\\b|\\bBrush\\b", RegexOptions.CultureInvariant);

			String folder = Path.Combine(SourceFiles.ProjectFolder, "ViewModels");
			List<String> offences = [];
			foreach (String path in Directory.EnumerateFiles(folder, "*.cs"))
			{
				foreach ((String line, Int32 number) in File.ReadLines(path).Select((line, index) => (line, index + 1)))
				{
					if (forbidden.IsMatch(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
						offences.Add(Path.GetFileName(path) + ":" + number + " " + line.Trim());
				}
			}

			Assert.True(offences.Count == 0, String.Join(Environment.NewLine, offences));
		}

		private static void RunOnStaThread(Action action)
		{
			Exception? failure = null;
			Thread thread = new(() =>
			{
				try
				{
					action();
				}
				catch (Exception exception)
				{
					failure = exception;
				}
			});

			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (failure != null)
				throw new InvalidOperationException("The test failed.", failure);
		}

		[Fact]
		public void PixelImage_NeedsPixelsThatFitItsSize()
		{
			Assert.Equal(16, new PixelImage(2, 2, new Byte[16]).Stride * 2);
			Assert.Throws<ArgumentException>(() => new PixelImage(2, 2, new Byte[15]));
			Assert.Throws<ArgumentOutOfRangeException>(() => new PixelImage(0, 2, Array.Empty<Byte>()));
		}

		[Fact]
		public void PixelImageSource_DrawsThePicture_AndAgainWhenItsPixelsChange()
		{
			RunOnStaThread(() =>
			{
				Byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 255];
				PixelImage picture = new(2, 1, pixels);
				Image control = new();

				PixelImageSource.SetImage(control, picture);
				WriteableBitmap first = Assert.IsType<WriteableBitmap>(control.Source);
				Byte[] read = new Byte[8];
				first.CopyPixels(read, 8, 0);
				Assert.Equal(pixels, read);

				// The movie draws its next picture in the same array and says so: the same bitmap is written again.
				pixels[0] = 99;
				picture.Invalidate();
				Assert.Same(first, control.Source);
				first.CopyPixels(read, 8, 0);
				Assert.Equal(99, read[0]);

				// Another picture of the same size reuses the bitmap too; a picture of another size has its own.
				PixelImageSource.SetImage(control, new PixelImage(2, 1, new Byte[8]));
				Assert.Same(first, control.Source);
				PixelImageSource.SetImage(control, new PixelImage(3, 1, new Byte[12]));
				Assert.NotSame(first, control.Source);
				Assert.Equal(3, ((BitmapSource)control.Source).PixelWidth);

				// The first picture is not followed any more; nothing at all clears the control.
				pixels[0] = 7;
				picture.Invalidate();
				PixelImageSource.SetImage(control, null);
				Assert.Null(control.Source);
			});
		}

		[Fact]
		public void FractionOfWidthConverter_PlacesTheMarkInTheSpace()
		{
			FractionOfWidthConverter converter = new();
			System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.InvariantCulture;

			Assert.Equal(new Thickness(49, 0, 0, 0), converter.Convert([0.5, 100.0, 2.0], typeof(Thickness), null!, culture));
			Assert.Equal(new Thickness(0), converter.Convert([0.0, 100.0, 2.0], typeof(Thickness), null!, culture));
			Assert.Equal(new Thickness(98, 0, 0, 0), converter.Convert([7.0, 100.0, 2.0], typeof(Thickness), null!, culture));

			// Not yet laid out (no width): the mark waits at the left.
			Assert.Equal(new Thickness(0), converter.Convert([0.5, Double.NaN, 2.0], typeof(Thickness), null!, culture));
			Assert.Equal(new Thickness(0), converter.Convert(["x"], typeof(Thickness), null!, culture));
		}

		[Fact]
		public void EnvelopeGeometryConverter_DrawsTheOutlineOfTheLoudness()
		{
			RunOnStaThread(() =>
			{
				EnvelopeGeometryConverter converter = new();
				Assert.Null(converter.Convert(Array.Empty<Byte>(), typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture));
				Assert.Null(converter.Convert(null, typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture));

				Geometry shape = Assert.IsAssignableFrom<Geometry>(converter.Convert(new Byte[] { 0, 255, 0 }, typeof(Geometry), null, System.Globalization.CultureInfo.InvariantCulture));

				// The loudest part reaches the top and the bottom of the box of 1000 by 100 units.
				Rect bounds = shape.Bounds;
				Assert.Equal(0, bounds.Top, 3);
				Assert.Equal(100, bounds.Bottom, 3);
				Assert.True(shape.IsFrozen);
			});
		}
	}
}