namespace DatExplorerIII.Services.Checks
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Models.GameFiles;
	using Services.Interface;

	/// <summary>The problems of message files in a few words: "13 problems: 4 critical, 8 high, 1 medium".</summary>
	public static class MsgIssueSummary
	{
		/// <summary>"4 critical, 8 high, 1 medium": the severities that have problems.</summary>
		public static String Breakdown(Int32 critical, Int32 high, Int32 medium)
		{
			List<String> parts = [];
			if (critical > 0)
				parts.Add(Loc.Plural("Str.PluralCritical", critical));

			if (high > 0)
				parts.Add(Loc.Plural("Str.PluralHigh", high));

			if (medium > 0)
				parts.Add(Loc.Plural("Str.PluralMedium", medium));

			return String.Join(Loc.Get("Str.MsgListSeparator"), parts);
		}

		/// <summary>"13 problems: 4 critical, 8 high, 1 medium"; empty when there is no problem.</summary>
		public static String Describe(Int32 critical, Int32 high, Int32 medium)
		{
			Int32 total = critical + high + medium;
			return total == 0 ? String.Empty : Loc.Format("Str.MsgCountsFormat", Loc.Plural("Str.PluralProblems", total), Breakdown(critical, high, medium));
		}

		public static String Describe(IReadOnlyCollection<MsgIssue> issues)
		{
			return Describe(
				issues.Count(issue => issue.Severity == MsgIssueSeverity.Critical),
				issues.Count(issue => issue.Severity == MsgIssueSeverity.High),
				issues.Count(issue => issue.Severity == MsgIssueSeverity.Medium));
		}
	}
}