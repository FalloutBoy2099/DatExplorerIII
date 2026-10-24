namespace DatExplorerIII.Services.Checks
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats.GameFiles;
	using Models.Archives;
	using Models.GameFiles;
	using Services.Interface;
	using Services.Platform;
	using Services.Texts;

	/// <summary>
	/// Checks the message files (.msg) of an archive, of a folder, or one file of the disk, with <see cref="MsgValidator"/>.
	/// </summary>
	public static class MsgScanner
	{
		/// <summary>True for the name of a message file.</summary>
		public static Boolean IsMsgName(String name)
		{
			return name.EndsWith(".msg", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Checks the content of a file (decoded as any text, the encoding being detected).</summary>
		public static MsgFileReport ScanData(String path, Byte[] data)
		{
			DecodedText decoded = TextDecoder.Decode(data);
			return new MsgFileReport(path, decoded.LooksBinary ? Array.Empty<MsgIssue>() : MsgValidator.Validate(decoded.Text));
		}

		/// <summary>Checks a file of an archive. A file that cannot be read is reported with its error, not thrown.</summary>
		public static MsgFileReport ScanEntry(DatArchive archive, DatEntry entry)
		{
			try
			{
				return ScanData(entry.FullPath, archive.ReadData(entry));
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException or InvalidDataException)
			{
				return new MsgFileReport(entry.FullPath, Array.Empty<MsgIssue>(), ErrorText.Describe(exception));
			}
		}

		/// <summary>Checks every message file of an archive (a few at the same time); the reports are in the order of the paths.</summary>
		public static IReadOnlyList<MsgFileReport> ScanArchive(DatArchive archive, String label, IProgress<DatProgress>? progress = null, CancellationToken cancellation = default)
		{
			List<DatEntry> entries = archive.Entries.Where(entry => IsMsgName(entry.Name) && !entry.IsDamaged).OrderBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase).ToList();
			MsgFileReport[] reports = new MsgFileReport[entries.Count];
			Int32 done = 0;
			ParallelOptions options = new() { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount), CancellationToken = cancellation };
			try
			{
				Parallel.For(0, entries.Count, options, index =>
				{
					MsgFileReport report = ScanEntry(archive, entries[index]);
					reports[index] = label.Length == 0 ? report : report with { Path = label + ":" + Path.DirectorySeparatorChar + report.Path };
					progress?.Report(new DatProgress(entries[index].FullPath, Interlocked.Increment(ref done), entries.Count));
				});
			}
			catch (AggregateException exception) when (exception.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			}

			return reports;
		}

		/// <summary>
		/// Checks what a path names: a message file, a folder (its message files, in all its sub folders), or a DAT archive (its
		/// message files). A path that is not there, or an archive that cannot be opened, gives a report with an error.
		/// </summary>
		public static IReadOnlyList<MsgFileReport> ScanPath(String path, IProgress<DatProgress>? progress = null, CancellationToken cancellation = default)
		{
			if (Directory.Exists(path))
			{
				List<String> files = Directory.EnumerateFiles(path, "*.msg", SearchOption.AllDirectories).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
				List<MsgFileReport> reports = new(files.Count);
				for (Int32 index = 0; index < files.Count; index++)
				{
					cancellation.ThrowIfCancellationRequested();
					progress?.Report(new DatProgress(files[index], index + 1, files.Count));
					reports.Add(ScanFile(files[index]));
				}

				return reports;
			}

			if (!File.Exists(path))
				return [new MsgFileReport(path, Array.Empty<MsgIssue>(), "Not found.")];

			if (DatFiles.IsDatFile(path))
			{
				try
				{
					using DatArchive archive = DatArchive.Open(path);
					return ScanArchive(archive, path, progress, cancellation);
				}
				catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
				{
					return [new MsgFileReport(path, Array.Empty<MsgIssue>(), ErrorText.Describe(exception))];
				}
			}

			return [ScanFile(path)];
		}

		private static MsgFileReport ScanFile(String path)
		{
			try
			{
				return ScanData(path, File.ReadAllBytes(path));
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				return new MsgFileReport(path, Array.Empty<MsgIssue>(), exception.Message);
			}
		}
	}
}