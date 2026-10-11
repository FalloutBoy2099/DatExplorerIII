namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows.Input;
	using DatExplorerIII.ViewModels;

	/// <summary>
	/// The shortcuts and the drag and drop are written in the XAML, where nothing checks them until a key is pressed: these tests
	/// read it and check that what it binds exists.
	/// </summary>
	public sealed class InputBindingsTests
	{
		private static String ReadMainWindowXaml()
		{
			String? folder = AppContext.BaseDirectory;
			while (folder != null)
			{
				String candidate = Path.Combine(folder, "DatExplorerIII", "Views", "MainWindow.xaml");
				if (File.Exists(candidate))
				{
					return File.ReadAllText(candidate);
				}

				folder = Path.GetDirectoryName(folder);
			}

			throw new FileNotFoundException("MainWindow.xaml was not found above " + AppContext.BaseDirectory);
		}

		[Fact]
		public void EveryKeyBinding_NamesACommandOfTheViewModel()
		{
			String xaml = ReadMainWindowXaml();
			List<String> names = Regex.Matches(xaml, "<KeyBinding[\\s][^>]*Command=\"\\{Binding (\\w+)")
				.Select(match => match.Groups[1].Value)
				.ToList();

			// Window, search box, tree and list: the shortcuts of the program.
			Assert.True(names.Count == 15, "Found only " + names.Count + " key bindings.");
			foreach (String name in names)
			{
				Assert.True(
					typeof(MainViewModel).GetProperty(name)?.PropertyType == typeof(ICommand),
					name + " is not a command of MainViewModel.");
			}
		}

		[Fact]
		public void TheDragAndDropAndFocusBindings_NameAnExistingProperty()
		{
			String xaml = ReadMainWindowXaml();
			List<String> names = Regex.Matches(xaml, "behaviors:\\w+\\.\\w+=\"\\{Binding (\\w+)")
				.Select(match => match.Groups[1].Value)
				.ToList();

			Assert.Equal(
				["FolderDragSource", "ImportDroppedCommand", "ListDragSource", "OpenDroppedCommand", "SearchFocusRequests"],
				names.Order().ToList());
			foreach (String name in names)
			{
				Assert.NotNull(typeof(MainViewModel).GetProperty(name));
			}
		}

		[Fact]
		public void TheWindow_NoLongerHandlesKeysOrDropsInItsCodeBehind()
		{
			String xaml = ReadMainWindowXaml();

			Assert.DoesNotMatch("(?<![A-Za-z])(PreviewKeyDown|KeyDown|DragOver|Drop|PreviewMouseMove|PreviewMouseLeftButtonDown)=\"", xaml);
		}
	}
}