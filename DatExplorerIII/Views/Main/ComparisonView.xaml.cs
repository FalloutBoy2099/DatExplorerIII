namespace DatExplorerIII.Views.Main
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Globalization;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using Core;

	/// <summary>The comparison of two archives: it takes the place of the folders, the files and the preview.</summary>
	public partial class ComparisonView : UserControl
	{
		/// <summary>The number column and the room the cells keep around their text.</summary>
		private const Double NumbersWidth = 44;
		private const Double CellPadding = 16;

		public ComparisonView()
		{
			InitializeComponent();
			foreach (DataGrid grid in new[] { DiffLeftGrid, DiffRightGrid })
			{
				DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(DataGrid)).AddValueChanged(grid, (_, _) => FitDiffColumns());
				grid.SizeChanged += (_, _) => FitDiffColumns();
			}
		}

		/// <summary>
		/// Each text is in a grid of its own, with its own horizontal scroll bar (the vertical ones follow each other). A line that is
		/// longer than the grid makes its column as wide as the longest line, so that the line can be reached instead of being cut.
		/// </summary>
		private void FitDiffColumns()
		{
			IReadOnlyList<DiffRow> rows = DiffLeftGrid.ItemsSource as IReadOnlyList<DiffRow> ?? [];
			Double character = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(DiffLeftGrid.FontFamily, DiffLeftGrid.FontStyle, DiffLeftGrid.FontWeight, DiffLeftGrid.FontStretch), DiffLeftGrid.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(DiffLeftGrid).PixelsPerDip).WidthIncludingTrailingWhitespace;

			Apply(DiffLeftGrid, rows.Count == 0 ? 0 : rows.Max(row => row.LeftText.Length) * character + CellPadding);
			Apply(DiffRightGrid, rows.Count == 0 ? 0 : rows.Max(row => row.RightText.Length) * character + CellPadding);
		}

		private static void Apply(DataGrid grid, Double needed)
		{
			if (grid.Columns.Count < 2)
				return;

			Double available = grid.ActualWidth - NumbersWidth - SystemParameters.VerticalScrollBarWidth;
			grid.Columns[1].Width = needed <= available ? new DataGridLength(1, DataGridLengthUnitType.Star) : new DataGridLength(needed);
		}
	}
}
