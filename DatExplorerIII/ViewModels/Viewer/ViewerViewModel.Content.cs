namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats.Audio;
	using Formats.GameFiles;
	using Formats.Movies;
	using Formats.Prototypes;
	using Services.Texts;
	using Models.Archives;
	using Models.Audio;
	using Models.GameFiles;
	using Models.Interface;
	using Services.Checks;
	using Scripts;

	/// <summary>The content of a file: what is made of each kind of file to show it.</summary>
	public sealed partial class ViewerViewModel
	{
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

					if (Path.GetExtension(entry.Name).Equals(".scr", StringComparison.OrdinalIgnoreCase))
					{
						ShowScr(archive, data);
						break;
					}

					if (ArcanumFileText.Handles(Path.GetExtension(entry.Name)))
					{
						ShowArcanumFile(archive, entry, data);
						break;
					}

					if (Path.GetExtension(entry.Name).Equals(".ent", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(entry.Name).Equals(".chr", StringComparison.OrdinalIgnoreCase))
					{
						ShowEnt(data);
						break;
					}

					Byte[] shown = data.Length > MaxTextBytes ? data.AsSpan(0, MaxTextBytes).ToArray() : data;
					Text.SourcePath = entry.FullPath;

					// A list of files says which files it names that are not in its folder (the other open archives count: the game reads
					// all its archives as one).
					LstReport? list = null;
					if (Path.GetExtension(entry.Name).Equals(".lst", StringComparison.OrdinalIgnoreCase) && data.Length <= MaxTextBytes)
					{
						IReadOnlyList<DatArchive> archives = GameArchives(archive);
						list = await Task.Run(() => LstChecker.Check(entry.Directory, entry.Name, TextDecoder.Decode(data).Text, archives), token);
						token.ThrowIfCancellationRequested();
					}

					Text.Show(shown, data.Length, Path.GetExtension(entry.Name), null, list);
					Show(kind, String.Empty);
					break;

				case FileViewerKind.Image:
					await Task.Run(() => ShowImage(archive, entry, data, palettes, token), token);
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
					if (ArcanumProReader.IsArcanumMob(data))
					{
						Prototype.Show(ArcanumProReader.ReadMob(data));
					}
					else if (ArcanumProReader.IsArcanumPro(data))
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
	}
}