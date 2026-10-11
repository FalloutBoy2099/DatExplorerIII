namespace DatExplorerIII.Services
{
	using System;
	using System.Windows;
	using System.Windows.Threading;

	/// <summary>
	/// <see cref="IUiThread"/> backed by WPF: every timer ticks on the dispatcher of the thread that made it, and an action
	/// runs on the dispatcher of the application.
	/// </summary>
	public sealed class DispatcherUiThread : IUiThread
	{
		public IUiTimer CreateTimer()
		{
			return new Timer();
		}

		public void Invoke(Action action)
		{
			Dispatcher? dispatcher = Application.Current?.Dispatcher;
			if (dispatcher == null || dispatcher.CheckAccess())
			{
				action();
			}
			else
			{
				dispatcher.Invoke(action);
			}
		}

		private sealed class Timer : IUiTimer
		{
			private readonly DispatcherTimer _timer = new();

			public TimeSpan Interval
			{
				get => _timer.Interval;
				set => _timer.Interval = value;
			}

			public event EventHandler? Tick
			{
				add => _timer.Tick += value;
				remove => _timer.Tick -= value;
			}

			public void Start()
			{
				_timer.Start();
			}

			public void Stop()
			{
				_timer.Stop();
			}
		}
	}
}