namespace DatExplorerIII.Services.Interface
{
	using System;

	/// <summary>
	/// A timer, made by <see cref="IUiThread"/>, whose <see cref="Tick"/> runs on the thread of the user interface, so that the view models do not depend on a
	/// dispatcher.
	/// </summary>
	public interface IUiTimer
	{
		/// <summary>Time between two ticks.</summary>
		TimeSpan Interval { get; set; }

		event EventHandler? Tick;

		void Start();

		void Stop();
	}
}