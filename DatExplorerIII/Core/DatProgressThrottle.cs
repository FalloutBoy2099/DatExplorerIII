namespace DatExplorerIII.Core
{
	using System;
	using System.Diagnostics;
	using Models;

	/// <summary>
	/// Forwards progress notifications at a rate the user interface can follow.
	/// </summary>
	internal sealed class DatProgressThrottle
	{
		private static readonly Int64 IntervalTicks = Stopwatch.Frequency / 20;

		private readonly IProgress<DatProgress>? _progress;
		private readonly Int32 _total;
		private Int64 _lastReport;

		public DatProgressThrottle(IProgress<DatProgress>? progress, Int32 total)
		{
			_progress = progress;
			_total = total;
			_lastReport = Stopwatch.GetTimestamp() - IntervalTicks;
		}

		public void Report(String name, Int32 current)
		{
			if (_progress == null)
			{
				return;
			}

			Int64 now = Stopwatch.GetTimestamp();
			if (current >= _total || now - _lastReport >= IntervalTicks)
			{
				_lastReport = now;
				_progress.Report(new DatProgress(name, current, _total));
			}
		}
	}
}