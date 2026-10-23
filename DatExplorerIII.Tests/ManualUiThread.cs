namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using DatExplorerIII.Services.Interface;

	/// <summary>A user interface thread for tests: actions run at once, timers tick when a test says so, so that view models are tested without a dispatcher.</summary>
	public sealed class ManualUiThread : IUiThread
	{
		public List<ManualTimer> Created { get; } = [];

		/// <summary>What the view models put on the clipboard.</summary>
		public List<String> Clipboard { get; } = [];

		public void SetClipboardText(String text)
		{
			Clipboard.Add(text);
		}

		public void Invoke(Action action)
		{
			action();
		}

		public IUiTimer CreateTimer()
		{
			ManualTimer timer = new();
			Created.Add(timer);
			return timer;
		}
	}

	public sealed class ManualTimer : IUiTimer
	{
		public TimeSpan Interval { get; set; }

		public Boolean IsRunning { get; private set; }

		public event EventHandler? Tick;

		public void Start()
		{
			IsRunning = true;
		}

		public void Stop()
		{
			IsRunning = false;
		}

		/// <summary>Ticks once, if the timer is running.</summary>
		public void Raise()
		{
			if (IsRunning)
				Tick?.Invoke(this, EventArgs.Empty);
		}
	}
}