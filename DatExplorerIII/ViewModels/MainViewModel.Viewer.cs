namespace DatExplorerIII.ViewModels
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
		/// <summary>The selected file when it is a file the viewer can show.</summary>
		private DatEntry? SelectedViewableEntry => SelectedItems is [{ Entry: { } entry }] && FileViewers.IsViewable(entry.Name) ? entry : null;

		/// <summary>Whether the preview pane next to the list is shown.</summary>
		public Boolean IsPreviewVisible
		{
			get => _settings.Data.PreviewPaneVisible;
			set
			{
				if (_settings.Data.PreviewPaneVisible != value)
				{
					_settings.Data.PreviewPaneVisible = value;
					OnPropertyChanged();

					if (value)
					{
						UpdatePreview();
					}
					else
					{
						Preview.Clear(Loc.Get("Str.ViewerNoPreview"));
					}
				}
			}
		}

		private IEnumerable<DatArchive> OpenArchives() => Roots.Select(root => root.Document.Archive).ToList();

		/// <summary>Whether a line shows the limit between the folders, the files and the preview.</summary>
		public Boolean IsBlockSeparatorsVisible
		{
			get => _settings.Data.BlockSeparatorsVisible;
			set
			{
				if (_settings.Data.BlockSeparatorsVisible != value)
				{
					_settings.Data.BlockSeparatorsVisible = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>Whether a frame is drawn around the window, so that its edge shows against a dark desktop.</summary>
		public Boolean IsWindowFrameVisible
		{
			get => _settings.Data.WindowFrameVisible;
			set
			{
				if (_settings.Data.WindowFrameVisible != value)
				{
					_settings.Data.WindowFrameVisible = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>Whether a file opened while the program runs is opened in the running program (saved at once: the next launch reads it).</summary>
		public Boolean IsSingleInstance
		{
			get => _settings.Data.SingleInstance;
			set
			{
				if (_settings.Data.SingleInstance != value)
				{
					_settings.Data.SingleInstance = value;
					_settings.Save();
					OnPropertyChanged();
				}
			}
		}

		/// <summary>Whether a double click on a file the viewer can show opens it there.</summary>
		public Boolean DoubleClickOpensViewer
		{
			get => _settings.Data.DoubleClickOpensViewer;
			set
			{
				_settings.Data.DoubleClickOpensViewer = value;
				OnPropertyChanged();
				NotifyDoubleClick();
			}
		}

		/// <summary>Whether a double click on a file does nothing.</summary>
		public Boolean DoubleClickDisabled => _settings.Data.DoubleClickDisabled;

		public Boolean IsDoubleClickViewer => !DoubleClickDisabled && DoubleClickOpensViewer;

		public Boolean IsDoubleClickProgram => !DoubleClickDisabled && !DoubleClickOpensViewer;

		public Boolean IsDoubleClickNothing => DoubleClickDisabled;

		/// <summary>Chooses what a double click on a file does: "Viewer", "Program" or "None".</summary>
		private void SetDoubleClick(String mode)
		{
			_settings.Data.DoubleClickDisabled = mode == "None";
			if (mode != "None")
			{
				_settings.Data.DoubleClickOpensViewer = mode == "Viewer";
			}

			OnPropertyChanged(nameof(DoubleClickOpensViewer));
			NotifyDoubleClick();
		}

		private void NotifyDoubleClick()
		{
			OnPropertyChanged(nameof(DoubleClickDisabled));
			OnPropertyChanged(nameof(IsDoubleClickViewer));
			OnPropertyChanged(nameof(IsDoubleClickProgram));
			OnPropertyChanged(nameof(IsDoubleClickNothing));
		}

		/// <summary>Shows the file of the selection in the preview pane.</summary>
		private void UpdatePreview()
		{
			if (!IsPreviewVisible || Busy.IsBusy)
			{
				return;
			}

			if (CurrentDocument is { } document && SelectedItems is [{ Entry: { } entry }])
			{
				_ = Preview.LoadAsync(document.Archive, entry);
			}
			else
			{
				Preview.Clear(Loc.Get("Str.ViewerNoPreview"));
			}
		}

		private async Task ViewSelectedItemAsync(Object? parameter)
		{
			if (SelectedViewableEntry is { } entry)
			{
				await ViewEntryAsync(entry);
			}
		}

		/// <summary>Shows a file in a window of its own.</summary>
		private async Task ViewEntryAsync(DatEntry entry)
		{
			if (CurrentDocument is not { } document)
			{
				return;
			}

			ViewerViewModel viewer = CreateViewer();
			_dialogs.ShowViewer(viewer);
			await viewer.LoadAsync(document.Archive, entry);
		}
	}
}