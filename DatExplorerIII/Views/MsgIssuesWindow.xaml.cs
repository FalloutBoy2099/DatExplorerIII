namespace DatExplorerIII.Views
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using Services;
	using ViewModels;

	/// <summary>
	/// The problems found in message files (.msg): the files on the left, and on the right the problems of the selected file one at a time.
	/// </summary>
	public partial class MsgIssuesWindow : Window
	{
		public MsgIssuesWindow(MsgIssuesViewModel issues)
		{
			DataContext = issues;
			InitializeComponent();
			TitleBarTheme.Attach(this);
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			base.OnPreviewKeyDown(e);
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				Close();
			}
		}
	}
}