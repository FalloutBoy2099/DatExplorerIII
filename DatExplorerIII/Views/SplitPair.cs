namespace DatExplorerIII.Views
{
	using System;
	using System.Windows;
	using System.Windows.Controls;

	/// <summary>
	/// Two blocks with a bar between them that can be dragged: side by side (<see cref="Orientation.Horizontal"/>) or one above
	/// the other (<see cref="Orientation.Vertical"/>). The way they are arranged can change while the program runs; the blocks
	/// are the same elements, so what they show is kept. The first block can be hidden: the second one then takes all the room.
	/// </summary>
	public sealed class SplitPair : Grid
	{
		public static readonly DependencyProperty OrientationProperty = Register(nameof(Orientation), typeof(Orientation), Orientation.Horizontal);

		public static readonly DependencyProperty FirstProperty = Register(nameof(First), typeof(UIElement), null);

		public static readonly DependencyProperty SecondProperty = Register(nameof(Second), typeof(UIElement), null);

		public static readonly DependencyProperty FirstVisibleProperty = Register(nameof(FirstVisible), typeof(Boolean), true);

		public static readonly DependencyProperty FirstWeightProperty = Register(nameof(FirstWeight), typeof(Double), 1.0);

		public static readonly DependencyProperty SecondWeightProperty = Register(nameof(SecondWeight), typeof(Double), 1.0);

		/// <summary>The smallest width or height of a block.</summary>
		private const Double MinimumSize = 90;

		public Orientation Orientation
		{
			get => (Orientation)GetValue(OrientationProperty);
			set => SetValue(OrientationProperty, value);
		}

		public UIElement? First
		{
			get => (UIElement?)GetValue(FirstProperty);
			set => SetValue(FirstProperty, value);
		}

		public UIElement? Second
		{
			get => (UIElement?)GetValue(SecondProperty);
			set => SetValue(SecondProperty, value);
		}

		public Boolean FirstVisible
		{
			get => (Boolean)GetValue(FirstVisibleProperty);
			set => SetValue(FirstVisibleProperty, value);
		}

		/// <summary>The share of the room the first block gets (compared with <see cref="SecondWeight"/>).</summary>
		public Double FirstWeight
		{
			get => (Double)GetValue(FirstWeightProperty);
			set => SetValue(FirstWeightProperty, value);
		}

		public Double SecondWeight
		{
			get => (Double)GetValue(SecondWeightProperty);
			set => SetValue(SecondWeightProperty, value);
		}

		private static DependencyProperty Register(String name, Type type, Object? defaultValue)
		{
			return DependencyProperty.Register(name, type, typeof(SplitPair), new PropertyMetadata(defaultValue, (d, _) => ((SplitPair)d).Rebuild()));
		}

		/// <summary>Puts the blocks and the bar in the grid again, for the arrangement that is asked.</summary>
		private void Rebuild()
		{
			Children.Clear();
			ColumnDefinitions.Clear();
			RowDefinitions.Clear();

			Boolean sideBySide = Orientation == Orientation.Horizontal;
			Boolean both = FirstVisible && First != null;

			if (both)
			{
				AddDefinition(sideBySide, new GridLength(Math.Max(FirstWeight, 0.01), GridUnitType.Star));
				AddDefinition(sideBySide, GridLength.Auto, 0);
			}

			AddDefinition(sideBySide, new GridLength(Math.Max(SecondWeight, 0.01), GridUnitType.Star));

			Int32 index = 0;
			if (both)
			{
				Place(First!, sideBySide, index++);

				GridSplitter splitter = new();
				if (Application.Current?.TryFindResource(sideBySide ? "BlockSplitterStyle" : "BlockSplitterRowStyle") is Style style)
					splitter.Style = style;

				Place(splitter, sideBySide, index++);
			}

			if (Second != null)
				Place(Second, sideBySide, index);
		}

		private void AddDefinition(Boolean sideBySide, GridLength size, Double minimum = MinimumSize)
		{
			if (sideBySide)
			{
				ColumnDefinitions.Add(new ColumnDefinition { Width = size, MinWidth = minimum });
			}
			else
			{
				RowDefinitions.Add(new RowDefinition { Height = size, MinHeight = minimum });
			}
		}

		private void Place(UIElement element, Boolean sideBySide, Int32 index)
		{
			SetColumn(element, sideBySide ? index : 0);
			SetRow(element, sideBySide ? 0 : index);
			Children.Add(element);
		}
	}
}