namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Threading.Tasks;
	using Models;
	using Services;

	/// <summary>
	/// The settings window: the options of the program that are not in the menus, and how the changes are taken.
	/// </summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Whether the window comes back with the size it had.</summary>
		public Boolean SaveWindowSize => _settings.Data.SaveWindowSize;

		/// <summary>Whether the window comes back where it was.</summary>
		public Boolean SaveWindowPosition => _settings.Data.SaveWindowPosition;

		/// <summary>Whether the movies are shown with their subtitles.</summary>
		public Boolean IsShowSubtitles => _settings.Data.ShowSubtitles;

		/// <summary>Comparison: whether the two versions of a file scroll together.</summary>
		public Boolean IsCompareSyncScroll => _settings.Data.CompareSyncScroll;

		/// <summary>Comparison: what is ignored, and how the content of the files is compared.</summary>
		public ComparisonOptions ComparisonOptions => _settings.Data.ComparisonOptions;

		/// <summary>A viewer for a file, set as the user wants (the other archives, the subtitles of the movies).</summary>
		private ViewerViewModel CreateViewer()
		{
			return new ViewerViewModel(_uiThread) { OtherArchives = OpenArchives, ShowSubtitles = _settings.Data.ShowSubtitles };
		}

		/// <summary>Gives the option of the subtitles to the viewers that are open (the preview and those of a comparison).</summary>
		private void ApplySubtitlesOption()
		{
			Preview.ShowSubtitles = _settings.Data.ShowSubtitles;
			if (_comparison != null)
			{
				_comparison.LeftViewer.ShowSubtitles = _settings.Data.ShowSubtitles;
				_comparison.RightViewer.ShowSubtitles = _settings.Data.ShowSubtitles;
			}
		}

		/// <summary>Opens the settings window; when the user accepts, the changes apply at once.</summary>
		internal async Task OpenSettingsAsync()
		{
			SettingsDialogViewModel model = new(_settings.Data);
			if (!_dialogs.ShowSettings(model))
			{
				return;
			}

			await ApplySettingsAsync(model);
		}

		/// <summary>Takes the values of the settings window. A comparison that is open is made again when what it ignores changed.</summary>
		internal async Task ApplySettingsAsync(SettingsDialogViewModel model)
		{
			IsSingleInstance = model.SingleInstance;
			IsWindowFrameVisible = model.WindowFrameVisible;
			IsBlockSeparatorsVisible = model.BlockSeparatorsVisible;
			_settings.Data.SaveWindowSize = model.SaveWindowSize;
			_settings.Data.SaveWindowPosition = model.SaveWindowPosition;
			_settings.Data.CompareSyncScroll = model.CompareSyncScroll;
			_settings.Data.ShowSubtitles = model.ShowSubtitles;
			_settings.Data.ScrollBarWidth = ScrollBarMetrics.Clamp((Int32)Math.Round(model.ScrollBarWidth), ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.MaximumWidth);
			_settings.Data.ScrollBarMinimumThumb = ScrollBarMetrics.Clamp((Int32)Math.Round(model.ScrollBarMinimumThumb), ScrollBarMetrics.MinimumThumbLength, ScrollBarMetrics.MaximumThumbLength);
			ScrollBarMetrics.Apply(_settings.Data.ScrollBarWidth, _settings.Data.ScrollBarMinimumThumb);
			ApplySubtitlesOption();

			ComparisonOptions options = model.ToComparisonOptions();
			Boolean optionsChanged = options != _settings.Data.ComparisonOptions;
			_settings.Data.ComparisonOptions = options;
			_settings.Save();

			OnPropertyChanged(nameof(SaveWindowSize));
			OnPropertyChanged(nameof(SaveWindowPosition));
			OnPropertyChanged(nameof(IsCompareSyncScroll));
			OnPropertyChanged(nameof(IsShowSubtitles));
			OnPropertyChanged(nameof(ComparisonOptions));

			if (optionsChanged && IsComparing)
			{
				await CompareAgainAsync();
			}
		}
	}
}