namespace DatExplorerIII.Services
{
	using System;
	using System.IO;

	/// <summary>
	/// Scratch space for files opened from a DAT or dragged out of it. Removed when the application exits.
	/// A running session holds a lock file, so that another instance never mistakes it for a leftover.
	/// </summary>
	public sealed class TempFolder : IDisposable
	{
		private const String LockFileName = ".lock";
		private static readonly String BaseFolder = Path.Combine(Path.GetTempPath(), "DatExplorerIII");

		private readonly String _session = Path.Combine(BaseFolder, Guid.NewGuid().ToString("N"));
		private FileStream? _lock;

		/// <summary>Creates a new empty folder.</summary>
		public String Create(String purpose)
		{
			EnsureSession();

			String path = Path.Combine(_session, purpose, Path.GetRandomFileName());
			Directory.CreateDirectory(path);
			return path;
		}

		/// <summary>
		/// Removes the folders left behind by sessions that are over. A session is over when its lock file is free
		/// (or, for a folder without lock file, when nothing was written in it for a day).
		/// </summary>
		public static void CleanStaleSessions()
		{
			try
			{
				if (!Directory.Exists(BaseFolder))
				{
					return;
				}

				foreach (String directory in Directory.GetDirectories(BaseFolder))
				{
					if (IsStale(directory))
					{
						Directory.Delete(directory, true);
					}
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// Leftovers are removed by a later session.
			}
		}

		/// <summary>Removes the scratch space (the same as <see cref="Cleanup"/>).</summary>
		public void Dispose()
		{
			Cleanup();
		}

		public void Cleanup()
		{
			try
			{
				_lock?.Dispose();
				_lock = null;

				if (Directory.Exists(_session))
				{
					Directory.Delete(_session, true);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// A file still opened in another program: removed by a later session.
			}
		}

		private static Boolean IsStale(String directory)
		{
			String lockFile = Path.Combine(directory, LockFileName);
			if (!File.Exists(lockFile))
			{
				return DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) > TimeSpan.FromDays(1);
			}

			try
			{
				// Only a session that is over leaves its lock file free.
				using FileStream probe = new(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				return true;
			}
			catch (IOException)
			{
				return false;
			}
			catch (UnauthorizedAccessException)
			{
				return false;
			}
		}

		private void EnsureSession()
		{
			if (_lock != null)
			{
				return;
			}

			Directory.CreateDirectory(_session);
			_lock = new FileStream(Path.Combine(_session, LockFileName), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
		}
	}
}