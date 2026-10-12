namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;

	/// <summary>Looks in the visual tree, for the behaviors.</summary>
	internal static class VisualTree
	{
		/// <summary>The first element of that type at or above <paramref name="start"/>.</summary>
		public static T? FindAncestor<T>(Object? start)
			where T : DependencyObject
		{
			DependencyObject? current = start as DependencyObject;
			while (current != null)
			{
				if (current is T match)
				{
					return match;
				}

				current = Parent(current);
			}

			return null;
		}

		/// <summary>The item (a tree item, a list item) that holds the element under the mouse, if there is one.</summary>
		public static FrameworkElement? ItemAt(Object? original)
		{
			DependencyObject? current = original as DependencyObject;
			while (current != null)
			{
				if (current is TreeViewItem or ListBoxItem)
				{
					return (FrameworkElement)current;
				}

				current = Parent(current);
			}

			return null;
		}

		/// <summary>Every scroll viewer under <paramref name="root"/>, in the order of the tree.</summary>
		public static List<ScrollViewer> ScrollViewersOf(DependencyObject root)
		{
			List<ScrollViewer> found = new();
			void Walk(DependencyObject node)
			{
				for (Int32 index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(node, index);
					if (child is ScrollViewer viewer)
					{
						found.Add(viewer);
					}

					Walk(child);
				}
			}

			Walk(root);
			return found;
		}

		private static DependencyObject? Parent(DependencyObject node)
		{
			return node is Visual or System.Windows.Media.Media3D.Visual3D
				? VisualTreeHelper.GetParent(node)
				: LogicalTreeHelper.GetParent(node);
		}
	}
}