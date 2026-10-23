namespace DatExplorerIII.Services.Checks
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Models.GameFiles;
	using Services.Interface;

	/// <summary>How the problems of the message files are said (the program says them in the language of the interface, the log in English).</summary>
	public sealed record MsgReportText(Func<MsgIssue, String> Describe, Func<MsgIssueSeverity, String> SeverityName)
	{
		/// <summary>The texts of the program, in the language that is loaded.</summary>
		public static MsgReportText FromResources()
		{
			return new MsgReportText(
				issue => Loc.Format("Str.MsgIssue" + issue.Kind, issue.Kind == MsgIssueKind.WrongFieldCount ? Loc.Plural("Str.PluralBracePairs", issue.Value) : issue.Value, issue.Other, issue.Excerpt),
				severity => Loc.Get("Str.MsgSeverity" + severity));
		}
	}

	/// <summary>
	/// Checks message files from the command line:
	/// <c>DatExplorerIII.exe --scan-msg [options] [path...]</c>, where a path is a message file, a folder (all its .msg files, in
	/// its sub folders too) or a DAT archive. Nothing opens: the problems are written to the console and to a log.
	/// </summary>
	public static class MsgCommandLine
	{
		public const String Switch = "--scan-msg";

		public const String UsageText = "Usage: DatExplorerIII.exe --scan-msg [--log <file>] [--min-severity critical|high|medium] [--no-exitcode] [path...]\r\n"
			+ "  path           a .msg file, a folder (its .msg files, sub folders included) or a DAT archive; the current folder when none\r\n"
			+ "  --log <file>   also writes the report to this file (when the program is not started from a console, to DatExplorerIII-msg.log)\r\n"
			+ "  --min-severity only the problems of this severity or worse: critical, high or medium (the default)\r\n"
			+ "  --no-exitcode  always ends with the code 0 (otherwise 1 when problems were found, 2 when the command is wrong)\r\n";

		public const Int32 ExitClean = 0;

		public const Int32 ExitProblems = 1;

		public const Int32 ExitUsage = 2;

		public const String DefaultLogName = "DatExplorerIII-msg.log";

		/// <summary>True when the arguments ask for the check of message files.</summary>
		public static Boolean IsCommand(IReadOnlyList<String> args)
		{
			return args.Count > 0 && args[0].Equals(Switch, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Runs the command and returns the exit code of the program.</summary>
		/// <param name="args">The arguments of the program (the first one is <see cref="Switch"/>).</param>
		/// <param name="console">Where the report is written (null when the program has no console).</param>
		/// <param name="text">How the problems are said.</param>
		/// <param name="workingFolder">Where the log goes when the program has no console and no log was asked for.</param>
		public static Int32 Run(IReadOnlyList<String> args, TextWriter? console, MsgReportText text, String workingFolder)
		{
			String? logPath = null;
			Boolean noExitCode = false;
			MsgIssueSeverity minimum = MsgIssueSeverity.Medium;
			List<String> targets = [];

			for (Int32 index = 1; index < args.Count; index++)
			{
				String argument = args[index];
				if (argument.Equals("--no-exitcode", StringComparison.OrdinalIgnoreCase))
				{
					noExitCode = true;
				}
				else if (argument.Equals("--log", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
				{
					logPath = args[++index];
				}
				else if (argument.Equals("--min-severity", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count && Enum.TryParse(args[index + 1], true, out MsgIssueSeverity parsed))
				{
					minimum = parsed;
					index++;
				}
				else if (argument is "--help" or "-h" or "/?")
				{
					console?.Write(UsageText);
					return ExitClean;
				}
				else if (argument.StartsWith("--", StringComparison.Ordinal))
				{
					console?.WriteLine("Unknown option or missing value: " + argument);
					console?.Write(UsageText);
					return ExitUsage;
				}
				else
				{
					targets.Add(argument);
				}
			}

			if (targets.Count == 0)
				targets.Add(".");

			StringBuilder report = new();
			Int32 scanned = 0;
			Int32 files = 0;
			Int32 problems = 0;
			Boolean failed = false;
			Int32[] bySeverity = new Int32[3];

			foreach (String target in targets)
			{
				foreach (MsgFileReport file in MsgScanner.ScanPath(target))
				{
					if (file.ReadError != null)
					{
						failed = true;
						report.Append(file.Path).AppendLine().Append("  ").Append("Cannot be checked: ").Append(file.ReadError).AppendLine();
						continue;
					}

					scanned++;
					List<MsgIssue> shown = file.Issues.Where(issue => issue.Severity <= minimum).ToList();
					if (shown.Count == 0)
						continue;

					files++;
					report.Append(file.Path).AppendLine();
					foreach (MsgIssue issue in shown)
					{
						problems++;
						bySeverity[(Int32)issue.Severity]++;
						report.Append("  [").Append(text.SeverityName(issue.Severity)).Append("] ")
							.Append(issue.Line.ToString(CultureInfo.InvariantCulture)).Append(':').Append(issue.Column.ToString(CultureInfo.InvariantCulture))
							.Append("  ").Append(text.Describe(issue)).AppendLine();
						if (issue.Excerpt.Length > 0)
							report.Append("      ").Append(issue.Excerpt).AppendLine();
					}
				}
			}

			report.Append(Count(problems, "problem")).Append(" in ").Append(files.ToString(CultureInfo.InvariantCulture))
				.Append(" of ").Append(Count(scanned, "file")).Append(": ")
				.Append(bySeverity[0].ToString(CultureInfo.InvariantCulture)).Append(' ').Append(text.SeverityName(MsgIssueSeverity.Critical)).Append(", ")
				.Append(bySeverity[1].ToString(CultureInfo.InvariantCulture)).Append(' ').Append(text.SeverityName(MsgIssueSeverity.High)).Append(", ")
				.Append(bySeverity[2].ToString(CultureInfo.InvariantCulture)).Append(' ').Append(text.SeverityName(MsgIssueSeverity.Medium)).Append('.').AppendLine();

			console?.Write(report.ToString());
			logPath ??= console == null ? Path.Combine(workingFolder, DefaultLogName) : null;
			if (logPath != null && !WriteLog(logPath, report.ToString(), console))
				return ExitUsage;

			if (noExitCode)
				return ExitClean;

			return failed ? ExitUsage : problems > 0 ? ExitProblems : ExitClean;
		}

		/// <summary>"1 file", "2 files": the log is in English.</summary>
		private static String Count(Int32 number, String noun)
		{
			return number.ToString(CultureInfo.InvariantCulture) + " " + noun + (number == 1 ? String.Empty : "s");
		}

		private static Boolean WriteLog(String path, String report, TextWriter? console)
		{
			try
			{
				File.WriteAllText(path, report, new UTF8Encoding(false));
				return true;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				console?.WriteLine("Cannot write the log " + path + ": " + exception.Message);
				return false;
			}
		}
	}
}