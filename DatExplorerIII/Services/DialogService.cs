namespace DatExplorerIII.Services
{
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Windows;
	using Microsoft.Win32;
	using Models;
	using ViewModels;
	using Views;

	/// <summary>
	/// <see cref="IDialogService"/> backed by WPF windows and the Windows common dialogs.
	/// </summary>
	public sealed class DialogService : IDialogService
	{
		private static Window? Owner
		{
			get
			{
				Window? main = Application.Current.MainWindow;
				return main is { IsLoaded: true } ? main : null;
			}
		}

		public String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons)
		{
			MessageDialog dialog = new(title, message, icon, buttons) { Owner = Owner };
			return dialog.ShowDialog() == true ? dialog.SelectedId : null;
		}

		public Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question)
		{
			DialogButton[] buttons =
			[
				new("yes", Loc.Get("Str.Yes"), IsDefault: true),
				new("no", Loc.Get("Str.No"), IsCancel: true),
			];

			return Choose(title, message, icon, buttons) == "yes";
		}

		public void ShowMessage(String title, String message, DialogIcon icon)
		{
			Choose(title, message, icon, [new DialogButton("ok", Loc.Get("Str.Ok"), true, true)]);
		}

		public void ShowError(String message)
		{
			ShowMessage(Loc.Get("Str.ErrorTitle"), message, DialogIcon.Error);
		}

		public String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate)
		{
			InputDialog dialog = new(title, label, initialValue, validate) { Owner = Owner };
			return dialog.ShowDialog() == true ? dialog.Value : null;
		}

		public IReadOnlyList<String> PickDatFiles(String? initialFolder)
		{
			OpenFileDialog dialog = new()
			{
				Title = Loc.Get("Str.Open"),
				Filter = Loc.Get("Str.DatFilter"),
				Multiselect = true,
				InitialDirectory = initialFolder ?? String.Empty,
			};

			return dialog.ShowDialog(Owner) == true ? dialog.FileNames : Array.Empty<String>();
		}

		public String? PickNewDatFile(DatFormat format, String? initialFolder)
		{
			SaveFileDialog dialog = new()
			{
				Title = Loc.Get(format switch
				{
					DatFormat.Fallout1 => "Str.NewFallout1",
					DatFormat.Arcanum => "Str.NewArcanum",
					_ => "Str.NewFallout2",
				}),
				Filter = Loc.Get("Str.DatFilter"),
				DefaultExt = ".dat",
				AddExtension = true,
				OverwritePrompt = true,
				InitialDirectory = initialFolder ?? String.Empty,
			};

			return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
		}

		public SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat)
		{
			DatFormat[] formats = chooseFormat ? [DatFormat.Fallout2, DatFormat.Fallout1, DatFormat.Arcanum] : [format];
			SaveFileDialog dialog = new()
			{
				Title = Loc.Get("Str.SaveAsTitle"),
				Filter = String.Join("|", formats.Select(candidate => Loc.Get(candidate switch
				{
					DatFormat.Fallout1 => "Str.SaveFilterFallout1",
					DatFormat.Arcanum => "Str.SaveFilterArcanum",
					_ => "Str.SaveFilterFallout2",
				}))),
				FilterIndex = Math.Max(Array.IndexOf(formats, format), 0) + 1,
				DefaultExt = ".dat",
				AddExtension = true,
				OverwritePrompt = true,
				FileName = suggestedName,
				InitialDirectory = initialFolder ?? String.Empty,
			};

			return dialog.ShowDialog(Owner) == true ? new SaveAsChoice(dialog.FileName, formats[dialog.FilterIndex - 1]) : null;
		}

		public ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions)
		{
			CompareSetupDialog dialog = new(Loc.Get("Str.CompareTitle"), left, right, suggestions) { Owner = Owner };
			return dialog.ShowDialog() == true ? dialog.Sources : null;
		}

		public IReadOnlyList<String> PickFiles(String? initialFolder)
		{
			OpenFileDialog dialog = new()
			{
				Title = Loc.Get("Str.ImportFiles"),
				Multiselect = true,
				InitialDirectory = initialFolder ?? String.Empty,
			};

			return dialog.ShowDialog(Owner) == true ? dialog.FileNames : Array.Empty<String>();
		}

		public String? PickFolder(String title, String? initialFolder)
		{
			OpenFolderDialog dialog = new()
			{
				Title = title,
				InitialDirectory = initialFolder ?? String.Empty,
			};

			return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
		}

		public void ShowSearchResults(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo)
		{
			// Not modal: the user keeps browsing while the list stays open.
			new SearchResultsWindow(title, results, goTo) { Owner = Owner }.Show();
		}

		public void ShowViewer(ViewerViewModel viewer)
		{
			// Not modal: the user keeps browsing, and can open several viewers.
			new ViewerWindow(viewer) { Owner = Owner }.Show();
		}

		public Boolean ShowSettings(SettingsDialogViewModel settings)
		{
			return new SettingsWindow(settings) { Owner = Owner }.ShowDialog() == true;
		}

		public void ShowAbout()
		{
			String version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? String.Empty;
			DialogButton[] buttons = [new("close", Loc.Get("Str.Close"), true, true)];
			Choose(Loc.Get("Str.About"), Loc.Format("Str.AboutText", version), DialogIcon.Information, buttons);
		}
	}
}