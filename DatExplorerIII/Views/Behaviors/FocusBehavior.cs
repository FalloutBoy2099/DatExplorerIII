namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Controls;

	/// <summary>
	/// Gives the focus to an element each time the bound value changes (a counter a view model raises to ask for it), and selects
	/// the text of a text box.
	/// </summary>
	public static class FocusBehavior
	{
		public static readonly DependencyProperty TriggerProperty = DependencyProperty.RegisterAttached(
			"Trigger",
			typeof(Object),
			typeof(FocusBehavior),
			new PropertyMetadata(null, OnTriggerChanged));

		public static Object? GetTrigger(DependencyObject element)
		{
			return element.GetValue(TriggerProperty);
		}

		public static void SetTrigger(DependencyObject element, Object? value)
		{
			element.SetValue(TriggerProperty, value);
		}

		private static void OnTriggerChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
		{
			// The first value comes from the binding when the window loads: nobody asked for anything yet.
			if (e.OldValue == null || sender is not UIElement element)
				return;

			element.Focus();
			if (element is TextBox box)
				box.SelectAll();
		}
	}
}