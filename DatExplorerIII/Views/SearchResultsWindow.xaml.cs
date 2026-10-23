namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using Models;
	using ViewModels;

	/// <summary>
	/// Lists the files found by a search; activating one navigates to it.
	/// </summary>
	public partial class SearchResultsWindow : Window
	{
		public SearchResultsWindow(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
		{
			DataContext = new SearchResultsViewModel(results, goTo);
			InitializeComponent();
			TitleBarTheme.Attach(this);
			Title = title;
		}
	}
}