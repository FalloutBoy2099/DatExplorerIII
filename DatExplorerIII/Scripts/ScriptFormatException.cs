namespace DatExplorerIII.Scripts
{
	using System;

	/// <summary>A compiled script cannot be read: it is damaged, or it is built in a way the decompiler does not know.</summary>
	public sealed class ScriptFormatException : Exception
	{
		public ScriptFormatException(String message)
			: base(message)
		{ }

		public ScriptFormatException(String message, Exception innerException)
			: base(message, innerException)
		{ }

		/// <summary>True when the work was given up because it took too long (the script may be fine, only huge or strange).</summary>
		public Boolean TimedOut { get; init; }
	}
}