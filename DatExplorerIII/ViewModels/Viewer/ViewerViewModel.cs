namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Infrastructure;
	using Models.Archives;
	using Models.GameFiles;
	using Models.Interface;
	using Scripts;
	using Services.Interface;
	using Services.Platform;

	/// <summary>
	/// The viewer of a file: it reads the file, then shows it as a text, a picture or a sound according to its extension.
	/// The same class serves the preview pane of the main window and the separate viewer windows.
	/// </summary>
	public sealed partial class ViewerViewModel : ObservableObject, IDisposable
	{
		/// <summary>A file larger than this is not read for a preview.</summary>
		private const Int64 MaxEntryBytes = 64L * 1024 * 1024;

		/// <summary>Only the beginning of a larger text is shown.</summary>
		private const Int32 MaxTextBytes = 4 * 1024 * 1024;

		private CancellationTokenSource? _loading;

		/// <summary>When the last file was asked for (<see cref="System.Diagnostics.Stopwatch"/> time stamp).</summary>
		private Int64 _lastLoad;

		/// <summary>
		/// A file asked for this soon after the previous one (the arrow keys go through a list) waits a little before it is read: when
		/// the next one comes meanwhile, the work of reading and showing it is saved.
		/// </summary>
		private static readonly TimeSpan QuickSuccession = TimeSpan.FromMilliseconds(150);

		private static readonly TimeSpan QuickSuccessionDelay = TimeSpan.FromMilliseconds(120);
		private String _title = String.Empty;
		private String _message = String.Empty;
		private Boolean _isLoading;
		private FileViewerKind? _kind;

		private readonly IUiThread _uiThread;

		public ViewerViewModel(IUiThread uiThread)
		{
			_uiThread = uiThread;
			Text = new TextViewerViewModel(uiThread);
			Image = new ImageViewerViewModel(uiThread);
			Audio = new AudioViewerViewModel(uiThread);
			Video = new VideoViewerViewModel(uiThread);
			Character = new CharacterViewerViewModel();
			Prototype = new PrototypeViewerViewModel();
			Table = new TableViewerViewModel();
			Map = new MapViewerViewModel();
			_message = Loc.Get("Str.ViewerNoPreview");
		}

		public TextViewerViewModel Text { get; }

		public ImageViewerViewModel Image { get; }

		public AudioViewerViewModel Audio { get; }

		public VideoViewerViewModel Video { get; }

		public CharacterViewerViewModel Character { get; }

		public PrototypeViewerViewModel Prototype { get; }

		public TableViewerViewModel Table { get; }

		public MapViewerViewModel Map { get; }

		/// <summary>
		/// The other archives that are open: a map needs the prototypes and the lists of the game, which may be in another archive
		/// than the map (a map added by a mod, for instance).
		/// </summary>
		public Func<IEnumerable<DatArchive>>? OtherArchives { get; set; }

		public String Title
		{
			get => _title;
			private set => SetProperty(ref _title, value);
		}

		/// <summary>What the viewer says when it shows nothing: no file, an error, a file it cannot show.</summary>
		public String Message
		{
			get => _message;
			private set
			{
				SetProperty(ref _message, value);
				OnPropertyChanged(nameof(HasMessage));
			}
		}

		public Boolean HasMessage => _message.Length > 0;

		public Boolean IsLoading
		{
			get => _isLoading;
			private set => SetProperty(ref _isLoading, value);
		}

		/// <summary>Whether the subtitles of a movie (its .sve file) are shown over it.</summary>
		public Boolean ShowSubtitles
		{
			get => Video.ShowSubtitles;
			set => Video.ShowSubtitles = value;
		}

		/// <summary>The kinds of problems of a message file that its notice does not mention.</summary>
		public IReadOnlyCollection<MsgIssueKind> DisabledMessageChecks
		{
			get => Text.DisabledMessageChecks;
			set => Text.DisabledMessageChecks = value;
		}

		/// <summary>What the button of the problems of a message file does with the report of the file that is shown.</summary>
		public Action<MsgFileReport>? ProblemsRequested
		{
			get => Text.ProblemsRequested;
			set => Text.ProblemsRequested = value;
		}

		public Boolean IsText => _kind == FileViewerKind.Text;

		public Boolean IsImage => _kind == FileViewerKind.Image;

		public Boolean IsAudio => _kind == FileViewerKind.Audio;

		public Boolean IsVideo => _kind == FileViewerKind.Video;

		public Boolean IsCharacter => _kind == FileViewerKind.Character;

		public Boolean IsPrototype => _kind == FileViewerKind.Prototype;

		public Boolean IsTable => _kind == FileViewerKind.Table;

		public Boolean IsMap => _kind == FileViewerKind.Map;

		/// <summary>Reads a file of an archive and shows it. A load in progress is dropped.</summary>
		public async Task LoadAsync(DatArchive archive, DatEntry entry)
		{
			_loading?.Cancel();
			CancellationTokenSource loading = new();
			_loading = loading;
			CancellationToken token = loading.Token;

			Title = entry.Name;
			Stop();

			if (!FileViewers.TryGetKind(entry.Name, out FileViewerKind kind))
			{
				Show(null, Loc.Get("Str.ViewerNotViewable"));
				return;
			}

			if (entry.IsDamaged)
			{
				Show(null, Loc.Format(entry.Problem == DatIssueKind.EntryCorrupt ? "Str.Err.CorruptData" : "Str.Err.OutsideArchive", entry.FullPath));
				return;
			}

			if (entry.UnpackedSize > MaxEntryBytes)
			{
				Show(null, Loc.Format("Str.ViewerTooLarge", SizeFormatter.Format(entry.UnpackedSize)));
				return;
			}

			IsLoading = true;
			Boolean quick = System.Diagnostics.Stopwatch.GetElapsedTime(_lastLoad) < QuickSuccession;
			_lastLoad = System.Diagnostics.Stopwatch.GetTimestamp();
			try
			{
				if (quick)
					await Task.Delay(QuickSuccessionDelay, token);

				Byte[] data = await Task.Run(() => archive.ReadData(entry), token);
				List<PaletteChoice> palettes = kind == FileViewerKind.Image && IsAnimation(entry.Name)
					? await Task.Run(() => ReadPalettes(archive, entry), token)
					: [];
				token.ThrowIfCancellationRequested();

				await ShowContentAsync(archive, entry, kind, data, palettes, token);
			}
			catch (OperationCanceledException)
			{
				// Another file was chosen meanwhile.
			}
			catch (InvalidDataException)
			{
				if (!token.IsCancellationRequested)
					Show(null, Loc.Get("Str.ViewerInvalid"));
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
			{
				if (!token.IsCancellationRequested)
					Show(null, ErrorText.Describe(exception));
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// A damaged file must never bring the program down: whatever the reader tripped on, the viewer says it cannot show it.
				ErrorLog.Write(exception);
				if (!token.IsCancellationRequested)
					Show(null, Loc.Get("Str.ViewerInvalid"));
			}
			finally
			{
				if (_loading == loading)
					IsLoading = false;
			}
		}

		/// <summary>Shows nothing, with a remark.</summary>
		public void Clear(String message)
		{
			_loading?.Cancel();
			Title = String.Empty;
			Stop();
			Show(null, message);
		}

		/// <summary>Stops what moves: a sound being played, an animation.</summary>
		public void Stop()
		{
			Image.Stop();
			Audio.Stop();
			Video.Stop();
		}

		/// <summary>Names the texts of the viewer again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			Text.RefreshLanguage();
			Audio.RefreshLanguage();
			Character.RefreshLanguage();
			Prototype.RefreshLanguage();
			Table.RefreshLanguage();
			Map.RefreshLanguage();
			if (_kind == null && _message.Length > 0 && Title.Length == 0)
				Message = Loc.Get("Str.ViewerNoPreview");
		}

		public void Dispose()
		{
			_loading?.Cancel();
			_loading?.Dispose();
			_loading = null;
			Image.Dispose();
			Audio.Dispose();
			Video.Dispose();
		}

		private void Show(FileViewerKind? kind, String message)
		{
			_kind = kind;
			Message = message;
			OnPropertyChanged(nameof(IsText));
			OnPropertyChanged(nameof(IsImage));
			OnPropertyChanged(nameof(IsAudio));
			OnPropertyChanged(nameof(IsVideo));
			OnPropertyChanged(nameof(IsCharacter));
			OnPropertyChanged(nameof(IsPrototype));
			OnPropertyChanged(nameof(IsTable));
			OnPropertyChanged(nameof(IsMap));

			// What the other viewers still hold (a movie can take tens of megabytes) is let go of.
			if (kind != FileViewerKind.Text)
				Text.Clear();

			if (kind != FileViewerKind.Image)
				Image.Clear();

			if (kind != FileViewerKind.Audio)
				Audio.Clear();

			if (kind != FileViewerKind.Video)
				Video.Clear();

			if (kind != FileViewerKind.Character)
				Character.Clear();

			if (kind != FileViewerKind.Prototype)
				Prototype.Clear();

			if (kind != FileViewerKind.Table)
				Table.Clear();

			if (kind != FileViewerKind.Map)
				Map.Clear();
		}

	}
}