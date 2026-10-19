namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;

	/// <summary>
	/// Lets the user move a magnified picture by dragging it with the right button: the picture follows the mouse, as a page does
	/// under the hand. It is set on the <see cref="ScrollViewer"/> that holds the picture.
	/// </summary>
	public static class RightDragScroll
	{
		public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(Boolean),
			typeof(RightDragScroll),
			new PropertyMetadata(false, OnIsEnabledChanged));

		private static readonly DependencyProperty StartPointProperty = DependencyProperty.RegisterAttached(
			"StartPoint",
			typeof(Point?),
			typeof(RightDragScroll),
			new PropertyMetadata(null));

		private static readonly DependencyProperty StartOffsetProperty = DependencyProperty.RegisterAttached(
			"StartOffset",
			typeof(Vector),
			typeof(RightDragScroll),
			new PropertyMetadata(default(Vector)));

		public static Boolean GetIsEnabled(DependencyObject element)
		{
			return (Boolean)element.GetValue(IsEnabledProperty);
		}

		public static void SetIsEnabled(DependencyObject element, Boolean value)
		{
			element.SetValue(IsEnabledProperty, value);
		}

		private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not ScrollViewer viewer)
			{
				return;
			}

			viewer.PreviewMouseRightButtonDown -= OnMouseDown;
			viewer.PreviewMouseMove -= OnMouseMove;
			viewer.PreviewMouseRightButtonUp -= OnMouseUp;
			viewer.LostMouseCapture -= OnLostCapture;

			if ((Boolean)e.NewValue)
			{
				viewer.PreviewMouseRightButtonDown += OnMouseDown;
				viewer.PreviewMouseMove += OnMouseMove;
				viewer.PreviewMouseRightButtonUp += OnMouseUp;
				viewer.LostMouseCapture += OnLostCapture;
			}
		}

		private static void OnMouseDown(Object sender, MouseButtonEventArgs e)
		{
			ScrollViewer viewer = (ScrollViewer)sender;

			// Nothing to move when the picture fits.
			if (viewer.ScrollableWidth <= 0 && viewer.ScrollableHeight <= 0)
			{
				return;
			}

			viewer.SetValue(StartPointProperty, e.GetPosition(viewer));
			viewer.SetValue(StartOffsetProperty, new Vector(viewer.HorizontalOffset, viewer.VerticalOffset));
			viewer.CaptureMouse();
			viewer.Cursor = Cursors.SizeAll;
			e.Handled = true;
		}

		private static void OnMouseMove(Object sender, MouseEventArgs e)
		{
			ScrollViewer viewer = (ScrollViewer)sender;
			if (viewer.GetValue(StartPointProperty) is not Point start || e.RightButton != MouseButtonState.Pressed)
			{
				return;
			}

			Point now = e.GetPosition(viewer);
			Vector offset = (Vector)viewer.GetValue(StartOffsetProperty);
			viewer.ScrollToHorizontalOffset(offset.X - (now.X - start.X));
			viewer.ScrollToVerticalOffset(offset.Y - (now.Y - start.Y));
		}

		private static void OnMouseUp(Object sender, MouseButtonEventArgs e)
		{
			ScrollViewer viewer = (ScrollViewer)sender;
			if (viewer.GetValue(StartPointProperty) != null)
			{
				viewer.ReleaseMouseCapture();
				e.Handled = true;
			}
		}

		private static void OnLostCapture(Object sender, MouseEventArgs e)
		{
			ScrollViewer viewer = (ScrollViewer)sender;
			viewer.SetValue(StartPointProperty, null);
			viewer.ClearValue(FrameworkElement.CursorProperty);
		}
	}
}