namespace DatExplorerIII.ViewModels
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Core;
	using Infrastructure;
	using Models.Archives;
	using Services.Interface;

	/// <summary>
	/// A long running operation: the progress shown over the window and the way to run work behind it.
	/// </summary>
	public sealed class BusyState : ObservableObject
	{
		private readonly IDialogService _dialogs;

		private Boolean _isBusy;
		private String _title = String.Empty;
		private String _detail = String.Empty;
		private Int32 _progressValue;
		private Int32 _progressMaximum = 1;
		private Boolean _isIndeterminate;
		private Boolean _canCancel;
		private CancellationTokenSource? _cancellation;

		public BusyState(IDialogService dialogs)
		{
			_dialogs = dialogs;
			CancelCommand = new RelayCommand(() => _cancellation?.Cancel());
		}

		public ICommand CancelCommand { get; }

		public Boolean IsBusy
		{
			get => _isBusy;
			private set => SetProperty(ref _isBusy, value);
		}

		public String Title
		{
			get => _title;
			private set => SetProperty(ref _title, value);
		}

		/// <summary>What is being processed (the file name).</summary>
		public String Detail
		{
			get => _detail;
			private set => SetProperty(ref _detail, value);
		}

		public Int32 ProgressValue
		{
			get => _progressValue;
			private set => SetProperty(ref _progressValue, value);
		}

		public Int32 ProgressMaximum
		{
			get => _progressMaximum;
			private set => SetProperty(ref _progressMaximum, value);
		}

		public Boolean IsIndeterminate
		{
			get => _isIndeterminate;
			private set => SetProperty(ref _isIndeterminate, value);
		}

		public Boolean CanCancel
		{
			get => _canCancel;
			private set => SetProperty(ref _canCancel, value);
		}

		/// <summary>True when the last operation was stopped by the user.</summary>
		public Boolean WasCancelled { get; private set; }

		/// <summary>
		/// Runs a long operation behind the progress display. Returns false when it failed (the error is shown) or was cancelled.
		/// </summary>
		public async Task<Boolean> RunAsync(String title, Boolean canCancel, Func<IProgress<DatProgress>, CancellationToken, Task> work)
		{
			Title = title;
			Detail = String.Empty;
			ProgressValue = 0;
			ProgressMaximum = 1;
			IsIndeterminate = true;
			CanCancel = canCancel;
			WasCancelled = false;
			IsBusy = true;
			CommandRequery.Invalidate();

			using CancellationTokenSource cancellation = new();
			_cancellation = cancellation;

			Progress<DatProgress> progress = new(report =>
			{
				IsIndeterminate = false;
				ProgressMaximum = Math.Max(1, report.Total);
				ProgressValue = report.Current;
				Detail = report.Name;
			});

			try
			{
				await work(progress, cancellation.Token);
				return true;
			}
			catch (OperationCanceledException)
			{
				WasCancelled = true;
				return false;
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
				return false;
			}
			finally
			{
				_cancellation = null;
				IsBusy = false;
				CommandRequery.Invalidate();
			}
		}
	}
}