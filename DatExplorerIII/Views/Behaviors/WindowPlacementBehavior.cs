namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using Infrastructure;
	using Models;

	/// <summary>
	/// Puts a window where it was when the program ended last time, and keeps where it is when it closes. What is kept, and where,
	/// is the business of the <see cref="IWindowPlacementStore"/>.
	/// </summary>
	public static class WindowPlacementBehavior
	{
		public static readonly DependencyProperty StoreProperty = DependencyProperty.RegisterAttached(
			"Store",
			typeof(IWindowPlacementStore),
			typeof(WindowPlacementBehavior),
			new PropertyMetadata(null, OnStoreChanged));

		public static IWindowPlacementStore? GetStore(DependencyObject window)
		{
			return (IWindowPlacementStore?)window.GetValue(StoreProperty);
		}

		public static void SetStore(DependencyObject window, IWindowPlacementStore? value)
		{
			window.SetValue(StoreProperty, value);
		}

		private static void OnStoreChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not Window window)
			{
				return;
			}

			window.Closing -= OnClosing;
			if (e.NewValue is IWindowPlacementStore store)
			{
				Restore(window, store);
				window.Closing += OnClosing;
			}
		}

		/// <summary>Puts the window where it was; the first time, or when nothing is kept, it opens in the middle of the screen.</summary>
		private static void Restore(Window window, IWindowPlacementStore store)
		{
			Boolean useSize = store.SaveWindowSize;
			Boolean usePosition = store.SaveWindowPosition;
			if (store.LoadPlacement() is not { } saved || (!useSize && !usePosition))
			{
				window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
				return;
			}

			// What the user does not want kept is the default: the size of the design, the middle of the screen.
			WindowPlacement placement = new WindowPlacement(
				saved.Left,
				saved.Top,
				useSize ? saved.Width : window.Width,
				useSize ? saved.Height : window.Height,
				useSize && saved.IsMaximized).FitInto(
				SystemParameters.VirtualScreenLeft,
				SystemParameters.VirtualScreenTop,
				SystemParameters.VirtualScreenWidth,
				SystemParameters.VirtualScreenHeight);

			window.Width = Math.Max(placement.Width, window.MinWidth);
			window.Height = Math.Max(placement.Height, window.MinHeight);
			if (usePosition)
			{
				window.WindowStartupLocation = WindowStartupLocation.Manual;
				window.Left = placement.Left;
				window.Top = placement.Top;
			}
			else
			{
				window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			}

			if (placement.IsMaximized)
			{
				window.WindowState = WindowState.Maximized;
			}
		}

		private static void OnClosing(Object? sender, CancelEventArgs e)
		{
			if (e.Cancel || sender is not Window window || GetStore(window) is not { } store)
			{
				return;
			}

			Rect bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
			store.SavePlacement(new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, window.WindowState == WindowState.Maximized));
		}
	}
}