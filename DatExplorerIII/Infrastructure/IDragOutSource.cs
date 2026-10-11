namespace DatExplorerIII.Infrastructure
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// What a view model gives to a list or a tree whose items can be dragged out of the program, to the file explorer.
	/// </summary>
	public interface IDragOutSource
	{
		/// <summary>
		/// Makes real files of what is dragged (the file explorer needs them) and returns their paths, or null when there is
		/// nothing to drag.
		/// </summary>
		/// <param name="item">The data of the item under the mouse (a tree node, a list entry).</param>
		IReadOnlyList<String>? PrepareDragOut(Object? item);
	}

	/// <summary><see cref="IDragOutSource"/> that calls a delegate.</summary>
	public sealed class DelegateDragOutSource : IDragOutSource
	{
		private readonly Func<Object?, IReadOnlyList<String>?> _prepare;

		public DelegateDragOutSource(Func<Object?, IReadOnlyList<String>?> prepare)
		{
			_prepare = prepare;
		}

		public IReadOnlyList<String>? PrepareDragOut(Object? item)
		{
			return _prepare(item);
		}
	}
}