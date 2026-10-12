namespace DatExplorerIII.Tests
{
	using System;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>A settings file that a person edited, or that another version wrote, never takes the program down or costs every setting.</summary>
	public sealed class SettingsRobustnessTests
	{
		[Fact]
		public void TheWidthsOfTheBlocksAndTheMessageChecks_AreBroughtBackToWhatMakesSense()
		{
			SettingsData fine = SettingsService.Parse("{\"TreeWidth\": 320, \"PreviewShare\": 0.8, \"DisabledMessageChecks\": [6, 8, 6], \"SaveBlockWidths\": false}");
			Assert.Equal(320, fine.TreeWidth);
			Assert.Equal(0.8, fine.PreviewShare);
			Assert.Equal([MsgIssueKind.DuplicateId, MsgIssueKind.WordTooLong], fine.DisabledMessageChecks);
			Assert.False(fine.SaveBlockWidths);

			SettingsData wrong = SettingsService.Parse("{\"TreeWidth\": 3, \"PreviewShare\": -2, \"DisabledMessageChecks\": [17, 3]}");
			Assert.Equal(0, wrong.TreeWidth);
			Assert.Equal(0, wrong.PreviewShare);
			Assert.Equal([MsgIssueKind.StrayCloseBrace], wrong.DisabledMessageChecks);

			SettingsData huge = SettingsService.Parse("{\"TreeWidth\": 90000, \"PreviewShare\": 500}");
			Assert.Equal(SettingsData.MaximumColumnWidth, huge.TreeWidth);
			Assert.Equal(SettingsData.MaximumPreviewShare, huge.PreviewShare);

			SettingsData defaults = new();
			Assert.True(defaults.SaveBlockWidths);
			Assert.Equal([MsgIssueKind.NoFinalEmptyLine], defaults.DisabledMessageChecks);
			Assert.False(defaults.DoubleClickMessageProblems);
		}

		[Fact]
		public void ASettingThatIsNull_KeepsItsDefault_WhereAValueIsNeeded()
		{
			SettingsData data = SettingsService.Parse("{\"ComparisonOptions\": null, \"RecentFiles\": null, \"Theme\": null, \"ViewMode\": null, \"LastDatFolder\": null}");

			Assert.NotNull(data.ComparisonOptions);
			Assert.NotNull(data.RecentFiles);
			Assert.Equal("Automatic", data.Theme);
			Assert.Equal("Details", data.ViewMode);
			Assert.Null(data.LastDatFolder);

			// The window of the settings opens on such a file.
			Assert.NotNull(new SettingsDialogViewModel(data));
		}

		[Fact]
		public void OneSettingThatIsNotValid_DoesNotCostTheOthers()
		{
			SettingsData data = SettingsService.Parse("{\"SingleInstance\": false, \"ComparisonOptions\": {\"Method\": \"Nope\"}, \"Language\": 5, \"CompareTreeVisible\": \"yes\", \"Theme\": \"Dark\", \"Window\": {\"Left\": \"x\"}}");

			Assert.False(data.SingleInstance);
			Assert.Equal("Dark", data.Theme);
			Assert.Equal(new ComparisonOptions(), data.ComparisonOptions);
			Assert.Null(data.Language);
			Assert.True(data.CompareTreeVisible);
			Assert.Null(data.Window);
		}

		[Fact]
		public void TheValidSettings_AreReadAsBefore()
		{
			SettingsData data = SettingsService.Parse("{\"ComparisonOptions\": {\"Method\": \"Binary\", \"IgnoreCase\": true}, \"Window\": {\"Left\": 10, \"Top\": 20, \"Width\": 800, \"Height\": 600, \"IsMaximized\": true}, \"RecentFiles\": [\"a.dat\", \"b.dat\"], \"Language\": \"fr\"}");

			Assert.Equal(CompareMethod.Binary, data.ComparisonOptions.Method);
			Assert.True(data.ComparisonOptions.IgnoreCase);
			Assert.Equal(new WindowPlacement(10, 20, 800, 600, true), data.Window);
			Assert.Equal(["a.dat", "b.dat"], data.RecentFiles);
			Assert.Equal("fr", data.Language);
		}

		[Theory]
		[InlineData("")]
		[InlineData("not json")]
		[InlineData("[]")]
		[InlineData("42")]
		[InlineData("null")]
		[InlineData("{\"RecentFiles\": [1, 2]}")]
		public void WhatIsNotASettingsFile_GivesTheDefaults(String json)
		{
			SettingsData data = SettingsService.Parse(json);

			Assert.Equal(new SettingsData().ScrollBarWidth, data.ScrollBarWidth);
			Assert.Empty(data.RecentFiles);
			Assert.True(data.SingleInstance);
		}

		[Fact]
		public void TheSortAndTheColumnWidths_AreKeptWhenTheyAreValid_AndBackToTheDesignWhenTheyAreNot()
		{
			SettingsData good = SettingsService.Parse("{\"SortColumn\": \"Size\", \"SortDescending\": true, \"ColumnWidths\": [150, 70, 90, 5000]}");
			Assert.Equal("Size", good.SortColumn);
			Assert.True(good.SortDescending);
			Assert.Equal([150.0, 70.0, 90.0, SettingsData.MaximumColumnWidth], good.ColumnWidths);

			// An unknown column, widths for another number of columns: the defaults.
			SettingsData bad = SettingsService.Parse("{\"SortColumn\": \"Nope\", \"ColumnWidths\": [150, 70]}");
			Assert.Equal("Name", bad.SortColumn);
			Assert.Empty(bad.ColumnWidths);
			Assert.Empty(new SettingsData().ColumnWidths);
		}

		[Fact]
		public void TheVolumeOfTheSounds_StaysBetween0And100()
		{
			Assert.Equal(100, new SettingsData().AudioVolume);
			Assert.Equal(100, SettingsService.Parse("{\"AudioVolume\": 400}").AudioVolume);
			Assert.Equal(0, SettingsService.Parse("{\"AudioVolume\": -3}").AudioVolume);
			SettingsData kept = SettingsService.Parse("{\"AudioVolume\": 35, \"AudioLoop\": true}");
			Assert.Equal(35, kept.AudioVolume);
			Assert.True(kept.AudioLoop);
		}

		[Fact]
		public void TheLists_LoseTheirEmptyEntries_AndTheNumbersComeBackToTheirLimits()
		{
			SettingsData data = SettingsService.Parse("{\"RecentFiles\": [\"a.dat\", null, \"\", \"  \", \"b.dat\"], \"ScrollBarWidth\": 99999, \"ScrollBarMinimumThumb\": -5}");

			Assert.Equal(["a.dat", "b.dat"], data.RecentFiles);
			Assert.Equal(ScrollBarMetrics.MaximumWidth, data.ScrollBarWidth);
			Assert.Equal(ScrollBarMetrics.MinimumThumbLength, data.ScrollBarMinimumThumb);
		}
	}
}