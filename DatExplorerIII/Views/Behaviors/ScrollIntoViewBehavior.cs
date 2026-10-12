namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;

	/// <summary>Scrolls a list or a grid to its selected item each time the selection changes (the selection can be moved by code).</summary>
	public static class ScrollIntoViewBehavior
	{
		public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
			"Enabled",
			typeof(Boolean),
			typeof(ScrollIntoViewBehavior),
			new PropertyMetadata(false, OnEnabledChanged));

		public static Boolean GetEnabled(DependencyObject element)
		{
			return (Boolean)element.GetValue(EnabledProperty);
		}

		public static void SetEnabled(DependencyObject element, Boolean value)
		{
			element.SetValue(EnabledProperty, value);
		}

		private static void OnEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not Selector selector)
			{
				return;
			}

			selector.SelectionChanged -= OnSelectionChanged;
			if (e.NewValue is true)
			{
				selector.SelectionChanged += OnSelectionChanged;
			}
		}

		private static void OnSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			if (!ReferenceEquals(e.OriginalSource, sender) || ((Selector)sender).SelectedItem is not { } item)
			{
				return;
			}

			switch (sender)
			{
				case DataGrid grid:
					grid.ScrollIntoView(item);
					break;
				case ListBox list:
					list.ScrollIntoView(item);
					break;
			}
		}
	}
}