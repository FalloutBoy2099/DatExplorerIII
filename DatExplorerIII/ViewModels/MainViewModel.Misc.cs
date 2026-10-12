namespace DatExplorerIII.ViewModels
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Models;
	using Services;

	public sealed partial class MainViewModel
	{
		private void ChangeLanguage(String code)
		{
			if (code == Loc.Language)
			{
				return;
			}

			Loc.SetLanguage(code);
			_settings.Data.Language = Loc.Language;
			_settings.Save();

			// The texts of the XAML follow by themselves; the ones built by the view model are made again.
			StatusText = Loc.Get("Str.Ready");
			RefreshItems();
			Preview.RefreshLanguage();
			Comparison.Current?.RefreshLanguage();
		}

		private void Associate()
		{
			if (!_dialogs.Confirm(Title, Loc.Get("Str.AskAssociate")))
			{
				return;
			}

			try
			{
				FileAssociationService.Associate();
				StatusText = Loc.Get("Str.Associated");
			}
			catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
				return;
			}

			// Windows keeps the program the user chose in Explorer over the one registered here.
			if (FileAssociationService.IsOverriddenByUserChoice && _dialogs.Confirm(Title, Loc.Get("Str.AssociateOtherChoice"), DialogIcon.Information))
			{
				FileAssociationService.OpenDefaultAppsSettings();
			}
		}

		private void AssociatePatches()
		{
			if (!_dialogs.Confirm(Title, Loc.Get("Str.AskAssociatePatches")))
			{
				return;
			}

			try
			{
				FileAssociationService.AssociatePatches();
				StatusText = Loc.Get("Str.AssociatedPatches");
			}
			catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
			{
				_dialogs.ShowError(ErrorText.Describe(exception));
				return;
			}

			if (FileAssociationService.ArePatchesOverriddenByUserChoice && _dialogs.Confirm(Title, Loc.Get("Str.AssociatePatchesOtherChoice"), DialogIcon.Information))
			{
				FileAssociationService.OpenDefaultAppsSettings();
			}
		}

		/// <summary>
		/// Runs a long operation behind the progress display. Returns false when it failed or was cancelled.
		/// </summary>
		private async Task<Boolean> RunBusyAsync(String title, Boolean canCancel, Func<IProgress<DatProgress>, CancellationToken, Task> work)
		{
			Boolean completed = await Busy.RunAsync(title, canCancel, work);
			if (Busy.WasCancelled)
			{
				StatusText = Loc.Get("Str.Cancelled");
			}

			return completed;
		}
	}
}