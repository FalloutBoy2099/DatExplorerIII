namespace DatExplorerIII.Services.Interface
{
	using System;
	using System.Globalization;
	using System.Linq;
	using Core;
	using Models.Archives;

	/// <summary>
	/// Turns errors and findings of the core into texts in the language of the interface.
	/// </summary>
	public static class ErrorText
	{
		/// <summary>
		/// The text of an error. Archive errors are translated; the messages of the system (disk, permissions)
		/// are already in the language of Windows.
		/// </summary>
		public static String Describe(Exception exception)
		{
			if (exception is DatException archiveError && Loc.TryGet("Str.Err." + archiveError.Kind, out String? format))
			{
				try
				{
					return String.Format(CultureInfo.CurrentCulture, format, archiveError.Arguments.ToArray());
				}
				catch (FormatException)
				{
					return exception.Message;
				}
			}

			return exception.Message;
		}

		/// <summary>
		/// The text of a problem found in an archive.
		/// </summary>
		public static String Describe(DatIssue issue)
		{
			return Loc.Format("Str.Issue." + issue.Kind, issue.Path ?? String.Empty, issue.Value);
		}
	}
}