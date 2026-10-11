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
		public void TheLists_LoseTheirEmptyEntries_AndTheNumbersComeBackToTheirLimits()
		{
			SettingsData data = SettingsService.Parse("{\"RecentFiles\": [\"a.dat\", null, \"\", \"  \", \"b.dat\"], \"ScrollBarWidth\": 99999, \"ScrollBarMinimumThumb\": -5}");

			Assert.Equal(["a.dat", "b.dat"], data.RecentFiles);
			Assert.Equal(ScrollBarMetrics.MaximumWidth, data.ScrollBarWidth);
			Assert.Equal(ScrollBarMetrics.MinimumThumbLength, data.ScrollBarMinimumThumb);
		}
	}
}