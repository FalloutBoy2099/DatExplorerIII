namespace DatExplorerIII.Views
{
	using System;
	using System.Linq;
	using System.Windows;
	using ViewModels;

	/// <summary>
	/// The settings of the program, in tabs: the general ones, and what the comparison of two archives ignores. The values are
	/// those of a <see cref="SettingsDialogViewModel"/>, which the program takes when the user presses OK.
	/// </summary>
	public partial class SettingsWindow : Window
	{
		public SettingsWindow(SettingsDialogViewModel settings)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);
			DataContext = settings;

			// A tab control is as tall as the tab that is shown: all the tabs get the height of the tallest, so that the window does not
			// change its size (and push its buttons out of the screen) when the tab changes.
			// The pages that hold a list (the extensions, the associations) take what the others leave: they do not count in the height.
			FrameworkElement[] pages = [(FrameworkElement)GeneralTab.Content, (FrameworkElement)CompareTab.Content, (FrameworkElement)MessagesTab.Content];
			FrameworkElement[] listPages = [(FrameworkElement)ExtensionsTab.Content, (FrameworkElement)AssociationsTab.Content, (FrameworkElement)LimitsTab.Content];
			foreach (FrameworkElement page in pages)
			{
				page.Measure(new Size(Width - 80, Double.PositiveInfinity));
			}

			Double height = pages.Max(page => page.DesiredSize.Height);
			foreach (FrameworkElement page in pages)
			{
				page.MinHeight = height;
			}

			foreach (FrameworkElement page in listPages)
			{
				page.Height = height;
			}

			// Centered on a main window that is taller than the room left by the task bar, the buttons would be out of reach.
			Loaded += (_, _) =>
			{
				Rect area = SystemParameters.WorkArea;
				if (Top + ActualHeight > area.Bottom)
					Top = Math.Max(area.Top, area.Bottom - ActualHeight);
			};
		}

		private void OnSelectAllExtensions(Object sender, RoutedEventArgs e)
		{
			((SettingsDialogViewModel)DataContext).SetAllExtensions(true);
		}

		private void OnSelectNoExtensions(Object sender, RoutedEventArgs e)
		{
			((SettingsDialogViewModel)DataContext).SetAllExtensions(false);
		}

		private void OnAddAssociation(Object sender, RoutedEventArgs e)
		{
			((SettingsDialogViewModel)DataContext).AddAssociation();
			NewExtensionBox.Focus();
		}

		private void OnNewExtensionKeyDown(Object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter && ((SettingsDialogViewModel)DataContext).CanAddAssociation)
			{
				((SettingsDialogViewModel)DataContext).AddAssociation();
				e.Handled = true;
			}
		}

		private void OnRemoveAssociation(Object sender, RoutedEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is AssociationOption option)
				((SettingsDialogViewModel)DataContext).RemoveAssociation(option);
		}

		private void OnAddLimit(Object sender, RoutedEventArgs e)
		{
			((SettingsDialogViewModel)DataContext).AddLimit();
			NewLimitExtensionBox.Focus();
		}

		private void OnNewLimitKeyDown(Object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter && ((SettingsDialogViewModel)DataContext).CanAddLimit)
			{
				((SettingsDialogViewModel)DataContext).AddLimit();
				e.Handled = true;
			}
		}

		private void OnRemoveLimit(Object sender, RoutedEventArgs e)
		{
			if (((FrameworkElement)sender).DataContext is LimitOption option)
				((SettingsDialogViewModel)DataContext).RemoveLimit(option);
		}

		private void OnOk(Object sender, RoutedEventArgs e)
		{
			DialogResult = true;
		}
	}
}