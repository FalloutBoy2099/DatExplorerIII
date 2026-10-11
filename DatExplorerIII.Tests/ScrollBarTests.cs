namespace DatExplorerIII.Tests
{
	using System;
	using System.Globalization;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The size of the scroll bars: a slider that is never smaller than the user wants, and the settings that say so.</summary>
	[Collection("ScrollBarSizes")]
	public sealed class ScrollBarTests
	{
		private static Double Convert(Double viewport, Double minimum, Double maximum, Double trackLength, Double minimumThumb)
		{
			return (Double)new ScrollBarViewportConverter().Convert([viewport, minimum, maximum, trackLength, minimumThumb], typeof(Double), null!, CultureInfo.InvariantCulture);
		}

		/// <summary>The length of the slider the track draws for what is seen, as the track works it out.</summary>
		private static Double ThumbLength(Double viewport, Double minimum, Double maximum, Double trackLength)
		{
			return trackLength * viewport / ((maximum - minimum) + viewport);
		}

		[Fact]
		public void TheSlider_OfALongList_IsAsLongAsTheSmallestOne()
		{
			// 27 lines seen of 953, in a track of 529: the theme would draw a slider of 15.
			Assert.InRange(ThumbLength(27, 0, 926, 529), 14, 16);

			Double viewport = Convert(27, 0, 926, 529, 60);

			Assert.Equal(60, ThumbLength(viewport, 0, 926, 529), 6);
		}

		[Fact]
		public void TheSlider_OfAShortList_IsLeftAlone()
		{
			// Half of the list is seen: the slider is half of the track, longer than the smallest.
			Assert.Equal(10, Convert(10, 0, 10, 400, 60));
			Assert.Equal(200, ThumbLength(10, 0, 10, 400), 6);
		}

		[Theory]
		[InlineData(0, 0, 0, 500, 60)]
		[InlineData(10, 0, 0, 500, 60)]
		[InlineData(10, 0, 100, 50, 60)]
		[InlineData(10, 0, 100, 0, 60)]
		[InlineData(Double.NaN, 0, 100, 500, 60)]
		public void TheSlider_WhenThereIsNothingToScrollOrNoRoom_KeepsWhatTheScrollBarSays(Double viewport, Double minimum, Double maximum, Double trackLength, Double minimumThumb)
		{
			Double result = Convert(viewport, minimum, maximum, trackLength, minimumThumb);

			Assert.True(Double.IsNaN(viewport) ? Double.IsNaN(result) : result == viewport);
		}

		[Fact]
		public void TheSlider_WithValuesThatAreNotNumbers_DoesNotFail()
		{
			ScrollBarViewportConverter converter = new();

			Assert.Equal(0.0, converter.Convert([], typeof(Double), null!, CultureInfo.InvariantCulture));
			Assert.Equal(5.0, converter.Convert([5.0, "x"], typeof(Double), null!, CultureInfo.InvariantCulture));
			Assert.Equal(5.0, converter.Convert([5.0, 0.0, 10.0, 100.0, "x"], typeof(Double), null!, CultureInfo.InvariantCulture));
		}

		[Fact]
		public void TheMetrics_KeepTheirNumbersBetweenTheLimits()
		{
			Assert.Equal(ScrollBarMetrics.MaximumWidth, ScrollBarMetrics.Clamp(500, ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.MaximumWidth));
			Assert.Equal(ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.Clamp(-3, ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.MaximumWidth));

			ScrollBarMetrics.Apply(16, 5000);
			Assert.Equal(ScrollBarMetrics.MaximumThumbLength, ScrollBarMetrics.Current.MinimumThumb);
			ScrollBarMetrics.Apply(16, 1);
			Assert.Equal(ScrollBarMetrics.MinimumThumbLength, ScrollBarMetrics.Current.MinimumThumb);
			ScrollBarMetrics.Apply(ScrollBarMetrics.DefaultWidth, ScrollBarMetrics.DefaultMinimumThumb);
			Assert.Equal(ScrollBarMetrics.DefaultMinimumThumb, ScrollBarMetrics.Current.MinimumThumb);
		}

		[Fact]
		public void TheSettings_HaveTheDefaultSizes_AndTheDialogGivesThemBack()
		{
			SettingsData data = new();
			Assert.Equal(ScrollBarMetrics.DefaultWidth, data.ScrollBarWidth);
			Assert.Equal(ScrollBarMetrics.DefaultMinimumThumb, data.ScrollBarMinimumThumb);

			data.ScrollBarWidth = 22;
			data.ScrollBarMinimumThumb = 90;
			SettingsDialogViewModel model = new(data);

			Assert.Equal(22, model.ScrollBarWidth);
			Assert.Equal(90, model.ScrollBarMinimumThumb);
		}
	}
}