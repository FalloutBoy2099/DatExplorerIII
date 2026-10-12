namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;

	/// <summary>
	/// Keeps the widths of the columns of a list: the widths it is given are put on the columns of its <see cref="GridView"/> (each
	/// time the list shows a view with columns), and when the user lets go of the edge of a column the new widths are given back
	/// (bind it two ways). Widths that nobody changed are never given back, so the widths of the design can still improve.
	/// </summary>
	public static class GridViewColumnWidths
	{
		public static readonly DependencyProperty WidthsProperty = DependencyProperty.RegisterAttached(
			"Widths",
			typeof(IReadOnlyList<Double>),
			typeof(GridViewColumnWidths),
			new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnWidthsChanged));

		private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
			"State",
			typeof(State),
			typeof(GridViewColumnWidths));

		public static IReadOnlyList<Double>? GetWidths(DependencyObject list)
		{
			return (IReadOnlyList<Double>?)list.GetValue(WidthsProperty);
		}

		public static void SetWidths(DependencyObject list, IReadOnlyList<Double>? value)
		{
			list.SetValue(WidthsProperty, value);
		}

		private static void OnWidthsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is ListView list && !StateOf(list).Giving)
			{
				Apply(list);
			}
		}

		private static State StateOf(ListView list)
		{
			if (list.GetValue(StateProperty) is not State state)
			{
				state = new State();
				list.SetValue(StateProperty, state);

				// The view comes and goes with the display mode (details, list, icons): the widths are put again each time.
				DependencyPropertyDescriptor.FromProperty(ListView.ViewProperty, typeof(ListView)).AddValueChanged(list, (_, _) => Apply(list));
				// (The header handles the end of the drag itself, so the handler also wants the events that are handled.)
				list.AddHandler(Thumb.DragCompletedEvent, (DragCompletedEventHandler)OnDragCompleted, true);
			}

			return state;
		}

		private static void Apply(ListView list)
		{
			if (list.View is GridView view && GetWidths(list) is { } widths && widths.Count == view.Columns.Count)
			{
				for (Int32 index = 0; index < widths.Count; index++)
				{
					view.Columns[index].Width = widths[index];
				}
			}
		}

		/// <summary>The user let go of the edge of a column (which set its width): the widths of the columns are given back.</summary>
		private static void OnDragCompleted(Object sender, DragCompletedEventArgs e)
		{
			if (sender is not ListView { View: GridView view } list || e.OriginalSource is not DependencyObject source || VisualTree.FindAncestor<GridViewColumnHeader>(source) == null)
			{
				return;
			}

			List<Double> widths = view.Columns.Select(column => Math.Round(Double.IsNaN(column.Width) ? column.ActualWidth : column.Width)).ToList();
			if (widths.Any(width => width <= 0))
			{
				return;
			}

			State state = StateOf(list);
			state.Giving = true;
			try
			{
				SetWidths(list, widths);
			}
			finally
			{
				state.Giving = false;
			}
		}

		private sealed class State
		{
			/// <summary>True while the widths are being given back: they are already on the columns.</summary>
			public Boolean Giving { get; set; }
		}
	}
}