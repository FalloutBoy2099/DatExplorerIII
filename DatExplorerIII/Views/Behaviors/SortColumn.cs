namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;

	/// <summary>
	/// Sorting by a column of a list: each column of the <see cref="GridView"/> has a <see cref="KeyProperty"/>, and a click on its
	/// header gives that key to the command of the list.
	/// </summary>
	public static class SortColumn
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(SortColumn),
			new PropertyMetadata(null, OnCommandChanged));

		public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
			"Key",
			typeof(String),
			typeof(SortColumn));

		public static ICommand? GetCommand(DependencyObject element)
		{
			return (ICommand?)element.GetValue(CommandProperty);
		}

		public static void SetCommand(DependencyObject element, ICommand? value)
		{
			element.SetValue(CommandProperty, value);
		}

		/// <summary>What the command receives when the header of the column is clicked.</summary>
		public static String? GetKey(DependencyObject column)
		{
			return (String?)column.GetValue(KeyProperty);
		}

		public static void SetKey(DependencyObject column, String? value)
		{
			column.SetValue(KeyProperty, value);
		}

		private static void OnCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not UIElement element)
			{
				return;
			}

			element.RemoveHandler(ButtonBase.ClickEvent, (RoutedEventHandler)OnHeaderClick);
			if (e.NewValue != null)
			{
				element.AddHandler(ButtonBase.ClickEvent, (RoutedEventHandler)OnHeaderClick);
			}
		}

		private static void OnHeaderClick(Object sender, RoutedEventArgs e)
		{
			if (e.OriginalSource is not GridViewColumnHeader { Column: { } column } || GetKey(column) is not { } key)
			{
				return;
			}

			if (GetCommand((DependencyObject)sender) is { } command && command.CanExecute(key))
			{
				command.Execute(key);
			}
		}
	}
}