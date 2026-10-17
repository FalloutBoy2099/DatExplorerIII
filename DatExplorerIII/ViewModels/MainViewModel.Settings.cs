namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
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

		/// <summary>Whether the width of the blocks of the window (the folders, the preview) comes back as it was.</summary>
		public Boolean SaveBlockWidths => _settings.Data.SaveBlockWidths;

		/// <summary>The width of the block of the folders that was kept (0: the width of the design, or nothing is kept).</summary>
		public Double TreeWidth => _settings.Data.SaveBlockWidths ? _settings.Data.TreeWidth : 0;

		/// <summary>The width of the preview compared with the list of the files that was kept (0: the design, or nothing is kept).</summary>
		public Double PreviewShare => _settings.Data.SaveBlockWidths ? _settings.Data.PreviewShare : 0;

		/// <summary>The kinds of problems of the message files that are not looked for.</summary>
		public IReadOnlyCollection<MsgIssueKind> DisabledMessageChecks => _settings.Data.DisabledMessageChecks;

		/// <summary>True when the user turned off every kind of problem: nothing is looked for in the message files.</summary>
		private Boolean AllMessageChecksOff => _settings.Data.DisabledMessageChecks.Count >= Enum.GetValues<MsgIssueKind>().Length;

		/// <summary>Keeps the width of the blocks of the window when it closes (nothing when the user does not want them kept).</summary>
		public void SaveBlocks(Double treeWidth, Double previewShare)
		{
			if (_settings.Data.SaveBlockWidths)
			{
				_settings.Data.TreeWidth = treeWidth;
				_settings.Data.PreviewShare = previewShare;
			}
		}

		/// <summary>Whether the movies are shown with their subtitles.</summary>
		public Boolean IsShowSubtitles => _settings.Data.ShowSubtitles;

		/// <summary>Comparison: whether the two versions of a file scroll together.</summary>
		public Boolean IsCompareSyncScroll => _settings.Data.CompareSyncScroll;

		/// <summary>Comparison: what is ignored, and how the content of the files is compared.</summary>
		public ComparisonOptions ComparisonOptions => _settings.Data.ComparisonOptions;

		/// <summary>A viewer for a file, set as the user wants (the other archives, the subtitles of the movies).</summary>
		private ViewerViewModel CreateViewer()
		{
			ViewerViewModel viewer = new(_uiThread) { OtherArchives = OpenArchives, ShowSubtitles = _settings.Data.ShowSubtitles, DisabledMessageChecks = _settings.Data.DisabledMessageChecks, ProblemsRequested = ShowMessageReport };

			// The search of a text asks what to find through the dialogs of the program.
			viewer.Text.Ask = (title, label, initial, validate) => _dialogs.Prompt(title, label, initial, validate);
			viewer.Text.Inform = message => _dialogs.ShowMessage(Title, message, DialogIcon.Information);

			// The wheel of the mouse magnifies a picture by the notches the user chose (read at each turn: a change applies at once).
			viewer.Image.WheelNotchesSource = () => WheelNotches;

			// The comments of the message files and of the dialogs that are shown are kept for the next file, and for the next run.
			viewer.Table.ShowLineComments = _settings.Data.ShowLineComments;
			viewer.Table.ShowBlockComments = _settings.Data.ShowBlockComments;
			viewer.Table.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(TableViewerViewModel.ShowLineComments))
				{
					_settings.Data.ShowLineComments = viewer.Table.ShowLineComments;
				}
				else if (args.PropertyName == nameof(TableViewerViewModel.ShowBlockComments))
				{
					_settings.Data.ShowBlockComments = viewer.Table.ShowBlockComments;
				}
			};

			// The volume and the repeat are kept for the next sound, and for the next run.
			viewer.Audio.Volume = _settings.Data.AudioVolume;
			viewer.Audio.IsLooping = _settings.Data.AudioLoop;
			viewer.Audio.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(AudioViewerViewModel.Volume))
				{
					_settings.Data.AudioVolume = (Int32)Math.Round(viewer.Audio.Volume);
				}
				else if (args.PropertyName == nameof(AudioViewerViewModel.IsLooping))
				{
					_settings.Data.AudioLoop = viewer.Audio.IsLooping;
				}
			};

			return viewer;
		}

		/// <summary>Gives the option of the subtitles to the viewers that are open (the preview and those of a comparison).</summary>
		private void ApplySubtitlesOption()
		{
			Preview.ShowSubtitles = _settings.Data.ShowSubtitles;
			Comparison.ApplySubtitles();
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
			_settings.Data.ShowHiddenFolders = model.ShowHiddenFolders;
			_settings.Data.SaveWindowSize = model.SaveWindowSize;
			_settings.Data.SaveWindowPosition = model.SaveWindowPosition;
			_settings.Data.SaveBlockWidths = model.SaveBlockWidths;
			if (!model.SaveBlockWidths)
			{
				_settings.Data.TreeWidth = 0;
				_settings.Data.PreviewShare = 0;
			}

			_settings.Data.DisabledMessageChecks = model.DisabledMessageChecks();
			_settings.Data.DoubleClickMessageProblems = model.DoubleClickMessageProblems;
			_settings.Data.CompareSyncScroll = model.CompareSyncScroll;
			_settings.Data.ShowSubtitles = model.ShowSubtitles;
			_settings.Data.ScrollBarWidth = ScrollBarMetrics.Clamp((Int32)Math.Round(model.ScrollBarWidth), ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.MaximumWidth);
			_settings.Data.ScrollBarMinimumThumb = ScrollBarMetrics.Clamp((Int32)Math.Round(model.ScrollBarMinimumThumb), ScrollBarMetrics.MinimumThumbLength, ScrollBarMetrics.MaximumThumbLength);
			_scrollBars.Update(_settings.Data.ScrollBarWidth, _settings.Data.ScrollBarMinimumThumb);
			ApplySubtitlesOption();
			ApplyMessageChecks();

			ComparisonOptions options = model.ToComparisonOptions();
			Boolean optionsChanged = options != _settings.Data.ComparisonOptions;
			_settings.Data.ComparisonOptions = options;
			_settings.Save();

			OnPropertyChanged(nameof(SaveWindowSize));
			OnPropertyChanged(nameof(SaveWindowPosition));
			OnPropertyChanged(nameof(SaveBlockWidths));
			OnPropertyChanged(nameof(DisabledMessageChecks));
			OnPropertyChanged(nameof(IsCompareSyncScroll));
			OnPropertyChanged(nameof(IsShowSubtitles));
			OnPropertyChanged(nameof(ComparisonOptions));

			if (optionsChanged && Comparison.IsComparing)
			{
				await Comparison.CompareAgainAsync();
			}
		}
	}
}