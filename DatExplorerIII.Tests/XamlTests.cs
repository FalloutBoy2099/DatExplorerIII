namespace DatExplorerIII.Tests
{
	using System;
	using System.Threading;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Interface;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.Views;

	/// <summary>
	/// The XAML of the program is only read when a window opens: these tests read it, so that a wrong key, a wrong name or a style
	/// that does not load is found by the tests and not when the program starts.
	/// </summary>
	public sealed class XamlTests
	{
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
				throw new InvalidOperationException("The XAML failed.", failure);
		}

		private static ResourceDictionary LoadScrollBars()
		{
			return (ResourceDictionary)Application.LoadComponent(new Uri("/DatExplorerIII;component/Resources/ScrollBars.xaml", UriKind.Relative));
		}

		/// <summary>A scroll bar with the style of the program, laid out; what is seen is 27 of 953 (a long list).</summary>
		private static (ScrollBar Bar, Track Track) CreateScrollBar(ResourceDictionary resources, Orientation orientation, Double length)
		{
			ScrollBar bar = new()
			{
				Orientation = orientation,
				Minimum = 0,
				Maximum = 926,
				ViewportSize = 27,
				Value = 100,
			};

			if (orientation == Orientation.Vertical)
			{
				bar.Height = length;
			}
			else
			{
				bar.Width = length;
			}

			bar.Resources.MergedDictionaries.Add(resources);
			bar.Style = (Style)resources[typeof(ScrollBar)];
			bar.Measure(new Size(2000, 2000));
			bar.Arrange(new Rect(0, 0, bar.DesiredSize.Width, bar.DesiredSize.Height));
			bar.UpdateLayout();
			return (bar, (Track)bar.Template.FindName("PART_Track", bar));
		}

		[Fact]
		public void TheScrollBars_Load_AndTheirSliderIsNeverShorterThanTheSmallestOne()
		{
			RunOnStaThread(() =>
			{
				ResourceDictionary resources = LoadScrollBars();
				ScrollBarMetrics metrics = (ScrollBarMetrics)resources["ScrollBarMetrics"];
				metrics.Update(16, 60);
				// The theme would draw a slider of 500 * 27 / 953 = 14; the track gets a viewport that makes it 60.
				(ScrollBar vertical, Track verticalTrack) = CreateScrollBar(resources, Orientation.Vertical, 500);
				Assert.Equal(16, vertical.ActualWidth, 1);
				Assert.NotNull(verticalTrack.Thumb);
				Assert.Equal(60, verticalTrack.Thumb.ActualHeight, 1);

				(ScrollBar horizontal, Track horizontalTrack) = CreateScrollBar(resources, Orientation.Horizontal, 500);
				Assert.Equal(16, horizontal.ActualHeight, 1);
				Assert.Equal(60, horizontalTrack.Thumb.ActualWidth, 1);

				// A longer minimum gives a longer slider, at once.
				metrics.Update(16, 120);
				vertical.UpdateLayout();
				verticalTrack.InvalidateArrange();
				vertical.Measure(new Size(2000, 2000));
				vertical.Arrange(new Rect(0, 0, vertical.DesiredSize.Width, vertical.DesiredSize.Height));
				vertical.UpdateLayout();
				Assert.Equal(120, verticalTrack.Thumb.ActualHeight, 1);

				// The width follows too.
				metrics.Update(24, 120);
				vertical.Measure(new Size(2000, 2000));
				Assert.Equal(24, vertical.DesiredSize.Width, 1);
			});
		}

		[Fact]
		public void TheScrollBars_HaveTheArrowPointer_EvenInATextBox()
		{
			RunOnStaThread(() =>
			{
				ResourceDictionary resources = LoadScrollBars();
				foreach (Orientation orientation in new[] { Orientation.Vertical, Orientation.Horizontal })
				{
					(ScrollBar bar, _) = CreateScrollBar(resources, orientation, 500);
					Assert.Equal(System.Windows.Input.Cursors.Arrow, bar.Cursor);
				}
			});
		}

		[Fact]
		public void TheScrollBar_OfAShortList_KeepsTheSliderTheTrackGives()
		{
			RunOnStaThread(() =>
			{
				ResourceDictionary resources = LoadScrollBars();
				ScrollBar bar = new() { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 10, ViewportSize = 10, Height = 400 };
				bar.Resources.MergedDictionaries.Add(resources);
				bar.Style = (Style)resources[typeof(ScrollBar)];
				bar.Measure(new Size(2000, 2000));
				bar.Arrange(new Rect(0, 0, bar.DesiredSize.Width, bar.DesiredSize.Height));
				bar.UpdateLayout();

				// Half of the list is seen: the slider is half of the track (the two arrows take 2 x 16 of the 400).
				Track track = (Track)bar.Template.FindName("PART_Track", bar);
				Assert.Equal((400 - 32) / 2.0, track.Thumb.ActualHeight, 1);
			});
		}

		[Fact]
		public void TheSettingsWindow_Loads_WithItsTabsItsSlidersAndTheirValues()
		{
			RunOnStaThread(() =>
			{
				SettingsData data = new() { ScrollBarWidth = 22, ScrollBarMinimumThumb = 90 };
				SettingsWindow window = new(new SettingsDialogViewModel(data));

				// The bindings give their values when the window is shown.
				window.Show();
				try
				{
					Check(window);
				}
				finally
				{
					window.Close();
				}
			});
		}

		private static void Check(SettingsWindow window)
		{
			{
				Slider width = (Slider)window.FindName("ScrollBarWidthSlider");
				Slider thumb = (Slider)window.FindName("ScrollBarThumbSlider");
				Assert.Equal(22, width.Value);
				Assert.Equal(90, thumb.Value);
				Assert.Equal(ScrollBarMetrics.MinimumWidth, width.Minimum);
				Assert.Equal(ScrollBarMetrics.MaximumWidth, width.Maximum);
				Assert.Equal(ScrollBarMetrics.MinimumThumbLength, thumb.Minimum);
				Assert.Equal(ScrollBarMetrics.MaximumThumbLength, thumb.Maximum);
				Assert.NotNull(window.FindName("GeneralTab"));
				Assert.NotNull(window.FindName("CompareTab"));
				Assert.NotNull(window.FindName("SubtitlesBox"));
				Assert.NotNull(window.FindName("SaveBlocksBox"));
				Assert.NotNull(window.FindName("MessagesTab"));
				Assert.Equal(3, ((ItemsControl)window.FindName("MessageChecksList")).Items.Count);
				Assert.NotNull(window.FindName("MethodBinaryButton"));
			}
		}

		[Fact]
		public void TheDialogs_Load()
		{
			RunOnStaThread(() =>
			{
				// (The other dialogs take their icon font from the resources of the application, which tests do not have.)
				Assert.NotNull(new CompareSetupDialog("Compare", null, null, []));
			});
		}
	}
}