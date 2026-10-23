namespace DatExplorerIII.Services.Interface
{
	using System;
	using System.Collections.Generic;
	using Models.Archives;
	using Models.Comparison;
	using Models.Interface;
	using ViewModels;
	using ViewModels.Viewer;

	/// <summary>
	/// Everything the view models need to ask from the user, so that they do not depend on windows.
	/// </summary>
	public interface IDialogService
	{
		/// <summary>Shows a message with custom buttons. Returns the id of the pressed button, or null when closed.</summary>
		String? Choose(String title, String message, DialogIcon icon, IReadOnlyList<DialogButton> buttons);

		Boolean Confirm(String title, String message, DialogIcon icon = DialogIcon.Question);

		void ShowMessage(String title, String message, DialogIcon icon);

		void ShowError(String message);

		/// <summary>Asks for a text. <paramref name="validate"/> returns an error message, or null when the text is acceptable.</summary>
		String? Prompt(String title, String label, String initialValue, Func<String, String?>? validate);

		IReadOnlyList<String> PickDatFiles(String? initialFolder);

		String? PickNewDatFile(DatFormat format, String? initialFolder);

		/// <summary>
		/// Asks under which name an archive is saved. When <paramref name="chooseFormat"/> is set the user also chooses the
		/// format (an archive made from a folder has none yet); otherwise the file types are those of <paramref name="format"/>.
		/// Returns null when the user gives up.
		/// </summary>
		SaveAsChoice? PickSaveAsFile(String suggestedName, String? initialFolder, DatFormat format, Boolean chooseFormat);

		/// <summary>
		/// Asks which two archives to compare (a DAT file or a folder for each side), starting from the given ones; the archives
		/// that are open and the ones used lately are proposed. Returns null when the user gives up.
		/// </summary>
		ComparisonSources? PickComparisonSources(String? left, String? right, IReadOnlyList<String> suggestions);

		IReadOnlyList<String> PickFiles(String? initialFolder);

		String? PickFolder(String title, String? initialFolder);

		void ShowSearchResults(String title, IReadOnlyList<DatEntry> results, Action<DatEntry> goTo);

		/// <summary>Shows the problems found in message files. The window is not modal.</summary>
		void ShowMessageIssues(MsgIssuesViewModel issues);

		/// <summary>Opens a window that shows a file. The window loads what the viewer model is given.</summary>
		void ShowViewer(ViewerViewModel viewer);

		/// <summary>Shows the settings window with the given values; returns true when the user accepted (the values were changed).</summary>
		Boolean ShowSettings(SettingsDialogViewModel settings);

		void ShowAbout();
	}
}