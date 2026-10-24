namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Pipes;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Lets a second launch of the program hand its files to the program that is already running. The running program listens on a
	/// named pipe (only for the current user); a new launch that finds it sends the files it was given and ends.
	/// </summary>
	/// <remarks>
	/// The messages are: the number of files (32 bit), then each path as a UTF-8 text with its length (32 bit). The server first
	/// sends its process identifier (32 bit), so that the sender can allow it to come to the front, and answers with a byte
	/// once it has read the files. While it listens, the server also holds a named event: a new launch that does not find it
	/// knows at once that nobody listens (waiting for a pipe that does not exist would take the whole time out).
	/// </remarks>
	public sealed class SingleInstance : IDisposable
	{
		private const Int32 MaxFiles = 1000;
		private const Int32 MaxPathBytes = 32 * 1024;
		private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

		private readonly String _pipeName;
		private readonly Action<IReadOnlyList<String>> _received;
		private CancellationTokenSource? _stop;
		private Task? _loop;
		private EventWaitHandle? _marker;

		public SingleInstance(String pipeName, Action<IReadOnlyList<String>> received)
		{
			_pipeName = pipeName;
			_received = received;
		}

		/// <summary>The name of the pipe of the program: one per Windows user.</summary>
		public static String DefaultPipeName => "DatExplorerIII.SingleInstance." + Environment.UserName;

		/// <summary>Whether this instance is listening.</summary>
		public Boolean IsRunning => _loop != null;

		/// <summary>
		/// Starts listening. False when another program already listens on the pipe (the first one to start keeps it).
		/// </summary>
		public Boolean Start()
		{
			if (_loop != null)
				return true;

			NamedPipeServerStream first;
			try
			{
				first = CreateServer();
			}
			catch (IOException)
			{
				return false;
			}

			_marker = new EventWaitHandle(false, EventResetMode.ManualReset, MarkerName(_pipeName));
			_stop = new CancellationTokenSource();
			_loop = Task.Run(() => ListenAsync(first, _stop.Token));
			return true;
		}

		/// <summary>Stops listening: a new launch will then start its own instance.</summary>
		public void Stop()
		{
			CancellationTokenSource? stop = _stop;
			Task? loop = _loop;
			_stop = null;
			_loop = null;
			_marker?.Dispose();
			_marker = null;

			if (stop != null)
			{
				stop.Cancel();
				try
				{
					loop?.Wait(TimeSpan.FromSeconds(2));
				}
				catch (AggregateException)
				{
					// The loop ended by being cancelled.
				}

				stop.Dispose();
			}
		}

		public void Dispose()
		{
			Stop();
		}

		/// <summary>
		/// Sends the files to the instance that is running, if there is one.
		/// </summary>
		/// <returns>True when another instance took them (this one then has nothing to do).</returns>
		public static Boolean TrySend(String pipeName, IReadOnlyList<String> paths, Int32 timeoutMilliseconds = 1000)
		{
			try
			{
				// Nobody listens: no need to wait for the pipe.
				if (!EventWaitHandle.TryOpenExisting(MarkerName(pipeName), out EventWaitHandle? marker))
					return false;

				marker.Dispose();

				using NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
				client.Connect(timeoutMilliseconds);

				using BinaryReader reader = new(client, Encoding.UTF8, true);
				Int32 processId = reader.ReadInt32();
				AllowSetForegroundWindow(processId);

				using BinaryWriter writer = new(client, Encoding.UTF8, true);
				writer.Write(paths.Count);
				foreach (String path in paths)
				{
					Byte[] bytes = Encoding.UTF8.GetBytes(path);
					writer.Write(bytes.Length);
					writer.Write(bytes);
				}

				writer.Flush();
				return reader.ReadByte() == 1;
			}
			catch (Exception exception) when (exception is TimeoutException or IOException or UnauthorizedAccessException or EndOfStreamException)
			{
				// Nobody listens (or it does not answer): this instance runs on its own.
				return false;
			}
		}

		/// <summary>The name of the event that tells that an instance listens on a pipe.</summary>
		private static String MarkerName(String pipeName) => pipeName + ".Listening";

		private NamedPipeServerStream CreateServer()
		{
			return new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
		}

		private async Task ListenAsync(NamedPipeServerStream server, CancellationToken token)
		{
			NamedPipeServerStream? current = server;
			while (!token.IsCancellationRequested)
			{
				try
				{
					current ??= CreateServer();
					await current.WaitForConnectionAsync(token);
					await ServeAsync(current, token);
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
					break;
				}
				catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException or OperationCanceledException)
				{
					// A broken, invalid or too slow message: the next sender is served normally.
				}
				finally
				{
					current?.Dispose();
					current = null;
				}
			}
		}

		private async Task ServeAsync(NamedPipeServerStream pipe, CancellationToken token)
		{
			using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
			timeout.CancelAfter(ReadTimeout);

			await pipe.WriteAsync(BitConverter.GetBytes(Environment.ProcessId), timeout.Token);
			await pipe.FlushAsync(timeout.Token);

			Int32 count = await ReadInt32Async(pipe, timeout.Token);
			if (count < 0 || count > MaxFiles)
				throw new InvalidDataException("The number of files is not valid.");

			List<String> paths = new(count);
			for (Int32 i = 0; i < count; i++)
			{
				Int32 length = await ReadInt32Async(pipe, timeout.Token);
				if (length < 0 || length > MaxPathBytes)
					throw new InvalidDataException("A path is too long.");

				Byte[] bytes = new Byte[length];
				await pipe.ReadExactlyAsync(bytes, timeout.Token);
				paths.Add(Encoding.UTF8.GetString(bytes));
			}

			_received(paths);
			await pipe.WriteAsync(new Byte[] { 1 }, timeout.Token);
			await pipe.FlushAsync(timeout.Token);
		}

		private static async Task<Int32> ReadInt32Async(Stream stream, CancellationToken token)
		{
			Byte[] bytes = new Byte[4];
			await stream.ReadExactlyAsync(bytes, token);
			return BitConverter.ToInt32(bytes);
		}

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern Boolean AllowSetForegroundWindow(Int32 processId);
	}
}