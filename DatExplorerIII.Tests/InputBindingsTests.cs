namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows.Input;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Main;

	/// <summary>
	/// The shortcuts and the drag and drop are written in the XAML, where nothing checks them until a key is pressed: these tests
	/// read it and check that what it binds exists.
	/// </summary>
	public sealed class InputBindingsTests
	{
		/// <summary>The XAML of the main window: the window and its parts (title bar, menu, tool bar, explorer, comparison...), in one text.</summary>
		private static String ReadMainWindowXaml()
		{
			String folder = System.IO.Path.Combine(SourceFiles.ProjectFolder, "Views", "Main");
			return String.Concat(System.IO.Directory.GetFiles(folder, "*.xaml").OrderBy(path => path, StringComparer.Ordinal).Select(System.IO.File.ReadAllText));
		}

		[Fact]
		public void EveryKeyBinding_NamesACommandOfTheViewModel()
		{
			String xaml = ReadMainWindowXaml();
			List<String> names = Regex.Matches(xaml, "<KeyBinding[\\s][^>]*Command=\"\\{Binding (\\w+)")
				.Select(match => match.Groups[1].Value)
				.ToList();

			// Window, search box, tree and list: the shortcuts of the program.
			Assert.True(names.Count == 16, "Found only " + names.Count + " key bindings.");
			foreach (String name in names)
			{
				Assert.True(
					typeof(MainViewModel).GetProperty(name)?.PropertyType == typeof(ICommand),
					name + " is not a command of MainViewModel.");
			}
		}

		/// <summary>Follows a path of properties (Comparison.SelectRowsCommand) from the type of the view model; null when one is missing.</summary>
		private static Type? Resolve(String path)
		{
			Type type = typeof(MainViewModel);
			foreach (String name in path.Split('.'))
			{
				PropertyInfo? property = type.GetProperty(name);
				if (property == null)
					return null;

				type = property.PropertyType;
			}

			return type;
		}

		[Fact]
		public void TheBehaviorBindings_NameAnExistingProperty()
		{
			String xaml = ReadMainWindowXaml();
			List<String> paths = Regex.Matches(xaml, "behaviors:\\w+\\.\\w+=\"\\{Binding ([\\w.]+)[,}]")
				.Select(match => match.Groups[1].Value)
				.ToList();

			// The drag and drop, the focus, the selections, the sort, the double click, the placement ("Binding" alone is the view model).
			foreach (String expected in new[] { "FolderDragSource", "ImportDroppedCommand", "ListDragSource", "OpenDroppedCommand", "SearchFocusRequests", "TreeSelectionChangedCommand", "ListSelectionChangedCommand", "SortCommand", "OpenItemCommand", "Comparison.Current.SelectRowsCommand", "IsCompareSyncScroll" })
			{
				Assert.Contains(expected, paths);
			}

			foreach (String path in paths)
			{
				Assert.True(Resolve(path) != null, path + " is not a property of the view model.");
			}
		}

		[Fact]
		public void EveryBindingToTheComparison_NamesAPropertyThatExists()
		{
			// The comparison moved out of the main view model: a path left behind would only fail on a screen nobody looks at in the tests.
			List<String> paths = Regex.Matches(ReadMainWindowXaml(), "\\{Binding (Comparison\\.[\\w.]+)")
				.Select(match => match.Groups[1].Value)
				.Distinct()
				.ToList();

			Assert.True(paths.Count > 40, "Found only " + paths.Count + " bindings to the comparison.");
			foreach (String path in paths)
			{
				Assert.True(Resolve(path) != null, path + " is not a property of the view model.");
			}
		}

		[Fact]
		public void EveryColumnOfTheList_HasTheKeyOfASortColumn()
		{
			String xaml = ReadMainWindowXaml();
			List<String> keys = Regex.Matches(xaml, "SortColumn\\.Key=\"(\\w+)\"").Select(match => match.Groups[1].Value).ToList();

			Assert.Equal(4, keys.Count);
			foreach (String key in keys)
			{
				Assert.True(Enum.TryParse<DatSortColumn>(key, out _), key + " is not a column the list can be sorted by.");
			}
		}

		[Fact]
		public void TheWindow_NoLongerHandlesKeysOrDropsInItsCodeBehind()
		{
			String xaml = ReadMainWindowXaml();

			Assert.DoesNotMatch("(?<![A-Za-z])(PreviewKeyDown|KeyDown|DragOver|Drop|PreviewMouseMove|PreviewMouseLeftButtonDown|PreviewMouseRightButtonDown|MouseDoubleClick|SelectionChanged|SelectedItemChanged|ScrollChanged)=\"", xaml);
		}
	}
}