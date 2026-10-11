namespace DatExplorerIII.Tests
{
	using System;
	using System.Linq;
	using System.Threading;
	using System.Windows;
	using System.Windows.Controls;
	using DatExplorerIII.Infrastructure;
	using DatExplorerIII.Views;

	/// <summary>The control that holds two blocks side by side or one above the other, and the choice that drives it.</summary>
	public sealed class SplitPairTests
	{
		/// <summary>Controls need a thread of their own that is single threaded apartment.</summary>
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
			{
				throw new InvalidOperationException("The control failed.", failure);
			}
		}

		[Fact]
		public void TheBlocksAreArrangedAsAsked_AndTheSameElementsStayWhenTheArrangementChanges()
		{
			RunOnStaThread(() =>
			{
				Border first = new();
				Border second = new();
				SplitPair pair = new() { First = first, Second = second };

				// Side by side: columns, the two blocks and the bar between them.
				Assert.Equal(3, pair.ColumnDefinitions.Count);
				Assert.Empty(pair.RowDefinitions);
				Assert.Equal(3, pair.Children.Count);
				Assert.Equal(0, Grid.GetColumn(first));
				Assert.Equal(2, Grid.GetColumn(second));
				Assert.IsType<GridSplitter>(pair.Children[1]);

				// One above the other: the same elements, in rows.
				pair.Orientation = Orientation.Vertical;
				Assert.Equal(3, pair.RowDefinitions.Count);
				Assert.Empty(pair.ColumnDefinitions);
				Assert.Contains(first, pair.Children.Cast<UIElement>());
				Assert.Contains(second, pair.Children.Cast<UIElement>());
				Assert.Equal(0, Grid.GetRow(first));
				Assert.Equal(2, Grid.GetRow(second));

				// The first block hidden: the second one alone, no bar.
				pair.FirstVisible = false;
				Assert.Single(pair.Children);
				Assert.Same(second, pair.Children[0]);
				Assert.Single(pair.RowDefinitions);

				pair.FirstVisible = true;
				Assert.Equal(3, pair.Children.Count);
			});
		}

		[Fact]
		public void TheWeightsGiveTheShareOfTheRoom_AndAMissingBlockDoesNotBreakIt()
		{
			RunOnStaThread(() =>
			{
				SplitPair pair = new() { First = new Border(), Second = new Border(), FirstWeight = 1, SecondWeight = 3 };

				Assert.Equal(new GridLength(1, GridUnitType.Star), pair.ColumnDefinitions[0].Width);
				Assert.Equal(new GridLength(3, GridUnitType.Star), pair.ColumnDefinitions[2].Width);

				SplitPair alone = new() { Second = new Border() };
				Assert.Single(alone.Children);

				SplitPair empty = new();
				Assert.Empty(empty.Children);
			});
		}

		[Fact]
		public void TheLayoutChoiceBecomesAnOrientation()
		{
			BooleanToOrientationConverter converter = new();

			Assert.Equal(Orientation.Vertical, converter.Convert(true, typeof(Orientation), null!, System.Globalization.CultureInfo.InvariantCulture));
			Assert.Equal(Orientation.Horizontal, converter.Convert(false, typeof(Orientation), null!, System.Globalization.CultureInfo.InvariantCulture));
			Assert.Equal(Orientation.Horizontal, converter.Convert("not a boolean", typeof(Orientation), null!, System.Globalization.CultureInfo.InvariantCulture));
		}
	}
}