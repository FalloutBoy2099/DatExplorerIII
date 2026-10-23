namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using DatExplorerIII.Models.Comparison;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Platform;

	/// <summary>The order of the lines of the menus, and the defaults of the options of the view.</summary>
	public sealed class MenuLayoutTests
	{
		private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

		private static String SolutionFolder()
		{
			String? folder = AppContext.BaseDirectory;
			while (folder != null && !File.Exists(Path.Combine(folder, "DatExplorerIII.sln")))
			{
				folder = Path.GetDirectoryName(folder);
			}

			return folder ?? throw new InvalidOperationException("The solution folder was not found.");
		}

		/// <summary>The lines of a menu of the main window: the name of the text of each, "-" for a separator.</summary>
		private static List<String> Lines(String menuTitleKey)
		{
			XDocument window = XDocument.Load(Path.Combine(SolutionFolder(), "DatExplorerIII", "Views", "Main", "MainMenu.xaml"));
			XElement menu = window.Descendants(Presentation + "MenuItem").First(item => (String?)item.Attribute("Header") == "{DynamicResource " + menuTitleKey + "}");

			return menu.Elements().Select(element =>
			{
				if (element.Name.LocalName == "Separator")
					return "-";

				// A title that opens a sub menu holds its text in a TextBlock (to line it up with the other lines).
				String header = (String?)element.Attribute("Header") ?? (String?)element.Descendants(Presentation + "TextBlock").FirstOrDefault()?.Attribute("Text") ?? String.Empty;
				Match match = Regex.Match(header, @"Str\.(\w+)");
				return match.Success ? match.Groups[1].Value : "?";
			}).ToList();
		}

		[Fact]
		public void TheViewMenu_IsInTheOrderAsked()
		{
			Assert.Equal(
				[
					"ViewDetails", "ViewList", "-", "ViewIcons", "-", "PreviewPane", "-", "DoubleClickOpens", "WheelZoom", "-",
					"CompareLayout", "-", "Up", "Refresh",
				],
				Lines("Str.View"));
		}

		[Fact]
		public void TheFileMenu_HasTheCloseLineBetweenTwoSeparators_WhereTheRecentFilesEnd()
		{
			Assert.Equal(
				[
					"Open", "OpenFolder", "New", "-", "Save", "SaveAs", "QuickSave", "-", "CloseDat", "CloseAllDats", "-", "Exit",
				],
				Lines("Str.File"));
		}

		[Fact]
		public void TheToolsMenu_OffersTheMessageCheckAndTheComparison_AfterTheRepairs_AndTheSettingsAtTheEnd()
		{
			Assert.Equal(
				[
					"VerifyIntegrity", "ShowProblems", "RemoveDamaged", "MsgScanCommand", "-", "Compare", "CompareClose", "-", "Settings",
				],
				Lines("Str.Tools"));
		}

		[Fact]
		public void TheOptionsOfTheView_HaveTheirDefaults()
		{
			SettingsData settings = new();

			Assert.True(settings.PreviewPaneVisible);
			Assert.True(settings.SingleInstance);
			Assert.False(settings.WindowFrameVisible);
			Assert.True(settings.BlockSeparatorsVisible);
			Assert.True(settings.SaveWindowSize);
			Assert.True(settings.SaveWindowPosition);
			Assert.True(settings.CompareSyncScroll);
			Assert.Equal(new ComparisonOptions(), settings.ComparisonOptions);
		}
	}
}