namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media;
	using Infrastructure;

	/// <summary>
	/// Lets the items of a tree or of a list be dragged out of the program, to the file explorer: the view model makes real
	/// files of them (<see cref="IDragOutSource"/>) and the files are dragged.
	/// </summary>
	public static class FileDrag
	{
		/// <summary>
		/// The data format that marks what the program drags itself, so that its own lists and trees do not take it as files
		/// dropped from the explorer.
		/// </summary>
		public const String InternalFormat = "DatExplorerIII.FileDrag";

		public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
			"Source",
			typeof(IDragOutSource),
			typeof(FileDrag),
			new PropertyMetadata(null, OnSourceChanged));

		private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
			"State",
			typeof(DragState),
			typeof(FileDrag));

		public static IDragOutSource? GetSource(DependencyObject element)
		{
			return (IDragOutSource?)element.GetValue(SourceProperty);
		}

		public static void SetSource(DependencyObject element, IDragOutSource? value)
		{
			element.SetValue(SourceProperty, value);
		}

		private static void OnSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not UIElement element)
				return;

			element.PreviewMouseLeftButtonDown -= OnMouseDown;
			element.PreviewMouseMove -= OnMouseMove;
			if (e.NewValue != null)
			{
				element.PreviewMouseLeftButtonDown += OnMouseDown;
				element.PreviewMouseMove += OnMouseMove;
			}
		}

		private static DragState StateOf(UIElement element)
		{
			if (element.GetValue(StateProperty) is not DragState state)
			{
				state = new DragState();
				element.SetValue(StateProperty, state);
			}

			return state;
		}

		private static void OnMouseDown(Object sender, MouseButtonEventArgs e)
		{
			UIElement element = (UIElement)sender;
			DragState state = StateOf(element);
			state.Origin = e.GetPosition(null);
			state.Candidate = VisualTree.ItemAt(e.OriginalSource) != null;
		}

		private static void OnMouseMove(Object sender, MouseEventArgs e)
		{
			UIElement element = (UIElement)sender;
			DragState state = StateOf(element);
			if (e.LeftButton != MouseButtonState.Pressed || !state.Candidate || !IsDragStarted(state.Origin, e.GetPosition(null)))
				return;

			state.Candidate = false;
			if (GetSource(element) is not { } source || VisualTree.ItemAt(e.OriginalSource) is not { } item)
				return;

			// Extracting the files can take a while.
			IReadOnlyList<String>? paths;
			Mouse.OverrideCursor = Cursors.Wait;
			try
			{
				paths = source.PrepareDragOut(item.DataContext);
			}
			finally
			{
				Mouse.OverrideCursor = null;
			}

			if (paths == null || paths.Count == 0)
				return;

			DataObject data = new(DataFormats.FileDrop, paths.ToArray());
			data.SetData(InternalFormat, true);
			DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
		}

		private static Boolean IsDragStarted(Point origin, Point position)
		{
			return Math.Abs(position.X - origin.X) > SystemParameters.MinimumHorizontalDragDistance
				|| Math.Abs(position.Y - origin.Y) > SystemParameters.MinimumVerticalDragDistance;
		}

		private sealed class DragState
		{
			public Point Origin { get; set; }

			public Boolean Candidate { get; set; }
		}
	}
}