namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;

	/// <summary>What a tree view needs that it does not do by itself: telling a command which item is selected, and selecting on a right click.</summary>
	public static class TreeViewBehavior
	{
		public static readonly DependencyProperty SelectedItemCommandProperty = DependencyProperty.RegisterAttached(
			"SelectedItemCommand",
			typeof(ICommand),
			typeof(TreeViewBehavior),
			new PropertyMetadata(null, OnSelectedItemCommandChanged));

		public static readonly DependencyProperty SelectOnRightClickProperty = DependencyProperty.RegisterAttached(
			"SelectOnRightClick",
			typeof(Boolean),
			typeof(TreeViewBehavior),
			new PropertyMetadata(false, OnSelectOnRightClickChanged));

		/// <summary>The command that receives the data of the selected item (null when nothing is selected) each time it changes.</summary>
		public static ICommand? GetSelectedItemCommand(DependencyObject element)
		{
			return (ICommand?)element.GetValue(SelectedItemCommandProperty);
		}

		public static void SetSelectedItemCommand(DependencyObject element, ICommand? value)
		{
			element.SetValue(SelectedItemCommandProperty, value);
		}

		/// <summary>True to select the item under the mouse before a context menu opens on it, as the file explorer does.</summary>
		public static Boolean GetSelectOnRightClick(DependencyObject element)
		{
			return (Boolean)element.GetValue(SelectOnRightClickProperty);
		}

		public static void SetSelectOnRightClick(DependencyObject element, Boolean value)
		{
			element.SetValue(SelectOnRightClickProperty, value);
		}

		private static void OnSelectedItemCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not TreeView tree)
				return;

			tree.SelectedItemChanged -= OnSelectedItemChanged;
			if (e.NewValue != null)
				tree.SelectedItemChanged += OnSelectedItemChanged;
		}

		private static void OnSelectedItemChanged(Object sender, RoutedPropertyChangedEventArgs<Object> e)
		{
			if (GetSelectedItemCommand((DependencyObject)sender) is { } command && command.CanExecute(e.NewValue))
				command.Execute(e.NewValue);
		}

		private static void OnSelectOnRightClickChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not UIElement element)
				return;

			element.PreviewMouseRightButtonDown -= OnRightButtonDown;
			if (e.NewValue is true)
				element.PreviewMouseRightButtonDown += OnRightButtonDown;
		}

		private static void OnRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			if (VisualTree.FindAncestor<TreeViewItem>(e.OriginalSource) is { } item)
				item.IsSelected = true;
		}
	}
}