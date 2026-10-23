namespace DatExplorerIII.Views
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using Services.Interface;

	/// <summary>
	/// Asks the user for a single line of text, with live validation.
	/// </summary>
	public partial class InputDialog : Window
	{
		private readonly Func<String, String?>? _validate;

		public InputDialog(String title, String prompt, String initialValue, Func<String, String?>? validate)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Title = title;
			PromptText.Text = prompt;
			_validate = validate;
			ValueBox.Text = initialValue;

			Loaded += (_, _) =>
			{
				ValueBox.Focus();
				ValueBox.SelectAll();
			};
		}

		public String Value => ValueBox.Text.Trim();

		private void OnValueChanged(Object sender, TextChangedEventArgs e)
		{
			String? error = _validate?.Invoke(Value);
			ErrorText.Text = error ?? String.Empty;
			OkButton.IsEnabled = error == null;
		}

		private void OnOk(Object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}