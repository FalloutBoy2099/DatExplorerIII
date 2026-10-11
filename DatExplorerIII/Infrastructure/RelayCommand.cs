namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Windows.Input;

	/// <summary>
	/// A command that runs a delegate. Availability is re-evaluated by WPF on user input.
	/// </summary>
	public sealed class RelayCommand : ICommand
	{
		private readonly Action<Object?> _execute;
		private readonly Func<Object?, Boolean>? _canExecute;

		public RelayCommand(Action<Object?> execute, Func<Object?, Boolean>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public RelayCommand(Action execute, Func<Boolean>? canExecute = null)
			: this(_ => execute(), canExecute == null ? null : _ => canExecute())
		{
		}

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}

		public Boolean CanExecute(Object? parameter)
		{
			return _canExecute == null || _canExecute(parameter);
		}

		public void Execute(Object? parameter)
		{
			_execute(parameter);
		}
	}
}