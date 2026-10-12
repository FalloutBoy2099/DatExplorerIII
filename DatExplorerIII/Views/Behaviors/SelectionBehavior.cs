namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;

	/// <summary>
	/// Tells a command what is selected in a list or a grid each time the selection changes: the command receives the selected
	/// items (an <c>IReadOnlyList&lt;Object&gt;</c>) as its parameter.
	/// </summary>
	public static class SelectionBehavior
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(SelectionBehavior),
			new PropertyMetadata(null, OnCommandChanged));

		public static ICommand? GetCommand(DependencyObject element)
		{
			return (ICommand?)element.GetValue(CommandProperty);
		}

		public static void SetCommand(DependencyObject element, ICommand? value)
		{
			element.SetValue(CommandProperty, value);
		}

		/// <summary>The items that are selected, whatever the selection mode of the control is.</summary>
		public static IReadOnlyList<Object> SelectedOf(Selector selector)
		{
			IList? items = selector switch
			{
				ListBox listBox => listBox.SelectedItems,
				MultiSelector multiSelector => multiSelector.SelectedItems,
				_ => null,
			};

			if (items != null)
			{
				return items.Cast<Object>().ToList();
			}

			return selector.SelectedItem == null ? Array.Empty<Object>() : [selector.SelectedItem];
		}

		private static void OnCommandChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is not Selector selector)
			{
				return;
			}

			selector.SelectionChanged -= OnSelectionChanged;
			if (e.NewValue != null)
			{
				selector.SelectionChanged += OnSelectionChanged;
			}
		}

		private static void OnSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			// The event also comes from the controls inside the items.
			if (!ReferenceEquals(e.OriginalSource, sender))
			{
				return;
			}

			Selector selector = (Selector)sender;
			IReadOnlyList<Object> selected = SelectedOf(selector);
			if (GetCommand(selector) is { } command && command.CanExecute(selected))
			{
				command.Execute(selected);
			}
		}
	}
}