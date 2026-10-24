namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Models.Interface;

	/// <summary>
	/// Which files the program can show, and how: the way to show a file depends on its extension.
	/// </summary>
	public static class FileViewers
	{
		private static readonly Dictionary<String, FileViewerKind> Kinds = Build();

		/// <summary>The extensions (with the dot) that are shown as a text.</summary>
		public static IEnumerable<String> TextExtensions => ExtensionsOf(FileViewerKind.Text);

		/// <summary>The extensions (with the dot) that are shown as a picture.</summary>
		public static IEnumerable<String> ImageExtensions => ExtensionsOf(FileViewerKind.Image);

		/// <summary>The extensions (with the dot) that are played.</summary>
		public static IEnumerable<String> AudioExtensions => ExtensionsOf(FileViewerKind.Audio);

		/// <summary>
		/// Whether a file is made of text that is read as text to compare it: the texts, and the files that the viewer shows as a
		/// table but that are lines of text (the variables of the game, the subtitles).
		/// </summary>
		public static Boolean IsPlainText(String fileName)
		{
			if (!TryGetKind(fileName, out FileViewerKind kind))
				return false;

			// A compiled script (.int, .scr) is shown as the source it decompiles to, but what it holds is not text.
			String extension = Path.GetExtension(fileName);
			return (kind == FileViewerKind.Text && !extension.Equals(".int", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".scr", StringComparison.OrdinalIgnoreCase)) || extension.Equals(".gam", StringComparison.OrdinalIgnoreCase) || extension.Equals(".sve", StringComparison.OrdinalIgnoreCase);
		}

		public static Boolean IsViewable(String fileName)
		{
			return TryGetKind(fileName, out _);
		}

		/// <summary>Finds how a file is shown, from its extension.</summary>
		public static Boolean TryGetKind(String fileName, out FileViewerKind kind)
		{
			return Kinds.TryGetValue(Path.GetExtension(fileName), out kind);
		}

		private static IEnumerable<String> ExtensionsOf(FileViewerKind kind)
		{
			foreach (KeyValuePair<String, FileViewerKind> pair in Kinds)
			{
				if (pair.Value == kind)
					yield return pair.Key;
			}
		}

		private static Dictionary<String, FileViewerKind> Build()
		{
			Dictionary<String, FileViewerKind> kinds = new(StringComparer.OrdinalIgnoreCase);

			// Texts: lists, dialogs, game variables, scripts and configuration files.
			foreach (String extension in new[] { ".lst", ".txt", ".msg", ".bio", ".cfg", ".ini", ".log", ".ssl", ".int", ".scr", ".lua", ".h", ".csv", ".xml", ".json", ".md", ".bat", ".cmd", ".ps1", ".vbs" })
			{
				kinds[extension] = FileViewerKind.Text;
			}

			// Pictures: animations (.frm, or .fr0 to .fr5 for one direction), splash screens and palettes of Fallout, the masks
			// of the world map (.msk), the art of Arcanum (.art), and the usual picture files.
			foreach (String extension in new[] { ".frm", ".fr0", ".fr1", ".fr2", ".fr3", ".fr4", ".fr5", ".rix", ".pal", ".act", ".art", ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".aaf", ".fon", ".msk" })
			{
				kinds[extension] = FileViewerKind.Image;
			}

			// Movies, characters and prototypes of objects.
			kinds[".mve"] = FileViewerKind.Video;
			kinds[".gcd"] = FileViewerKind.Character;
			kinds[".lip"] = FileViewerKind.Character;
			kinds[".pro"] = FileViewerKind.Prototype;
			kinds[".map"] = FileViewerKind.Map;

			// The messages and the dialogs of Arcanum, the variables of the game (.gam) and the subtitles of the movies (.sve), as tables.
			kinds[".mes"] = FileViewerKind.Table;
			kinds[".dlg"] = FileViewerKind.Table;
			kinds[".gam"] = FileViewerKind.Table;
			kinds[".sve"] = FileViewerKind.Table;

			// Sounds.
			foreach (String extension in new[] { ".acm", ".wav", ".mp3" })
			{
				kinds[extension] = FileViewerKind.Audio;
			}

			return kinds;
		}
	}
}