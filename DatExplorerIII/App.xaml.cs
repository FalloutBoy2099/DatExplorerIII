namespace DatExplorerIII
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Windows;
	using System.Windows.Threading;
	using Services;
	using ViewModels;
	using Views;

	/// <summary>
	/// DatExplorerIII - Fallout 1/2 and Arcanum DAT explorer, by Kain.
	/// </summary>
	public partial class App : Application, IDisposable
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// "--scan-msg": the check of message files from a command line, without any window.
			if (MsgCommandLine.IsCommand(e.Args))
			{
				Shutdown(RunMessageCheck(e.Args));
				return;
			}

			SettingsService settings = new();
			if (settings.Data.SingleInstance && SingleInstance.TrySend(SingleInstance.DefaultPipeName, FullPaths(e.Args)))
			{
				Shutdown();
				return;
			}

			ThemeManager.Initialize(settings);
			ScrollBarMetrics scrollBars = (ScrollBarMetrics)Resources["ScrollBarMetrics"];
			scrollBars.Update(settings.Data.ScrollBarWidth, settings.Data.ScrollBarMinimumThumb);
			TitleBarTheme.ApplyToAllWindows();
			Loc.Initialize(settings.Data.Language ?? Loc.DetectSystemLanguage());
			DispatcherUnhandledException += OnUnhandledException;
			TempFolder.CleanStaleSessions();

			MainViewModel viewModel = new(new DialogService(), settings, new TempFolder(), new DispatcherUiThread(), scrollBars);
			_mainViewModel = viewModel;
			Views.MainWindow window = new(viewModel);
			MainWindow = window;

			_singleInstance = new SingleInstance(SingleInstance.DefaultPipeName, paths => Dispatcher.InvokeAsync(() => OnOtherLaunch(window, viewModel, paths)));
			viewModel.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(MainViewModel.IsSingleInstance))
				{
					ApplySingleInstance(viewModel.IsSingleInstance);
				}
			};

			window.Loaded += async (_, _) =>
			{
				ApplySingleInstance(viewModel.IsSingleInstance);
				await viewModel.StartAsync(e.Args);
			};
			window.Show();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			Dispose();
			base.OnExit(e);
		}

		/// <summary>Frees what the program holds until it exits: the listening for the other launches, and the view model.</summary>
		public void Dispose()
		{
			_singleInstance?.Dispose();
			_mainViewModel?.Dispose();
		}

		private SingleInstance? _singleInstance;
		private MainViewModel? _mainViewModel;

		private void ApplySingleInstance(Boolean enabled)
		{
			if (enabled)
			{
				_singleInstance?.Start();
			}
			else
			{
				_singleInstance?.Stop();
			}
		}

		/// <summary>The files are given to the running program with their full path: the folder of the new launch is not the same.</summary>
		/// <summary>Checks the message files that the command line names; the report goes to the console it was started from and to a log.</summary>
		private Int32 RunMessageCheck(String[] args)
		{
			// The report is in English, whatever the language of the interface.
			Loc.Initialize("en");
			using TextWriter? console = ConsoleHost.OpenOutput();
			return MsgCommandLine.Run(args, console, MsgReportText.FromResources(), Environment.CurrentDirectory);
		}

		private static List<String> FullPaths(IEnumerable<String> arguments)
		{
			List<String> paths = new();
			foreach (String argument in arguments)
			{
				try
				{
					paths.Add(Path.GetFullPath(argument));
				}
				catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
				{
					paths.Add(argument);
				}
			}

			return paths;
		}

		/// <summary>Another launch handed its files over: the window comes to the front and opens them.</summary>
		private static async void OnOtherLaunch(Views.MainWindow window, MainViewModel viewModel, IReadOnlyList<String> paths)
		{
			if (window.WindowState == WindowState.Minimized)
			{
				SystemCommands.RestoreWindow(window);
			}

			window.Activate();
			window.Topmost = true;
			window.Topmost = false;

			if (paths.Count > 0)
			{
				await viewModel.OpenFromAnotherInstanceAsync(paths);
			}
		}

		private static void OnUnhandledException(Object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			e.Handled = true;
			ErrorLog.Write(e.Exception);
			new DialogService().ShowError(Loc.Get("Str.UnexpectedError") + Environment.NewLine + ErrorText.Describe(e.Exception));
		}

	}
}