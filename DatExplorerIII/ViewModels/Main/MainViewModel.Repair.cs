namespace DatExplorerIII.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Core;
	using Models;
	using Services;

	public sealed partial class MainViewModel
	{
		/// <summary>
		/// Reads every file of the archive to find the damaged ones, then offers to remove them.
		/// </summary>
		private async Task VerifyAsync(Object? parameter)
		{
			DatDocument? document = CurrentDocument;
			if (document == null)
			{
				return;
			}

			DatVerifyResult? result = null;
			Boolean completed = await RunBusyAsync(
				Loc.Get("Str.Verifying"),
				true,
				(progress, token) => Task.Run(() => result = document.Archive.Verify(progress, token), token));

			// The files found damaged are marked in the list.
			document.Refresh();
			RefreshItems();

			if (!completed || result == null)
			{
				return;
			}

			if (result.Damaged.Count == 0)
			{
				StatusText = Loc.Format("Str.VerifyOk", result.Checked);
				_dialogs.ShowMessage(Title, StatusText, DialogIcon.Information);
				return;
			}

			List<String> lines = result.Damaged.Take(MaxListedItems).Select(entry => entry.FullPath).ToList();
			if (result.Damaged.Count > MaxListedItems)
			{
				lines.Add(Loc.Format("Str.AndMore", result.Damaged.Count - MaxListedItems));
			}

			String question = Loc.Format("Str.VerifyDamaged", result.Damaged.Count, result.Checked)
				+ Environment.NewLine + Environment.NewLine + String.Join(Environment.NewLine, lines)
				+ Environment.NewLine + Environment.NewLine + Loc.Get("Str.VerifyRemovePrompt");

			StatusText = Loc.Format("Str.VerifyDamaged", result.Damaged.Count, result.Checked);
			if (_dialogs.Confirm(Title, question, DialogIcon.Warning))
			{
				RemoveDamagedFiles(document);
			}
		}

		private void RemoveDamaged()
		{
			if (CurrentDocument is { } document && _dialogs.Confirm(Title, Loc.Format("Str.RemoveDamagedPrompt", document.Archive.DamagedCount), DialogIcon.Warning))
			{
				RemoveDamagedFiles(document);
			}
		}

		private void RemoveDamagedFiles(DatDocument document)
		{
			Int32 removed = document.Archive.RemoveDamaged();
			document.Refresh();
			RefreshItems();
			StatusText = Loc.Format("Str.RemoveDamagedDone", removed);
		}

		private void ShowProblems()
		{
			if (CurrentDocument is not { } document)
			{
				return;
			}

			DatArchive archive = document.Archive;
			if (archive.Issues.Count == 0)
			{
				_dialogs.ShowMessage(Title, Loc.Get("Str.NoProblems"), DialogIcon.Information);
				return;
			}

			String text = Loc.Format("Str.ProblemsTitle", System.IO.Path.GetFileName(document.FilePath), archive.Issues.Count)
				+ Environment.NewLine + Environment.NewLine + DescribeIssues(archive);
			_dialogs.ShowMessage(Title, text, DialogIcon.Warning);
		}
	}
}