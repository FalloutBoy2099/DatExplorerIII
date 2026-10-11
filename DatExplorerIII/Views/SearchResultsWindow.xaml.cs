namespace DatExplorerIII.Views
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Input;
	using Models;
	using Services;

	/// <summary>
	/// Lists the files found by a search; activating one navigates to it.
	/// </summary>
	public partial class SearchResultsWindow : Window
	{
		private readonly Action<DatEntry> _goTo;

		public SearchResultsWindow(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			_goTo = goTo;
			Title = title;
			CaptionText.Text = Loc.Format("Str.SearchResultCount", results.Count);
			ResultsList.ItemsSource = results.Select(entry => new SearchResult(entry)).ToList();
		}

		private void OnResultActivated(Object sender, MouseButtonEventArgs e)
		{
			GoToSelected();
		}

		private void OnResultKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				GoToSelected();
			}
		}

		private void GoToSelected()
		{
			if (ResultsList.SelectedItem is SearchResult result)
			{
				_goTo(result.Entry);
			}
		}

		private sealed class SearchResult
		{
			public SearchResult(DatEntry entry)
			{
				Entry = entry;
			}

			public DatEntry Entry { get; }

			public String Name => Entry.Name;

			public String Folder => Entry.Directory;
		}
	}
}