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
			if (!GetIsEnabled(own) || GetPartner(own) is not { } other || e.OriginalSource is not ScrollViewer source || (e.VerticalChange == 0 && e.HorizontalChange == 0))
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
			Double vertical = Math.Min(source.VerticalOffset, target.ScrollableHeight);
			Double horizontal = Math.Min(source.HorizontalOffset, target.ScrollableWidth);
			if (Math.Abs(target.VerticalOffset - vertical) < 0.5 && Math.Abs(target.HorizontalOffset - horizontal) < 0.5)
				return;

			FollowedBars.AddOrUpdate(target, new Followed(vertical, horizontal));
			target.ScrollToVerticalOffset(vertical);
			target.ScrollToHorizontalOffset(horizontal);
		}

		private sealed record Followed(Double Vertical, Double Horizontal);
	}
}