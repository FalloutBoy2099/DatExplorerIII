namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Threading.Tasks;
	using System.Windows.Input;

	/// <summary>
	/// A command that runs an asynchronous delegate and cannot be started again until it completes.
	/// </summary>
	public sealed class AsyncRelayCommand : ICommand
	{
		private readonly Func<Object?, Task> _execute;
		private readonly Func<Object?, Boolean>? _canExecute;
		private Boolean _isRunning;

		public AsyncRelayCommand(Func<Task> execute, Func<Boolean>? canExecute = null)
			: this(_ => execute(), canExecute == null ? null : _ => canExecute())
		{ }

		public AsyncRelayCommand(Func<Object?, Task> execute, Func<Object?, Boolean>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}

		public Boolean CanExecute(Object? parameter)
		{
			return !_isRunning && (_canExecute == null || _canExecute(parameter));
		}

		public async void Execute(Object? parameter)
		{
			_isRunning = true;
			CommandManager.InvalidateRequerySuggested();
			try
			{
				await _execute(parameter);
			}
			finally
			{
				_isRunning = false;
				CommandManager.InvalidateRequerySuggested();
			}
		}
	}
}