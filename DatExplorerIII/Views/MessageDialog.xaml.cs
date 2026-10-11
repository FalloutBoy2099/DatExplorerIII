namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using Models;

	/// <summary>
	/// A themed message box with configurable buttons.
	/// </summary>
	public partial class MessageDialog : Window
	{
		public MessageDialog(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Title = title;
			MessageText.Text = message;
			IconText.Text = GetGlyph(icon);
			IconText.Visibility = icon == DialogIcon.None ? Visibility.Collapsed : Visibility.Visible;

			foreach (DialogButton button in buttons)
			{
				Button control = new()
				{
					Content = button.Label,
					MinWidth = 88,
					Margin = new Thickness(8, 0, 0, 0),
					IsDefault = button.IsDefault,
					IsCancel = button.IsCancel,
				};

				control.Click += (_, _) =>
				{
					SelectedId = button.Id;
					DialogResult = true;
				};

				ButtonsPanel.Children.Add(control);
			}
		}

		/// <summary>Id of the pressed button; null when the dialog was closed otherwise.</summary>
		public String? SelectedId { get; private set; }

		private static String GetGlyph(DialogIcon icon)
		{
			return icon switch
			{
				DialogIcon.Information => "",
				DialogIcon.Question => "",
				DialogIcon.Warning => "",
				DialogIcon.Error => "",
				_ => String.Empty,
			};
		}
	}
}