namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;

	/// <summary>The checks of the message files (.msg): what is critical, what is high, what is medium.</summary>
	public sealed class MsgValidatorTests
	{
		private static IReadOnlyList<MsgIssue> Check(String text) => MsgValidator.Validate(text);

		private static MsgIssue Only(String text)
		{
			IReadOnlyList<MsgIssue> issues = Check(text);
			return Assert.Single(issues);
		}

		[Fact]
		public void ADosEndOfFileMark_IsNotPartOfTheFile()
		{
			Assert.Empty(Check("{1}{}{a}\r\n\u001A"));
			Assert.Empty(Check("{1}{}{a}\r\n\u241A"));
			Assert.Equal(MsgIssueKind.NoFinalEmptyLine, Only("{1}{}{a}\u001A").Kind);
		}

		[Fact]
		public void AFileThatIsRight_HasNoProblem()
		{
			Assert.Empty(Check("# a comment\n{100}{}{Hello}\n{101}{snd01}{Two\nlines}\n{102}{}{}\n\n// ignored text\n"));
			Assert.Empty(Check(String.Empty));
			Assert.Empty(Check("﻿{1}{}{With a byte order mark}\r\n"));
			Assert.Empty(Check("{1}{}{Windows}\r\n{2}{}{line\r\nbreaks}\r\n"));
		}

		[Theory]
		[InlineData("{abc}{}{x}\n")]
		[InlineData("{-5}{}{x}\n")]
		[InlineData("{}{}{x}\n")]
		[InlineData("{  }{}{x}\n")]
		[InlineData("{1.5}{}{x}\n")]
		[InlineData("{12 34}{}{x}\n")]
		[InlineData("{99999999999}{}{x}\n")]
		public void AnIdThatIsNotANumber_IsCritical(String text)
		{
			MsgIssue issue = Only(text);

			Assert.Equal(MsgIssueKind.InvalidId, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Critical, issue.Severity);
			Assert.Equal(1, issue.Line);
		}

		[Fact]
		public void AnIdWithSpacesAroundTheNumber_IsAccepted()
		{
			Assert.Empty(Check("{ 100 }{}{x}\n"));
		}

		[Fact]
		public void AClosingBraceThatIsMissingAtTheEnd_IsCritical()
		{
			MsgIssue issue = Only("{1}{}{fine}\n{2}{}{text without end\nmore text\n");

			Assert.Equal(MsgIssueKind.UnclosedBrace, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Critical, issue.Severity);
			Assert.Equal(2, issue.Line);
		}

		[Fact]
		public void AClosingBraceThatIsMissingBeforeTheNextMessage_IsCritical_AndTheNextMessageIsStillRead()
		{
			IReadOnlyList<MsgIssue> issues = Check("{1}{}{text\n{2}{}{ok}\n{2}{}{again}\n");

			Assert.Equal(MsgIssueKind.UnexpectedOpenBrace, issues[0].Kind);
			Assert.Equal(2, issues[0].Line);
			Assert.Equal(1, issues[0].Column);

			// The message after it was read: its number is found again on the next line.
			Assert.Equal(MsgIssueKind.DuplicateId, issues[1].Kind);
			Assert.Equal(3, issues[1].Line);
			Assert.Equal(2, issues[1].Other);
		}

		[Fact]
		public void AnOpeningBraceInsideAField_IsCritical()
		{
			// What follows the brace is read as a message of its own, which has only two pairs of braces.
			IReadOnlyList<MsgIssue> issues = Check("{1382{}{My eyes}\n");

			Assert.Equal([MsgIssueKind.UnexpectedOpenBrace, MsgIssueKind.WrongFieldCount], issues.Select(issue => issue.Kind));
			Assert.Equal(6, issues[0].Column);
		}

		[Theory]
		[InlineData("{1}{text}\n", 2)]
		[InlineData("{1}\n", 1)]
		[InlineData("{1}{}{a}{b}\n", 4)]
		[InlineData("{1}{}{a}{2}{}{b}\n", 6)]
		public void ANumberOfPairsOfBracesThatIsNotThree_IsCritical(String text, Int32 pairs)
		{
			MsgIssue issue = Only(text);

			Assert.Equal(MsgIssueKind.WrongFieldCount, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Critical, issue.Severity);
			Assert.Equal(pairs, issue.Value);
		}

		[Theory]
		[InlineData("{1}{}{a}}\n")]
		[InlineData("{1}{}{a}\n}\n")]
		[InlineData("}{1}{}{a}\n")]
		public void AnExtraClosingBrace_IsCritical(String text)
		{
			Assert.Equal(MsgIssueKind.StrayCloseBrace, Only(text).Kind);
		}

		[Theory]
		[InlineData("{1}{}{   }\n", 1)]
		[InlineData("{1}{}{text\n}\n", 2)]
		[InlineData("{1}{}{text\n   \n  }\n", 3)]
		[InlineData("{1}{}{text\r\n }\r\n", 2)]
		public void ALastLineThatHoldsOnlySpacesOrLineBreaks_IsCritical(String text, Int32 line)
		{
			MsgIssue issue = Only(text);

			Assert.Equal(MsgIssueKind.BlankLastLine, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Critical, issue.Severity);
			Assert.Equal(line, issue.Line);
		}

		[Fact]
		public void AnEmptyText_AndATextThatEndsWithWordsOnItsLastLine_AreNotBlank()
		{
			Assert.Empty(Check("{1}{}{}\n{2}{}{one\ntwo}\n{3}{}{  spaces around  }\n"));
		}

		[Fact]
		public void ANumberUsedTwice_IsHigh_AndTellsWhereItWasUsedFirst()
		{
			IReadOnlyList<MsgIssue> issues = Check("{5}{}{a}\n{6}{}{b}\n\n{5}{}{c}\n{5}{}{d}\n");

			Assert.Equal(2, issues.Count);
			Assert.All(issues, issue => Assert.Equal(MsgIssueSeverity.High, issue.Severity));
			Assert.Equal(4, issues[0].Line);
			Assert.Equal(5, issues[0].Value);
			Assert.Equal(1, issues[0].Other);
			Assert.Equal(5, issues[1].Line);
			Assert.Equal(1, issues[1].Other);
			Assert.Equal("{5}{}{c}", issues[0].Excerpt);
		}

		[Fact]
		public void ADuplicate_IsFound_WhateverTheSpellingOfTheNumber()
		{
			Assert.Equal(MsgIssueKind.DuplicateId, Check("{7}{}{a}\n{007}{}{b}\n").Single().Kind);
		}

		[Fact]
		public void ATextOfMoreThan1024Characters_IsHigh_ButNotOneOf1024()
		{
			String text1024 = Words(1024);
			String text1025 = Words(1025);

			Assert.Empty(Check("{1}{}{" + text1024 + "}\n"));
			MsgIssue issue = Only("{1}{}{" + text1025 + "}\n");
			Assert.Equal(MsgIssueKind.TextTooLong, issue.Kind);
			Assert.Equal(MsgIssueSeverity.High, issue.Severity);
			Assert.Equal(1025, issue.Value);
		}

		/// <summary>A text of that many characters made of short words.</summary>
		private static String Words(Int32 length)
		{
			StringBuilder builder = new();
			while (builder.Length < length)
			{
				builder.Append(builder.Length % 10 == 9 ? ' ' : 'a');
			}

			return builder.ToString(0, length);
		}

		[Fact]
		public void TheLineBreaksOfATextAreNotCounted()
		{
			// 1024 characters on 3 lines: right. The same with one more character: too long.
			String lines = Words(340) + "\n" + Words(340) + "\r\n" + Words(344);

			Assert.Empty(Check("{1}{}{" + lines + "}\n"));
			Assert.Equal(MsgIssueKind.TextTooLong, Only("{1}{}{" + lines + "b}\n").Kind);
		}

		[Fact]
		public void AWordOfMoreThan53Characters_IsMedium_ButNotOneOf53()
		{
			Assert.Empty(Check("{1}{}{" + new String('x', 53) + "}\n"));

			MsgIssue issue = Only("{1}{}{short " + new String('x', 54) + " short}\n");
			Assert.Equal(MsgIssueKind.WordTooLong, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Medium, issue.Severity);
			Assert.Equal(54, issue.Value);
		}

		[Fact]
		public void EveryLongWordIsReported_WithItsWholeLength_AndOnLineItIsOn()
		{
			String text = "{1}{}{first line\n" + new String('a', 60) + " and " + new String('b', 70) + "\nlast}\n";

			IReadOnlyList<MsgIssue> issues = Check(text);

			Assert.Equal([60L, 70L], issues.Select(issue => issue.Value));
			Assert.All(issues, issue => Assert.Equal(2, issue.Line));
		}

		[Fact]
		public void APunctuationOrASpace_EndsAWord()
		{
			String text = "{1}{}{" + new String('a', 30) + "-" + new String('b', 30) + " " + new String('c', 30) + "}\n";

			Assert.Empty(Check(text));
		}

		[Fact]
		public void AFileThatDoesNotEndWithAnEmptyLine_IsMedium()
		{
			MsgIssue issue = Only("{1}{}{a}\n{2}{}{b}");

			Assert.Equal(MsgIssueKind.NoFinalEmptyLine, issue.Kind);
			Assert.Equal(MsgIssueSeverity.Medium, issue.Severity);
			Assert.Equal(2, issue.Line);
			Assert.Empty(Check("{1}{}{a}\n{2}{}{b}\n"));
			Assert.Empty(Check("{1}{}{a}\r\n"));
		}

		[Fact]
		public void TheProblems_ComeInTheOrderOfTheFile_WithTheLineAndTheText()
		{
			IReadOnlyList<MsgIssue> issues = Check("{x}{}{a}\n{2}{}{b}\n{2}{}{c}\n{3}{}{ }");

			Assert.Equal([1, 3, 4, 4], issues.Select(issue => issue.Line));
			Assert.Equal([MsgIssueKind.InvalidId, MsgIssueKind.DuplicateId, MsgIssueKind.BlankLastLine, MsgIssueKind.NoFinalEmptyLine], issues.Select(issue => issue.Kind));
			Assert.Equal("{x}{}{a}", issues[0].Excerpt);
		}

		[Fact]
		public void SeveralProblemsOfOneMessage_AreAllReported()
		{
			Int32[] kinds = Check("{a}{}{ }\n").Select(issue => (Int32)issue.Kind).ToArray();

			Assert.Contains((Int32)MsgIssueKind.InvalidId, kinds);
			Assert.Contains((Int32)MsgIssueKind.BlankLastLine, kinds);
		}

		[Fact]
		public void TheSeverityOfEveryKind_IsKnown()
		{
			foreach (MsgIssueKind kind in Enum.GetValues<MsgIssueKind>())
			{
				MsgIssueSeverity severity = MsgIssue.SeverityOf(kind);
				Assert.True(Enum.IsDefined(severity));
			}

			Assert.Equal(MsgIssueSeverity.Critical, MsgIssue.SeverityOf(MsgIssueKind.InvalidId));
			Assert.Equal(MsgIssueSeverity.High, MsgIssue.SeverityOf(MsgIssueKind.DuplicateId));
			Assert.Equal(MsgIssueSeverity.Medium, MsgIssue.SeverityOf(MsgIssueKind.NoFinalEmptyLine));
		}

		[Fact]
		public void ABigFile_IsCheckedInAFractionOfASecond()
		{
			StringBuilder builder = new();
			for (Int32 id = 0; id < 100000; id++)
			{
				builder.Append('{').Append(id).Append("}{}{Some text of a message that is not too short.}\n");
			}

			String text = builder.ToString();
			Check(text);
			Stopwatch watch = Stopwatch.StartNew();
			IReadOnlyList<MsgIssue> issues = Check(text);

			Assert.Empty(issues);
			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1.5), "The check took " + watch.Elapsed);
		}

		[Fact]
		public void TheRealMessageFilesOfFallout_AreCheckedWithoutFailing_WhenTheirFolderIsGiven()
		{
			String? folder = Environment.GetEnvironmentVariable("DATEXPLORERIII_FALLOUT_DIR");
			if (folder == null || !Directory.Exists(folder))
			{
				return;
			}

			Int32 files = 0;
			foreach (String path in Directory.EnumerateFiles(folder, "*.msg", SearchOption.AllDirectories))
			{
				IReadOnlyList<MsgIssue> issues = Check(TextDecoder.Decode(File.ReadAllBytes(path)).Text);
				Assert.All(issues, issue => Assert.True(issue.Line >= 1 && issue.Column >= 1, path));
				files++;
			}

			Assert.True(files >= 0);
		}
	}
}