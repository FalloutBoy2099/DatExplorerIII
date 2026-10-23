namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.IO;

	/// <summary>
	/// Keeps the report of the errors that nobody expected (with where they happened) in a file of the application data folder,
	/// for whoever looks into them. Writing the log never fails: a log that cannot be written is lost, not a second error.
	/// </summary>
	public static class ErrorLog
	{
		/// <summary>A log larger than this is started again: it must not grow for ever.</summary>
		private const Int64 MaxSize = 1024 * 1024;

		private static readonly Object Gate = new();

		public static String FilePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DatExplorerIII", "errors.log");

		public static void Write(Exception exception)
		{
			try
			{
				lock (Gate)
				{
					Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
					if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxSize)
						File.Delete(FilePath);

					File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
				}
			}
			catch (Exception logError) when (logError is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				// See the summary.
			}
		}
	}
}