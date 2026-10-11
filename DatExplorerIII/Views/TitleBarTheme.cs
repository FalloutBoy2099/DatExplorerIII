namespace DatExplorerIII.Views
{
	using System;
	using System.Runtime.InteropServices;
	using System.Windows;
	using System.Windows.Interop;
	using Services;

	/// <summary>
	/// Gives the title bar of a window the dark or light look of the application theme.
	/// </summary>
	public static class TitleBarTheme
	{
		// DWMWA_USE_IMMERSIVE_DARK_MODE: the documented value is 20; 19 is the one of the first Windows 10 builds that had it.
		private const Int32 ImmersiveDarkMode = 20;
		private const Int32 ImmersiveDarkModeBeforeBuild18985 = 19;

		/// <summary>
		/// Applies the theme to every window that is created, so that no dialog keeps a light title bar.
		/// </summary>
		public static void ApplyToAllWindows()
		{
			// A safety for the windows that did not call Attach: they get the theme once they have loaded.
			EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
		}

		/// <summary>
		/// Gives the title bar of a window its look as soon as the window of the system exists, which is before it is
		/// shown: applied only when the window has loaded, the title bar would be drawn light first. To call in the
		/// constructor of the window.
		/// </summary>
		public static void Attach(Window window)
		{
			if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
				Apply(window);
			else
				window.SourceInitialized += (_, _) => Apply(window);
		}

		/// <summary>
		/// Gives the title bar of an open window the look of the current theme.
		/// </summary>
		public static void Apply(Window window)
		{
			var handle = new WindowInteropHelper(window).Handle;
			if (handle != IntPtr.Zero)
				Apply(handle);
		}

		private static void OnWindowLoaded(Object sender, RoutedEventArgs e)
		{
			if (sender is Window window)
				Apply(window);
		}

		private static void Apply(IntPtr handle)
		{
			var dark = ThemeManager.IsDark() ? 1 : 0;
			if (SetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(Int32)) != 0)
			{
				// The title bar is only a matter of looks: if the older attribute is not known either, it stays as it is.
				_ = SetWindowAttribute(handle, ImmersiveDarkModeBeforeBuild18985, ref dark, sizeof(Int32));
			}
		}

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
		private static extern Int32 SetWindowAttribute(IntPtr window, Int32 attribute, ref Int32 value, Int32 size);
	}
}