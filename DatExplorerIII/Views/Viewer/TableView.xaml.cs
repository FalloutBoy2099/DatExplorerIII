namespace DatExplorerIII.Views.Viewer
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using ViewModels.Viewer;

	/// <summary>A table: the messages, the dialogs, the variables.</summary>
	public partial class TableView : UserControl
	{
		public TableView()
		{
			InitializeComponent();
		}

		private void OnAutoGeneratingColumn(Object? sender, DataGridAutoGeneratingColumnEventArgs e)
		{
			if (((FrameworkElement)sender!).DataContext is not TableViewerViewModel table)
			{
				return;
			}

			// The mark of the lines that are comments is not for the user: the lines are colored by it.
			if (e.PropertyName == "IsComment")
			{
				e.Cancel = true;
				return;
			}

			e.Column.Header = table.GetHeader(e.PropertyName);
			if (e.Column is DataGridTextColumn column)
			{
				Boolean isText = e.PropertyName is "Text" or "Text2" or "Comment";
				column.Width = isText ? new DataGridLength(340) : DataGridLength.Auto;
				if (isText)
				{
					column.ElementStyle = (Style)FindResource("WrapCell");
				}
			}
		}
	}
}