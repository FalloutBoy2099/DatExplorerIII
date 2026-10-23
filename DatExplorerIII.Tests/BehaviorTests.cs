namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Threading;
	using DatExplorerIII.Infrastructure;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Views.Behaviors;

	/// <summary>The attached behaviors of the main window, on real controls.</summary>
	public sealed class BehaviorTests
	{
		private static void RunOnStaThread(Action action)
		{
			Exception? failure = null;
			Thread thread = new(() =>
			{
				try
				{
					action();
				}
				catch (Exception exception)
				{
					failure = exception;
				}
			});

			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			thread.Join();
			if (failure != null)
				throw new InvalidOperationException("The behavior test failed.", failure);
		}

		/// <summary>Lets the layout and what it posts happen (the scroll bars tell their changes after a layout).</summary>
		private static void Pump(Window window)
		{
			for (Int32 i = 0; i < 4; i++)
			{
				window.UpdateLayout();
				window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
			}
		}

		private static Window Show(UIElement content)
		{
			Window window = new() { Content = content, Width = 400, Height = 400, ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = -5000, Top = 0 };
			window.Show();
			Pump(window);
			return window;
		}

		private static MouseButtonEventArgs MouseArgs(RoutedEvent routedEvent, MouseButton button, Object source)
		{
			return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button) { RoutedEvent = routedEvent, Source = source };
		}

		[Fact]
		public void SelectionBehavior_GivesTheSelectedItemsToTheCommand()
		{
			RunOnStaThread(() =>
			{
				List<IReadOnlyList<Object>> received = [];
				ListBox list = new() { SelectionMode = SelectionMode.Extended, ItemsSource = new[] { "a", "b", "c" } };
				SelectionBehavior.SetCommand(list, new RelayCommand(parameter => received.Add((IReadOnlyList<Object>)parameter!)));

				list.SelectedItems.Add("b");
				list.SelectedItems.Add("c");

				Assert.Equal(["b"], received[0]);
				Assert.Equal(["b", "c"], received[^1]);

				// Taking the behavior away stops the calls.
				Int32 count = received.Count;
				SelectionBehavior.SetCommand(list, null);
				list.SelectedItems.Clear();
				Assert.Equal(count, received.Count);
			});
		}

		[Fact]
		public void SelectionBehavior_ASingleSelectionControl_GivesAListOfOne()
		{
			RunOnStaThread(() =>
			{
				List<IReadOnlyList<Object>> received = [];
				ComboBox box = new() { ItemsSource = new[] { "x", "y" } };
				SelectionBehavior.SetCommand(box, new RelayCommand(parameter => received.Add((IReadOnlyList<Object>)parameter!)));

				box.SelectedIndex = 1;

				Assert.Equal(["y"], received[^1]);
			});
		}

		[Fact]
		public void SortColumn_GivesTheKeyOfTheClickedColumn_ToTheCommand()
		{
			RunOnStaThread(() =>
			{
				GridView view = new();
				foreach (String key in new[] { "Name", "Size" })
				{
					GridViewColumn column = new() { Header = key };
					SortColumn.SetKey(column, key);
					view.Columns.Add(column);
				}

				GridViewColumn withoutKey = new() { Header = "Other" };
				view.Columns.Add(withoutKey);

				List<Object?> received = [];
				ListView list = new() { View = view };
				SortColumn.SetCommand(list, new RelayCommand(parameter => received.Add(parameter)));
				Window window = Show(list);
				try
				{
					List<GridViewColumnHeader> headers = [];
					void Collect(DependencyObject node)
					{
						for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
						{
							DependencyObject child = VisualTreeHelper.GetChild(node, i);
							if (child is GridViewColumnHeader { Role: GridViewColumnHeaderRole.Normal } header)
								headers.Add(header);

							Collect(child);
						}
					}

					Collect(list);
					Assert.Equal(3, headers.Count);

					headers[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
					headers[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
					headers[2].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

					// A column without a key does not sort.
					Assert.Equal(["Size", "Name"], received);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void ItemActivation_RunsTheCommandForTheItemThatWasDoubleClicked_NotForTheRest()
		{
			RunOnStaThread(() =>
			{
				List<Object?> received = [];
				ListBox list = new() { ItemsSource = new[] { "first", "second" } };
				ItemActivation.SetCommand(list, new RelayCommand(parameter => received.Add(parameter)));
				Window window = Show(list);
				try
				{
					ListBoxItem item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
					// (A double click is raised on the list, with the item under the mouse as the original source.)
					list.RaiseEvent(MouseArgs(Control.MouseDoubleClickEvent, MouseButton.Left, item));
					Assert.Equal(["second"], received);

					// A double click on the list itself (an empty place, a header) does nothing.
					list.RaiseEvent(MouseArgs(Control.MouseDoubleClickEvent, MouseButton.Left, list));
					Assert.Single(received);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void TreeViewBehavior_TellsTheCommandWhatIsSelected_AndSelectsOnARightClick()
		{
			RunOnStaThread(() =>
			{
				List<Object?> received = [];
				TreeViewItem first = new() { Header = "first" };
				TreeViewItem second = new() { Header = "second" };
				TreeView tree = new();
				tree.Items.Add(first);
				tree.Items.Add(second);
				TreeViewBehavior.SetSelectedItemCommand(tree, new RelayCommand(parameter => received.Add(parameter)));
				TreeViewBehavior.SetSelectOnRightClick(tree, true);
				Window window = Show(tree);
				try
				{
					first.IsSelected = true;
					Assert.Same(first, received[^1]);

					// Without the behavior a right click selects nothing; with it, the item under the mouse is selected.
					tree.RaiseEvent(MouseArgs(UIElement.PreviewMouseRightButtonDownEvent, MouseButton.Right, second));
					Assert.True(second.IsSelected);
					Assert.Same(second, received[^1]);

					TreeViewBehavior.SetSelectOnRightClick(tree, false);
					tree.RaiseEvent(MouseArgs(UIElement.PreviewMouseRightButtonDownEvent, MouseButton.Right, first));
					Assert.True(second.IsSelected);
				}
				finally
				{
					window.Close();
				}
			});
		}

		private static (ScrollViewer Viewer, Border Host) ScrollingHost()
		{
			ScrollViewer viewer = new()
			{
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Content = new Border { Width = 3000, Height = 3000 },
			};

			return (viewer, new Border { Child = viewer, Width = 190, Height = 190 });
		}

		[Fact]
		public void ScrollSync_TheOtherPartFollows_OnlyWhenItIsOn()
		{
			RunOnStaThread(() =>
			{
				(ScrollViewer leftViewer, Border left) = ScrollingHost();
				(ScrollViewer rightViewer, Border right) = ScrollingHost();
				ScrollSync.SetPartner(left, right);
				ScrollSync.SetPartner(right, left);
				ScrollSync.SetIsEnabled(left, true);
				ScrollSync.SetIsEnabled(right, true);
				StackPanel panel = new() { Orientation = Orientation.Horizontal };
				panel.Children.Add(left);
				panel.Children.Add(right);
				Window window = Show(panel);
				try
				{
					leftViewer.ScrollToVerticalOffset(300);
					leftViewer.ScrollToHorizontalOffset(120);
					Pump(window);
					Assert.Equal(300, rightViewer.VerticalOffset);
					Assert.Equal(120, rightViewer.HorizontalOffset);

					// Both ways, and the follower does not bring the first one back.
					rightViewer.ScrollToVerticalOffset(500);
					Pump(window);
					Assert.Equal(500, leftViewer.VerticalOffset);
					Assert.Equal(500, rightViewer.VerticalOffset);

					// Turned off: each one goes its own way.
					ScrollSync.SetIsEnabled(left, false);
					ScrollSync.SetIsEnabled(right, false);
					leftViewer.ScrollToVerticalOffset(900);
					Pump(window);
					Assert.Equal(900, leftViewer.VerticalOffset);
					Assert.Equal(500, rightViewer.VerticalOffset);
				}
				finally
				{
					window.Close();
				}
			});
		}

		private static GridView FourColumns()
		{
			GridView view = new();
			foreach (String header in new[] { "Name", "Type", "Size", "Packed" })
			{
				view.Columns.Add(new GridViewColumn { Header = header, Width = 100 });
			}

			return view;
		}

		[Fact]
		public void GridViewColumnWidths_PutsTheWidthsOnTheColumns_AndGivesThemBackWhenTheUserChangesOne()
		{
			RunOnStaThread(() =>
			{
				GridView view = FourColumns();
				ListView list = new() { View = view };
				GridViewColumnWidths.SetWidths(list, new List<Double> { 50, 60, 70, 80 });
				Window window = Show(list);
				try
				{
					Assert.Equal([50.0, 60.0, 70.0, 80.0], view.Columns.Select(column => column.Width));

					// Nobody touched a column: nothing is given back, whatever the layout does.
					Assert.Equal([50.0, 60.0, 70.0, 80.0], GridViewColumnWidths.GetWidths(list));

					// The user drags the edge of the second column: the new widths are given back.
					view.Columns[1].Width = 135;
					Pump(window);
					Thumb gripper = FindAll<Thumb>(list).First(thumb => VisualTree.FindAncestor<GridViewColumnHeader>(thumb)?.Column == view.Columns[1]);
					gripper.RaiseEvent(new DragCompletedEventArgs(0, 0, false));
					Assert.Equal([50.0, 135.0, 70.0, 80.0], GridViewColumnWidths.GetWidths(list));

					// A new list of widths (other settings) goes on the columns; a list that does not fit the columns is ignored.
					GridViewColumnWidths.SetWidths(list, new List<Double> { 90, 91, 92, 93 });
					Assert.Equal([90.0, 91.0, 92.0, 93.0], view.Columns.Select(column => column.Width));
					GridViewColumnWidths.SetWidths(list, new List<Double> { 1, 2 });
					Assert.Equal([90.0, 91.0, 92.0, 93.0], view.Columns.Select(column => column.Width));
				}
				finally
				{
					window.Close();
				}
			});
		}

		private static List<T> FindAll<T>(DependencyObject root)
			where T : DependencyObject
		{
			List<T> found = [];
			for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(root, i);
				if (child is T match)
					found.Add(match);

				found.AddRange(FindAll<T>(child));
			}

			return found;
		}

		private sealed class FakeStore : IWindowPlacementStore
		{
			public Boolean SaveWindowSize { get; set; } = true;

			public Boolean SaveWindowPosition { get; set; } = true;

			public WindowPlacement? Saved { get; set; }

			public Int32 Saves { get; private set; }

			public WindowPlacement? LoadPlacement() => Saved;

			public void SavePlacement(WindowPlacement placement)
			{
				Saved = placement;
				Saves++;
			}
		}

		[Fact]
		public void WindowPlacementBehavior_PutsTheWindowWhereItWas_AndKeepsWhereItIsWhenItCloses()
		{
			RunOnStaThread(() =>
			{
				FakeStore store = new() { Saved = new WindowPlacement(40, 50, 700, 500, false) };
				Window window = new() { Width = 300, Height = 300, ShowActivated = false };
				WindowPlacementBehavior.SetStore(window, store);

				Assert.Equal(700, window.Width);
				Assert.Equal(500, window.Height);
				Assert.Equal(40, window.Left);
				Assert.Equal(50, window.Top);

				window.Show();
				window.Width = 640;
				window.Height = 480;
				window.Close();

				Assert.Equal(1, store.Saves);
				WindowPlacement saved = store.Saved!.Value;
				Assert.Equal(640, saved.Width);
				Assert.Equal(480, saved.Height);
				Assert.False(saved.IsMaximized);
			});
		}

		[Fact]
		public void WindowPlacementBehavior_WhatTheUserDoesNotKeep_StaysTheDefault_AndACancelledCloseKeepsNothing()
		{
			RunOnStaThread(() =>
			{
				// Neither size nor position: the window opens as designed.
				FakeStore store = new() { Saved = new WindowPlacement(40, 50, 700, 500, false), SaveWindowSize = false, SaveWindowPosition = false };
				Window window = new() { Width = 300, Height = 300 };
				WindowPlacementBehavior.SetStore(window, store);
				Assert.Equal(300, window.Width);
				Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);

				// A close that something cancels (unsaved changes) keeps nothing.
				Window cancelled = new() { ShowActivated = false, Left = -5000, Top = 0 };
				cancelled.Closing += (_, e) => e.Cancel = true;
				FakeStore other = new();
				WindowPlacementBehavior.SetStore(cancelled, other);
				cancelled.Show();
				cancelled.Close();
				Assert.Equal(0, other.Saves);
			});
		}
	}
}