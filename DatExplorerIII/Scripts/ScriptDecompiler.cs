namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.ExceptionServices;
	using System.Threading;

	/// <summary>
	/// Turns a compiled script of Fallout (a <c>.int</c> file) back into its source (<c>.ssl</c>). The compiled form keeps the
	/// names of the procedures and of the variables the script shares, and the text of its strings; the local names, the comments
	/// and the macros of the original source are lost, so what comes back is a source that compiles to the same program, not the
	/// source that was written.
	/// </summary>
	public static class ScriptDecompiler
	{
		private const Int32 StackSize = 16 * 1024 * 1024;

		/// <summary>
		/// Decompiles a script.
		/// </summary>
		/// <param name="data">The content of the <c>.int</c> file.</param>
		/// <param name="options">How to read and write it; null for the usual.</param>
		/// <param name="cancellation">Gives the work up (the caller no longer needs the source).</param>
		/// <exception cref="ScriptFormatException">The data is not a script, or one this decompiler cannot follow, or it took longer than the time limit of the options.</exception>
		/// <exception cref="OperationCanceledException">The caller gave the work up.</exception>
		public static DecompiledScript Decompile(Byte[] data, ScriptDecompilerOptions? options = null, CancellationToken cancellation = default)
		{
			ArgumentNullException.ThrowIfNull(data);

			ScriptDecompilerOptions actual = options ?? new ScriptDecompilerOptions();
			using CancellationTokenSource limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
			if (actual.Timeout > TimeSpan.Zero)
			{
				limit.CancelAfter(actual.Timeout);
			}

			CancellationToken token = limit.Token;
			DecompiledScript? script = null;
			Exception? failure = null;

			// The trees of the code are walked by recursion, as deep as the nesting of the script: a thread with a large stack keeps a
			// strange script from taking the program down.
			Thread worker = new(() =>
			{
				try
				{
					script = DecompileOnThisThread(data, actual, token);
				}
				catch (Exception exception)
				{
					failure = exception;
				}
			}, StackSize);
			worker.Start();
			worker.Join();

			if (failure is OperationCanceledException && !cancellation.IsCancellationRequested)
			{
				// Not the caller: the time limit.
				throw new ScriptFormatException("Error: The decompilation took too long and was given up") { TimedOut = true };
			}

			if (failure != null)
			{
				ExceptionDispatchInfo.Capture(failure).Throw();
			}

			return script!;
		}

		private static DecompiledScript DecompileOnThisThread(Byte[] data, ScriptDecompilerOptions options, CancellationToken token)
		{
			Decompilation decompilation = new(data, options, token);
			try
			{
				decompilation.Read();
				decompilation.InitializeDefinitions();
				decompilation.ProcessCode();
				return new DecompiledScript(decompilation.CreateSource(), decompilation.Messages);
			}
			catch (Exception exception) when (exception is ArgumentException or IndexOutOfRangeException or InvalidOperationException or KeyNotFoundException or OverflowException or InvalidCastException)
			{
				// A damaged script leads the analysis to places it does not expect: whichever way it fails, it is a script it cannot read.
				throw new ScriptFormatException("Error: The script is damaged or not supported (" + exception.GetType().Name + ")", exception);
			}
		}

		/// <summary>
		/// Decompiles a script with the usual options and, when it cannot be followed that way, with the options that tolerate
		/// the scripts whose calls do not have the number of arguments they are supposed to.
		/// </summary>
		/// <exception cref="ScriptFormatException">Neither way worked; the exception is the one of the first.</exception>
		public static DecompiledScript DecompileLeniently(Byte[] data, ScriptDecompilerOptions? options = null, CancellationToken cancellation = default)
		{
			try
			{
				return Decompile(data, options, cancellation);
			}
			catch (ScriptFormatException first) when (!first.TimedOut)
			{
				ScriptDecompilerOptions usual = options ?? new ScriptDecompilerOptions();
				ScriptDecompilerOptions lenient = usual.Clone();
				lenient.IgnoreWrongArgumentCount = true;
				lenient.InsertOmittedArgumentsBackward = true;
				try
				{
					return Decompile(data, lenient, cancellation);
				}
				catch (ScriptFormatException)
				{
					// The message of the first failure is the one that tells what is wrong with the script.
				}

				throw;
			}
		}
	}
}