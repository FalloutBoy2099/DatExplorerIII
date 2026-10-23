namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;

	/// <summary>
	/// Runs a command when an item of a list or a tree is double clicked (a double click on the header or on an empty place does
	/// nothing). The parameter of the command is the data of the item.
	/// </summary>
	public static class ItemActivation
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(ItemActivation),
			new PropertyMetadata(null, OnCommandChanged));

		public static ICommand? GetCommand(DependencyObject element)
		{
			return (ICommand?)element.GetValue(CommandProperty);
		}

		public static void SetCommand(DependencyObject element, ICommand? value)
		{
			element.SetValue(CommandProperty, value);
		}

		private static void OnCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not Control control)
				return;

			control.MouseDoubleClick -= OnDoubleClick;
			if (e.NewValue != null)
				control.MouseDoubleClick += OnDoubleClick;
		}

		private static void OnDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (VisualTree.ItemAt(e.OriginalSource) is not { } item || GetCommand((DependencyObject)sender) is not { } command)
				return;

			if (command.CanExecute(item.DataContext))
				command.Execute(item.DataContext);
		}
	}
}