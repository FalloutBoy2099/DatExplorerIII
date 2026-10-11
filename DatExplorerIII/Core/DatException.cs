namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using Models;

	/// <summary>
	/// Raised when a DAT archive is invalid or an operation on it cannot be completed.
	/// The message is in English, for logs; the user interface translates <see cref="Kind"/> and <see cref="Arguments"/>.
	/// </summary>
	public sealed class DatException : Exception
	{
		public DatException(DatErrorKind kind, String message, params Object[] arguments)
			: base(message)
		{
			Kind = kind;
			Arguments = arguments;
		}

		public DatException(DatErrorKind kind, String message, Exception innerException, params Object[] arguments)
			: base(message, innerException)
		{
			Kind = kind;
			Arguments = arguments;
		}

		public DatErrorKind Kind { get; }

		/// <summary>The values the translated message mentions (paths, names).</summary>
		public IReadOnlyList<Object> Arguments { get; }
	}
}