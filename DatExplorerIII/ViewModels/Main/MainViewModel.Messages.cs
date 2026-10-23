namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Core;
	using Infrastructure;
	using Models.Archives;
	using Models.GameFiles;
	using Models.Interface;
	using Services.Checks;
	using Services.Interface;

	/// <summary>
	/// The check of the message files (.msg): the files of the folder that is shown are checked in the background and get a
	/// warning in the list when something is wrong; a window lists the problems of the selected files, or of the whole archive.
	/// </summary>
	public sealed partial class MainViewModel
	{
		/// <summary>What was found in the files that were checked, by file (a file that was replaced is another file).</summary>
		private readonly ConditionalWeakTable<DatEntry, MsgFileReport> _messageReports = [];

		private CancellationTokenSource? _messageScan;

		/// <summary>The check of the message files of the folder that is shown (done when the list has its warnings).</summary>
		internal Task MessageScan { get; private set; } = Task.CompletedTask;

		/// <summary>Lists the problems of the selected message files in a window.</summary>
		public ICommand ShowMessageProblemsCommand { get; private set; } = null!;

		/// <summary>Checks all the message files of the archive and lists their problems in a window.</summary>
		public ICommand ScanMessagesCommand { get; private set; } = null!;

		private void CreateMessageCommands()
		{
			ShowMessageProblemsCommand = new AsyncRelayCommand(ShowMessageProblemsAsync, () => CanBrowse() && SelectedMessageFiles().Count > 0);
			ScanMessagesCommand = new AsyncRelayCommand(ScanMessagesAsync, () => CanBrowse() && CurrentDocument != null);
		}

		private List<ListEntry> SelectedMessageFiles()
		{
			return SelectedItems.Where(item => item.IsMessageFile).ToList();
		}

		/// <summary>Stops the check that is running (the list changed, or the archive closes).</summary>
		private void CancelMessageScan()
		{
			_messageScan?.Cancel();
			_messageScan?.Dispose();
			_messageScan = null;
		}

		/// <summary>How often the files that were checked in the background get their result in the list, in milliseconds.</summary>
		private const Int32 MessageScanFlushMilliseconds = 100;

		/// <summary>
		/// Checks the message files of a list in the background. The files that were checked already get their result at once;
		/// the others get theirs as the check goes, a few at a time (one call to the interface for each file would slow both down).
		/// </summary>
		private void StartMessageScan(DatArchive archive, IReadOnlyList<ListEntry> items)
		{
			CancelMessageScan();
			MessageScan = Task.CompletedTask;

			List<ListEntry> pending = [];
			foreach (ListEntry item in items.Where(item => item.IsMessageFile))
			{
				if (_messageReports.TryGetValue(item.Entry!, out MsgFileReport? known))
				{
					item.SetMessages(known.Without(DisabledMessageChecks));
				}
				else
				{
					pending.Add(item);
				}
			}

			if (pending.Count == 0)
				return;

			CancellationTokenSource source = new();
			_messageScan = source;
			CancellationToken token = source.Token;
			MessageScan = Task.Run(() =>
			{
				List<(ListEntry Item, MsgFileReport Report)> ready = [];
				Stopwatch sinceShown = Stopwatch.StartNew();

				void ShowReady()
				{
					if (ready.Count == 0)
						return;

					List<(ListEntry Item, MsgFileReport Report)> batch = ready.ToList();
					ready.Clear();
					_uiThread.Invoke(() =>
					{
						if (!token.IsCancellationRequested)
						{
							foreach ((ListEntry item, MsgFileReport report) in batch)
							{
								item.SetMessages(report.Without(DisabledMessageChecks));
							}
						}
					});
					sinceShown.Restart();
				}

				try
				{
					foreach (ListEntry item in pending)
					{
						if (token.IsCancellationRequested)
							return;

						MsgFileReport report = MsgScanner.ScanEntry(archive, item.Entry!);
						_messageReports.AddOrUpdate(item.Entry!, report);
						ready.Add((item, report));
						if (sinceShown.ElapsedMilliseconds >= MessageScanFlushMilliseconds)
							ShowReady();
					}

					ShowReady();
				}
				catch (Exception exception) when (exception is ObjectDisposedException or IOException or DatException)
				{
					// The archive was closed while the check was running: nothing to show any more.
				}
			});
		}

		internal async Task ShowMessageProblemsAsync()
		{
			List<ListEntry> files = SelectedMessageFiles();
			DatArchive? archive = CurrentDocument?.Archive;
			if (archive == null || files.Count == 0)
				return;

			if (AllMessageChecksOff)
			{
				ShowAllChecksOff();
				return;
			}

			List<MsgFileReport> reports = await Task.Run(() => files.Select(file => CheckedReport(archive, file.Entry!)).ToList());
			ShowMessageReports(reports);
		}

		internal async Task ScanMessagesAsync()
		{
			DatArchive? archive = CurrentDocument?.Archive;
			if (archive == null)
				return;

			if (AllMessageChecksOff)
			{
				ShowAllChecksOff();
				return;
			}

			IReadOnlyList<MsgFileReport>? reports = null;
			Boolean done = await RunBusyAsync(Loc.Get("Str.MsgScanning"), true, async (progress, token) =>
			{
				reports = await Task.Run(() => MsgScanner.ScanArchive(archive, String.Empty, progress, token), token);
			});

			if (!done || reports == null)
				return;

			// What the check found is kept: the files of the list that is shown get their warning too.
			foreach (MsgFileReport report in reports)
			{
				if (archive.Find(report.Path) is { } entry)
					_messageReports.AddOrUpdate(entry, report);
			}

			foreach (ListEntry item in Items.Where(item => item.IsMessageFile))
			{
				if (_messageReports.TryGetValue(item.Entry!, out MsgFileReport? known))
					item.SetMessages(known.Without(DisabledMessageChecks));
			}

			ShowMessageReports(reports);
		}

		/// <summary>The result for a file: the one that is kept, or a new check.</summary>
		private MsgFileReport CheckedReport(DatArchive archive, DatEntry entry)
		{
			if (_messageReports.TryGetValue(entry, out MsgFileReport? known))
				return known;

			MsgFileReport report = MsgScanner.ScanEntry(archive, entry);
			_messageReports.AddOrUpdate(entry, report);
			return report;
		}

		/// <summary>The warnings of the list again, after the kinds of problems that are looked for changed (the results are kept: nothing is checked again).</summary>
		private void ApplyMessageChecks()
		{
			Preview.DisabledMessageChecks = DisabledMessageChecks;
			Comparison.ApplyMessageChecks();
			foreach (ListEntry item in Items.Where(item => item.IsMessageFile))
			{
				if (_messageReports.TryGetValue(item.Entry!, out MsgFileReport? known))
					item.SetMessages(known.Without(DisabledMessageChecks));
			}
		}

		/// <summary>
		/// A double click on a message file: when the user wants it and the file has problems (of the kinds that are looked for), the
		/// window of the problems opens instead of the file.
		/// </summary>
		/// <returns>True when the window was opened (the file is not opened then).</returns>
		private async Task<Boolean> OpenMessageProblemsInsteadAsync(ListEntry item)
		{
			DatArchive? archive = CurrentDocument?.Archive;
			if (!_settings.Data.DoubleClickMessageProblems || !item.IsMessageFile || archive == null || AllMessageChecksOff)
				return false;

			MsgFileReport report = (await Task.Run(() => CheckedReport(archive, item.Entry!))).Without(DisabledMessageChecks);
			if (!report.HasIssues)
				return false;

			ShowMessageReports([report]);
			return true;
		}

		private void ShowAllChecksOff()
		{
			_dialogs.ShowMessage(Title, Loc.Get("Str.MsgAllChecksOff"), DialogIcon.Information);
		}

		/// <summary>Lists the problems of the message file that the viewer shows.</summary>
		private void ShowMessageReport(MsgFileReport report)
		{
			if (AllMessageChecksOff)
			{
				ShowAllChecksOff();
				return;
			}

			ShowMessageReports([report]);
		}

		private void ShowMessageReports(IReadOnlyList<MsgFileReport> allReports)
		{
			IReadOnlyList<MsgFileReport> reports = allReports.Select(report => report.Without(DisabledMessageChecks)).ToList();
			if (reports.Count == 0)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.MsgNothingToCheck"), DialogIcon.Information);
				return;
			}

			if (reports.All(report => !report.HasIssues))
			{
				_dialogs.ShowMessage(Title, Loc.Format("Str.MsgNoProblems", Loc.Plural("Str.PluralMessageFiles", reports.Count)), DialogIcon.Information);
				return;
			}

			_dialogs.ShowMessageIssues(new MsgIssuesViewModel(reports, MsgReportText.FromResources(), _uiThread));
		}
	}
}