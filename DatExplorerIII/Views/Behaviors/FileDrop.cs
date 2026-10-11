namespace DatExplorerIII.Views.Behaviors
{
	using System;
	using System.Windows;
	using System.Windows.Input;

	/// <summary>
	/// Makes an element accept files dropped from the file explorer. The command receives the paths (an
	/// <c>IReadOnlyList&lt;String&gt;</c>) as its parameter; what it cannot execute is refused while the files are over the
	/// element, so the pointer shows it.
	/// </summary>
	public static class FileDrop
	{
		public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
			"Command",
			typeof(ICommand),
			typeof(FileDrop),
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
			if (sender is not UIElement element)
			{
				return;
			}

			element.DragOver -= OnDragOver;
			element.Drop -= OnDrop;
			if (e.NewValue != null)
			{
				element.AllowDrop = true;
				element.DragOver += OnDragOver;
				element.Drop += OnDrop;
			}
		}

		/// <summary>The paths of the files in the data, or null when it is not a list of files or comes from the program itself.</summary>
		private static String[]? PathsOf(IDataObject data)
		{
			if (data.GetDataPresent(FileDrag.InternalFormat) || !data.GetDataPresent(DataFormats.FileDrop))
			{
				return null;
			}

			return data.GetData(DataFormats.FileDrop) as String[];
		}

		private static void OnDragOver(Object sender, DragEventArgs e)
		{
			ICommand? command = GetCommand((DependencyObject)sender);
			String[]? paths = PathsOf(e.Data);
			e.Effects = command != null && paths != null && command.CanExecute(paths) ? DragDropEffects.Copy : DragDropEffects.None;
			e.Handled = true;
		}

		private static void OnDrop(Object sender, DragEventArgs e)
		{
			ICommand? command = GetCommand((DependencyObject)sender);
			String[]? paths = PathsOf(e.Data);
			if (command != null && paths != null && command.CanExecute(paths))
			{
				command.Execute(paths);
			}

			e.Handled = true;
		}
	}
}