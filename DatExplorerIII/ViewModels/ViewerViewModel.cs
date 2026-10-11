namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats;
	using Infrastructure;
	using Models;
	using Scripts;
	using Services;

	/// <summary>
	/// The viewer of a file: it reads the file, then shows it as a text, a picture or a sound according to its extension.
	/// The same class serves the preview pane of the main window and the separate viewer windows.
	/// </summary>
	public sealed class ViewerViewModel : ObservableObject, IDisposable
	{
		/// <summary>A file larger than this is not read for a preview.</summary>
		private const Int64 MaxEntryBytes = 64L * 1024 * 1024;

		/// <summary>Only the beginning of a larger text is shown.</summary>
		private const Int32 MaxTextBytes = 4 * 1024 * 1024;

		/// <summary>The palettes of an archive that are offered for its pictures.</summary>
		private const Int32 MaxPalettes = 24;

		private CancellationTokenSource? _loading;
		private String _title = String.Empty;
		private String _message = String.Empty;
		private Boolean _isLoading;
		private FileViewerKind? _kind;

		private readonly IUiThread _uiThread;

		public ViewerViewModel(IUiThread uiThread)
		{
			_uiThread = uiThread;
			Text = new TextViewerViewModel();
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
			try
			{
				Byte[] data = await Task.Run(() => archive.ReadData(entry), token);
				List<PaletteChoice> palettes = kind == FileViewerKind.Image && IsAnimation(entry.Name)
					? await Task.Run(() => ReadPalettes(archive, entry), token)
					: new List<PaletteChoice>();
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
				{
					Show(null, Loc.Get("Str.ViewerInvalid"));
				}
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException)
			{
				if (!token.IsCancellationRequested)
				{
					Show(null, ErrorText.Describe(exception));
				}
			}
			catch (Exception exception) when (exception is not OperationCanceledException)
			{
				// A damaged file must never bring the program down: whatever the reader tripped on, the viewer says it cannot show it.
				ErrorLog.Write(exception);
				if (!token.IsCancellationRequested)
				{
					Show(null, Loc.Get("Str.ViewerInvalid"));
				}
			}
			finally
			{
				if (_loading == loading)
				{
					IsLoading = false;
				}
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
			{
				Message = Loc.Get("Str.ViewerNoPreview");
			}
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

		private static Boolean IsPaletteFile(String fileName)
		{
			return fileName.EndsWith(".pal", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".act", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Reads a palette file: .act files hold 8 bit colors, the others the 6 bit colors of Fallout.</summary>
		private static Palette ReadPalette(String fileName, Byte[] data)
		{
			return fileName.EndsWith(".act", StringComparison.OrdinalIgnoreCase) ? PaletteReader.ReadRgb(data) : PaletteReader.Read(data);
		}

		/// <summary>True for a sprite of Fallout, which is drawn with a palette that is not in the file.</summary>
		private static Boolean IsAnimation(String fileName)
		{
			String extension = Path.GetExtension(fileName).ToLowerInvariant();
			return extension is ".frm" or ".fr0" or ".fr1" or ".fr2" or ".fr3" or ".fr4" or ".fr5";
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
			{
				Text.Clear();
			}

			if (kind != FileViewerKind.Image)
			{
				Image.Clear();
			}

			if (kind != FileViewerKind.Audio)
			{
				Audio.Clear();
			}

			if (kind != FileViewerKind.Video)
			{
				Video.Clear();
			}

			if (kind != FileViewerKind.Character)
			{
				Character.Clear();
			}

			if (kind != FileViewerKind.Prototype)
			{
				Prototype.Clear();
			}

			if (kind != FileViewerKind.Table)
			{
				Table.Clear();
			}

			if (kind != FileViewerKind.Map)
			{
				Map.Clear();
			}
		}

		/// <summary>
		/// Shows the content of a file. What is made in the background is checked against <paramref name="token"/> before it is shown: a
		/// file that was chosen after this one has the place, and a slow reading of this one must not take it.
		/// </summary>
		private async Task ShowContentAsync(DatArchive archive, DatEntry entry, FileViewerKind kind, Byte[] data, List<PaletteChoice> palettes, CancellationToken token)
		{
			switch (kind)
			{
				case FileViewerKind.Text:
					if (Path.GetExtension(entry.Name).Equals(".int", StringComparison.OrdinalIgnoreCase))
					{
						await ShowScriptAsync(archive, data, token);
						break;
					}

					Byte[] shown = data.Length > MaxTextBytes ? data.AsSpan(0, MaxTextBytes).ToArray() : data;
					Text.Show(shown, data.Length, Path.GetExtension(entry.Name));
					Show(kind, String.Empty);
					break;

				case FileViewerKind.Image:
					await Task.Run(() => ShowImage(entry.Name, data, palettes, token), token);
					break;

				case FileViewerKind.Audio:
					Show(kind, String.Empty);
					await Audio.ShowAsync(data, Path.GetExtension(entry.Name));
					break;

				case FileViewerKind.Video:
					MveMovie movie = await Task.Run(() => MveReader.Read(data), token);
					(SveFile File, String Path)? subtitles = await Task.Run(() => FindSubtitles(archive, entry), token);
					token.ThrowIfCancellationRequested();
					Show(kind, String.Empty);
					Video.Show(movie, subtitles?.File, subtitles?.Path);
					break;

				case FileViewerKind.Character:
					if (Path.GetExtension(entry.Name).Equals(".lip", StringComparison.OrdinalIgnoreCase))
					{
						LipFile lip = LipReader.Read(data);
						Character.ShowSheet(() => LipSheet.Build(lip));
					}
					else
					{
						Character.Show(GcdReader.Read(data));
					}

					Show(kind, String.Empty);
					break;

				case FileViewerKind.Prototype:
					if (ArcanumProReader.IsArcanumPro(data))
					{
						Prototype.Show(ArcanumProReader.Read(data));
					}
					else
					{
						Prototype.Show(ProReader.Read(data));
					}

					Show(kind, String.Empty);
					break;

				case FileViewerKind.Map:
					MapResources resources = new(path => ReadGameFile(archive, path));
					MapFile map = await Task.Run(() => MapReader.Read(data, resources), token);
					token.ThrowIfCancellationRequested();
					Map.Show(map);
					Show(kind, String.Empty);
					break;

				case FileViewerKind.Table:
					String tableExtension = Path.GetExtension(entry.Name).ToLowerInvariant();
					switch (tableExtension)
					{
						case ".dlg":
							Table.Show(DlgReader.Read(data));
							break;

						case ".gam":
							Table.Show(GamReader.Read(data));
							break;

						case ".sve":
							Table.Show(SveReader.Read(data));
							break;

						default:
							Table.Show(MesReader.Read(data));
							break;
					}

					Show(kind, String.Empty);
					break;
			}
		}

		/// <summary>
		/// Shows a compiled script (.int) as the source it decompiles to, with the colors of a source. The decompiler is told which
		/// game the script is for (some opcodes are not the same in Fallout 1 and 2), and tolerates the scripts whose calls do not
		/// have all their arguments.
		/// </summary>
		private async Task ShowScriptAsync(DatArchive archive, Byte[] data, CancellationToken token)
		{
			ScriptDecompilerOptions options = new() { Game = archive.Format == DatFormat.Fallout1 ? ScriptGame.Fallout1 : ScriptGame.Fallout2 };
			try
			{
				DecompiledScript script = await Task.Run(() => ScriptDecompiler.DecompileLeniently(data, options, token), token);
				token.ThrowIfCancellationRequested();
				String note = Loc.Get("Str.ViewerDecompiled");
				if (script.Messages.Count > 0)
				{
					note += Loc.Format("Str.ViewerDecompiledWarnings", script.Messages.Count);
				}

				Byte[] text = System.Text.Encoding.UTF8.GetBytes(script.Text);
				Text.Show(text, text.Length, ".ssl", note);
				Show(FileViewerKind.Text, String.Empty);
			}
			catch (ScriptFormatException exception) when (!token.IsCancellationRequested)
			{
				Show(null, Loc.Format("Str.ViewerScriptError", exception.Message));
			}
		}

		/// <summary>
		/// The subtitles of a movie: the .sve file that has its name, in the archive of the movie first, then in the other open
		/// ones. When there are several (one for each language of the game), the one of the language of the interface is taken,
		/// else the English one, else the first.
		/// </summary>
		private (SveFile File, String Path)? FindSubtitles(DatArchive archive, DatEntry movie)
		{
			String wanted = Path.GetFileNameWithoutExtension(movie.Name) + ".sve";
			String language = Loc.Language switch
			{
				"fr" => "french",
				"de" => "german",
				"es" => "spanish",
				"it" => "italian",
				_ => "english",
			};

			List<(DatArchive Archive, DatEntry Entry, Int32 Rank)> found = new();
			try
			{
				foreach (DatArchive candidate in new[] { archive }.Concat(OtherArchives?.Invoke() ?? Array.Empty<DatArchive>()).Distinct())
				{
					foreach (DatEntry entry in candidate.Entries)
					{
						if (!entry.IsDamaged && entry.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
						{
							String folder = Path.DirectorySeparatorChar + entry.DirectoryKey.Trim(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
							String own = Path.DirectorySeparatorChar + language + Path.DirectorySeparatorChar;
							String english = Path.DirectorySeparatorChar + "english" + Path.DirectorySeparatorChar;
							found.Add((candidate, entry, folder.Contains(own, StringComparison.Ordinal) ? 0 : folder.Contains(english, StringComparison.Ordinal) ? 1 : 2));
						}
					}
				}

				if (found.Count == 0)
				{
					return null;
				}

				(DatArchive source, DatEntry chosen, _) = found.OrderBy(item => item.Rank).First();
				return (SveReader.Read(source.ReadData(chosen)), chosen.FullPath);
			}
			catch (Exception exception) when (exception is DatException or IOException or InvalidOperationException or InvalidDataException)
			{
				// Subtitles are a bonus: a movie is shown without them when they cannot be read.
				return null;
			}
		}

		/// <summary>A file of the game: in the archive of the map first, then in the other open archives.</summary>
		private Byte[]? ReadGameFile(DatArchive archive, String path)
		{
			IEnumerable<DatArchive> archives = new[] { archive }.Concat(OtherArchives?.Invoke() ?? Array.Empty<DatArchive>()).Distinct();
			foreach (DatArchive candidate in archives)
			{
				try
				{
					DatEntry? entry = candidate.Find(path);
					if (entry == null || entry.IsDamaged)
					{
						continue;
					}

					return candidate.ReadData(entry);
				}
				catch (Exception exception) when (exception is DatException or IOException or InvalidOperationException)
				{
					// This copy cannot be read (or the archive is being changed at this moment): another archive may hold one.
				}
			}

			return null;
		}

		private void ShowImage(String fileName, Byte[] data, List<PaletteChoice> palettes, CancellationToken token)
		{
			// What is shown on the interface thread is not shown when another file was chosen meanwhile.
			void OnUi(Action action) => _uiThread.Invoke(() =>
			{
				token.ThrowIfCancellationRequested();
				action();
			});

			String extension = Path.GetExtension(fileName).ToLowerInvariant();
			if (extension is ".pal" or ".act")
			{
				Palette palette = ReadPalette(fileName, data);
				OnUi(() => Image.ShowPalette(palette));
			}
			else if (extension == ".rix")
			{
				RixImage picture = RixReader.Read(data);
				OnUi(() => Image.ShowPicture(picture));
			}
			else if (extension == ".art")
			{
				ArtImage art = ArtReader.Read(data);
				List<PaletteChoice> artPalettes = art.Palettes.Select((palette, index) => new PaletteChoice(Loc.Format("Str.PaletteNumber", index + 1), palette)).ToList();
				OnUi(() => Image.ShowAnimation(art.Image, artPalettes));
			}
			else if (extension is ".aaf" or ".fon")
			{
				FontFile font = extension == ".aaf" ? AafReader.Read(data) : FonReader.Read(data);
				System.Windows.Media.Imaging.BitmapSource sheet = ImageRenderer.Render(font);
				String info = font.Format == FontFormat.Aaf
					? Loc.Format("Str.ViewerFontAaf", font.Glyphs.Count, font.Height, font.Spacing, font.SpaceWidth, font.LineSpacing)
					: Loc.Format("Str.ViewerFontFon", font.Glyphs.Count, font.Height, font.Spacing);
				OnUi(() => Image.ShowBitmap(sheet, info));
			}
			else if (extension is ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif")
			{
				System.Windows.Media.Imaging.BitmapSource bitmap = PictureDecoder.Decode(data);
				OnUi(() => Image.ShowBitmap(bitmap));
			}
			else
			{
				FrmImage animation = FrmReader.Read(data);
				OnUi(() => Image.ShowAnimation(animation, palettes.Count > 0 ? palettes : new List<PaletteChoice> { new(Loc.Get("Str.PaletteBuiltIn"), DefaultPalette.Palette), new(Loc.Get("Str.PaletteGray"), Palette.Grayscale()) }));
			}

			OnUi(() => Show(FileViewerKind.Image, String.Empty));
		}

		/// <summary>
		/// The palettes a sprite can be shown with: the palette that has the name of the picture (hero.pal for hero.frm)
		/// when there is one, else the palette of the game (color.pal) of the archive or, when it has none, the one the program holds, a grayscale,
		/// and every palette file (.pal, .act) of the archive.
		/// </summary>
		private static List<PaletteChoice> ReadPalettes(DatArchive archive, DatEntry picture)
		{
			List<PaletteChoice> choices = new();
			List<DatEntry> files = archive.Entries
				.Where(entry => IsPaletteFile(entry.Name) && !entry.IsDamaged)
				.OrderBy(entry => entry.Directory.Length)
				.ThenBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase)
				.Take(MaxPalettes)
				.ToList();

			foreach (DatEntry file in files)
			{
				try
				{
					choices.Add(new PaletteChoice(file.FullPath, ReadPalette(file.Name, archive.ReadData(file))));
				}
				catch (Exception exception) when (exception is InvalidDataException or DatException or IOException)
				{
					// A palette that cannot be read is not offered.
				}
			}

			// A palette next to the picture with the same name is the one made for it.
			String ownName = Path.Combine(picture.Directory, Path.GetFileNameWithoutExtension(picture.Name));
			DatEntry? sibling = archive.Find(ownName + ".pal") ?? archive.Find(ownName + ".act");
			PaletteChoice? own = null;
			if (sibling is { IsDamaged: false })
			{
				own = choices.FirstOrDefault(choice => choice.Display.Equals(sibling.FullPath, StringComparison.OrdinalIgnoreCase));
				if (own == null)
				{
					try
					{
						own = new PaletteChoice(sibling.FullPath, ReadPalette(sibling.Name, archive.ReadData(sibling)));
						choices.Insert(0, own);
					}
					catch (Exception exception) when (exception is InvalidDataException or DatException or IOException)
					{
						// A palette that cannot be read is not offered.
					}
				}
			}

			String gray = Loc.Get("Str.PaletteGray");
			PaletteChoice grayscale = new(gray, Palette.Grayscale());

			// The palette of the game that the program holds: all the sprites of Fallout 1 and 2 use it, so an archive without
			// a palette of its own is shown with the right colors anyway.
			PaletteChoice builtIn = new(Loc.Get("Str.PaletteBuiltIn"), DefaultPalette.Palette);

			// The first choice is the one used: the palette of the picture, else color.pal of the archive, else the built-in one.
			PaletteChoice preferred = own ?? choices.FirstOrDefault(choice => choice.Display.Equals("color.pal", StringComparison.OrdinalIgnoreCase)) ?? builtIn;
			List<PaletteChoice> result = new() { new PaletteChoice(Loc.Format("Str.PaletteAuto", preferred.Display), preferred.Palette) };
			if (!ReferenceEquals(preferred, builtIn))
			{
				result.Add(builtIn);
			}

			result.Add(grayscale);
			result.AddRange(choices);
			return result;
		}
	}
}