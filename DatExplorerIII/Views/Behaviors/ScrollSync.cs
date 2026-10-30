namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Windows;
	using System.Windows.Controls;

	/// <summary>
	/// Makes two parts of the interface scroll together (the two versions of a compared file): each one names the other as its
	/// <see cref="PartnerProperty"/>, and when a scroll bar of one moves, the matching scroll bar of the other goes to the same place.
	/// </summary>
	public static class ScrollSync
	{
		public static readonly DependencyProperty PartnerProperty = DependencyProperty.RegisterAttached(
			"Partner",
			typeof(UIElement),
			typeof(ScrollSync),
			new PropertyMetadata(null, OnPartnerChanged));

		/// <summary>Whether the two follow each other (the user can turn it off).</summary>
		public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
			"IsEnabled",
			typeof(Boolean),
			typeof(ScrollSync),
			new PropertyMetadata(false));

		/// <summary>Whether the vertical scroll bars follow each other (the default).</summary>
		public static readonly DependencyProperty SyncVerticalProperty = DependencyProperty.RegisterAttached(
			"SyncVertical",
			typeof(Boolean),
			typeof(ScrollSync),
			new PropertyMetadata(true));

		/// <summary>Whether the horizontal scroll bars follow each other (the default).</summary>
		public static readonly DependencyProperty SyncHorizontalProperty = DependencyProperty.RegisterAttached(
			"SyncHorizontal",
			typeof(Boolean),
			typeof(ScrollSync),
			new PropertyMetadata(true));

		/// <summary>The scroll bars that were moved by the program to follow the other part (not by the user), and where it put them.</summary>
		private static readonly ConditionalWeakTable<ScrollViewer, Followed> FollowedBars = [];

		public static UIElement? GetPartner(DependencyObject element)
		{
			return (UIElement?)element.GetValue(PartnerProperty);
		}

		public static void SetPartner(DependencyObject element, UIElement? value)
		{
			element.SetValue(PartnerProperty, value);
		}

		public static Boolean GetIsEnabled(DependencyObject element)
		{
			return (Boolean)element.GetValue(IsEnabledProperty);
		}

		public static void SetIsEnabled(DependencyObject element, Boolean value)
		{
			element.SetValue(IsEnabledProperty, value);
		}

		public static Boolean GetSyncVertical(DependencyObject element)
		{
			return (Boolean)element.GetValue(SyncVerticalProperty);
		}

		public static void SetSyncVertical(DependencyObject element, Boolean value)
		{
			element.SetValue(SyncVerticalProperty, value);
		}

		public static Boolean GetSyncHorizontal(DependencyObject element)
		{
			return (Boolean)element.GetValue(SyncHorizontalProperty);
		}

		public static void SetSyncHorizontal(DependencyObject element, Boolean value)
		{
			element.SetValue(SyncHorizontalProperty, value);
		}

		private static void OnPartnerChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not UIElement element)
				return;

			element.RemoveHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnScrollChanged);
			if (e.NewValue != null)
				element.AddHandler(ScrollViewer.ScrollChangedEvent, (ScrollChangedEventHandler)OnScrollChanged);
		}

		private static void OnScrollChanged(Object sender, ScrollChangedEventArgs e)
		{
			DependencyObject own = (DependencyObject)sender;
			Boolean vertical = GetSyncVertical(own);
			Boolean horizontal = GetSyncHorizontal(own);
			if (!GetIsEnabled(own) || GetPartner(own) is not { } other || e.OriginalSource is not ScrollViewer source || ((e.VerticalChange == 0 || !vertical) && (e.HorizontalChange == 0 || !horizontal)))
				return;

			// The scroll bar that was just moved by the program is not followed back.
			if (FollowedBars.TryGetValue(source, out Followed? followed))
			{
				FollowedBars.Remove(source);
				if (Math.Abs(source.VerticalOffset - followed.Vertical) < 1 && Math.Abs(source.HorizontalOffset - followed.Horizontal) < 1)
					return;
			}

			Int32 index = VisualTree.ScrollViewersOf((DependencyObject)sender).IndexOf(source);
			List<ScrollViewer> candidates = VisualTree.ScrollViewersOf(other);
			if (index < 0 || index >= candidates.Count)
				return;

			ScrollViewer target = candidates[index];
			Double toVertical = vertical ? Math.Min(source.VerticalOffset, target.ScrollableHeight) : target.VerticalOffset;
			Double toHorizontal = horizontal ? Math.Min(source.HorizontalOffset, target.ScrollableWidth) : target.HorizontalOffset;
			if (Math.Abs(target.VerticalOffset - toVertical) < 0.5 && Math.Abs(target.HorizontalOffset - toHorizontal) < 0.5)
				return;

			FollowedBars.AddOrUpdate(target, new Followed(toVertical, toHorizontal));
			target.ScrollToVerticalOffset(toVertical);
			target.ScrollToHorizontalOffset(toHorizontal);
		}

		private sealed record Followed(Double Vertical, Double Horizontal);
	}
}