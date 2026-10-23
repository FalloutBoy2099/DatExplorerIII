namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats.Graphics;
	using Models.Archives;
	using Models.Graphics;
	using Models.Interface;
	using Services.Checks;
	using Services.Interface;
	using Services.Media;

	/// <summary>The pictures: the palettes a sprite can be shown with, and what says if a picture can be an image of the ending.</summary>
	public sealed partial class ViewerViewModel
	{
		/// <summary>The palettes of an archive that are offered for its pictures.</summary>
		private const Int32 MaxPalettes = 24;

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

		private void ShowImage(DatArchive archive, DatEntry entry, Byte[] data, List<PaletteChoice> palettes, CancellationToken token)
		{
			String fileName = entry.Name;
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
			else if (extension == ".msk")
			{
				MaskImage mask = MskReader.Read(data);
				PixelImage picture = ImageRenderer.Render(mask);
				String info = Loc.Format("Str.ViewerMask", mask.Width, mask.Height, (Int32)Math.Round(100.0 * mask.SetCount / mask.Pixels.Length));
				OnUi(() => Image.ShowBitmap(picture, info));
			}
			else if (extension is ".aaf" or ".fon")
			{
				FontFile font = extension == ".aaf" ? AafReader.Read(data) : FonReader.Read(data);
				PixelImage sheet = ImageRenderer.Render(font);
				String info = font.Format == FontFormat.Aaf
					? Loc.Format("Str.ViewerFontAaf", font.Glyphs.Count, font.Height, font.Spacing, font.SpaceWidth, font.LineSpacing)
					: Loc.Format("Str.ViewerFontFon", font.Glyphs.Count, font.Height, font.Spacing);
				OnUi(() => Image.ShowBitmap(sheet, info));
			}
			else if (extension is ".png" or ".bmp" or ".jpg" or ".jpeg" or ".gif")
			{
				PixelImage bitmap = PictureDecoder.Decode(data);
				OnUi(() => Image.ShowBitmap(bitmap));
			}
			else
			{
				FrmImage animation = FrmReader.Read(data);
				IReadOnlyList<PaletteChoice> shownPalettes = palettes.Count > 0 ? palettes : [new(Loc.Get("Str.PaletteBuiltIn"), DefaultPalette.Palette), new(Loc.Get("Str.PaletteGray"), Palette.Grayscale())];

				// A picture with a palette of its own may be an image of the ending: say so, and give the text to try on it.
				EndingImageCheck? ending = null;
				if (extension == ".frm" && HasOwnPalette(archive, entry))
				{
					FrmFrame first = animation.Directions[0].Frames[0];
					ending = EndingImages.Check(entry.Directory, entry.Name, first.Width, first.Height);
				}

				OnUi(() => Image.ShowAnimation(animation, shownPalettes, ending, ending == null ? null : death => PickEndingText(archive, death)));
			}

			OnUi(() => Show(FileViewerKind.Image, String.Empty));
		}

		/// <summary>A palette that has the name of the picture is next to it (hero.pal beside hero.frm).</summary>
		private static Boolean HasOwnPalette(DatArchive archive, DatEntry picture)
		{
			String ownName = Path.Combine(picture.Directory, Path.GetFileNameWithoutExtension(picture.Name));
			return archive.Find(ownName + ".pal") is { IsDamaged: false };
		}

		/// <summary>A text of the endings from the archives (this one, then the others that are open), or the sample when they have none.</summary>
		private String PickEndingText(DatArchive archive, Boolean death)
		{
			IReadOnlyList<DatArchive> archives = GameArchives(archive);
			try
			{
				return EndingImages.Pick(archives, Loc.Language, death, Random.Shared) ?? EndingImages.SampleText;
			}
			catch (Exception exception) when (exception is DatException or IOException or InvalidDataException)
			{
				return EndingImages.SampleText;
			}
		}

		/// <summary>
		/// The palettes a sprite can be shown with: the palette that has the name of the picture (hero.pal for hero.frm)
		/// when there is one, else the palette of the game (color.pal) of the archive or, when it has none, the one the program holds, a grayscale,
		/// and every palette file (.pal, .act) of the archive.
		/// </summary>
		private static List<PaletteChoice> ReadPalettes(DatArchive archive, DatEntry picture)
		{
			List<PaletteChoice> choices = [];
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
			List<PaletteChoice> result = [new PaletteChoice(Loc.Format("Str.PaletteAuto", preferred.Display), preferred.Palette)];
			if (!ReferenceEquals(preferred, builtIn))
				result.Add(builtIn);

			result.Add(grayscale);
			result.AddRange(choices);
			return result;
		}
	}
}