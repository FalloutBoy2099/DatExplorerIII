namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>
	/// One decompilation of one script. The work goes in stages, one source file each: <c>Reading</c> reads the compiled file (its
	/// tables, the startup code, the code of each procedure), <c>Definitions</c> works out what the names of the namespace are,
	/// <c>Reduction</c>, <c>Tree</c> and <c>Blocks</c> turn the stack code into trees of expressions and statements, and
	/// <c>Source</c>, <c>Expressions</c> and <c>Naming</c> write them as source text.
	/// </summary>
	internal sealed partial class Decompilation
	{
		private readonly BinaryScriptReader _reader;

		private readonly ScriptDecompilerOptions _options;

		private readonly List<String> _messages = [];

		private readonly ProcedureTable _procedures = new();

		private readonly ScriptNamespace _namespace = new();

		private readonly ScriptNamespace _strings = new();

		private readonly List<ScriptOpcode> _globalVariables = [];

		private readonly List<ScriptOpcode> _exportedVariables = [];

		private readonly List<ScriptOpcode> _exportedVariableValues = [];

		private readonly List<ScriptOpcode> _exportedProcedures = [];

		private readonly List<List<Node>> _procedureBodies = [];

		private readonly List<List<Node>> _conditions = [];

		private readonly Dictionary<UInt32, DefinitionObject> _definitions = [];

		private readonly List<String> _globalVariableNames = [];

		private readonly CancellationToken _cancellation;

		public Decompilation(Byte[] data, ScriptDecompilerOptions options, CancellationToken cancellation = default)
		{
			_reader = new BinaryScriptReader(data);
			_options = options;
			_cancellation = cancellation;
		}

		public IReadOnlyList<String> Messages => _messages;

		/// <summary>
		/// Stops the work when the caller gave up or the time ran out. Called in the loops, so that a damaged script that sends the
		/// analysis round and round (or makes it very slow) can be abandoned.
		/// </summary>
		private void ThrowIfCancelled()
		{
			_cancellation.ThrowIfCancellationRequested();
		}
	}
}