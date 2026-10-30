namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Models.Interface;
	using Texts;

	/// <summary>
	/// Which files the program can show, and how: the way to show a file depends on its extension.
	/// </summary>
	public static class FileViewers
	{
		private static readonly Dictionary<String, FileViewerKind> Kinds = Build();

		private static IReadOnlyDictionary<String, ViewerReader> _associations = new Dictionary<String, ViewerReader>();

		/// <summary>The extensions (lower case, with the dot) that the user gave a reader to, with that reader.</summary>
		public static IReadOnlyDictionary<String, ViewerReader> Associations => _associations;

		/// <summary>Sets the readers that the user chose for extensions; they come before what the program knows.</summary>
		public static void SetAssociations(IEnumerable<KeyValuePair<String, ViewerReader>>? associations)
		{
			_associations = (associations ?? []).ToDictionary(pair => Normalize(pair.Key), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>An extension as it is kept: lower case, with a dot; empty when there is none.</summary>
		public static String Normalize(String? extension)
		{
			String text = (extension ?? String.Empty).Trim().ToLowerInvariant();
			return text.Length == 0 ? text : text.StartsWith('.') ? text : "." + text;
		}

		/// <summary>The kind of viewer a reader belongs to.</summary>
		public static FileViewerKind KindOf(ViewerReader reader) => reader switch
		{
			ViewerReader.Image => FileViewerKind.Image,
			ViewerReader.Audio => FileViewerKind.Audio,
			ViewerReader.Table or ViewerReader.Dialog => FileViewerKind.Table,
			_ => FileViewerKind.Text,
		};

		/// <summary>
		/// The extension that decides how a file is read: the one of its reader when the user chose one, else its own. A reader
		/// stands for the extension that is read that way by default (JSON for .json...).
		/// </summary>
		public static String EffectiveExtension(String fileName)
		{
			String extension = Path.GetExtension(fileName);
			return _associations.TryGetValue(extension, out ViewerReader reader) ? CanonicalExtension(reader) : extension;
		}

		/// <summary>The extension whose way of reading a reader stands for.</summary>
		public static String CanonicalExtension(ViewerReader reader) => reader switch
		{
			ViewerReader.Ini => ".ini",
			ViewerReader.Toml => ".toml",
			ViewerReader.Yaml => ".yaml",
			ViewerReader.Json => ".json",
			ViewerReader.Xml => ".xml",
			ViewerReader.Message => ".msg",
			ViewerReader.Script => ".ssl",
			ViewerReader.Biography => ".bio",
			ViewerReader.List => ".lst",
			ViewerReader.Lua => ".lua",
			ViewerReader.Image => ".png",
			ViewerReader.Audio => ".wav",
			ViewerReader.Table => ".mes",
			ViewerReader.Dialog => ".dlg",
			_ => ".log",
		};

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
			String extension = EffectiveExtension(fileName);
			return (kind == FileViewerKind.Text && !extension.Equals(".int", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".scr", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".ent", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".chr", StringComparison.OrdinalIgnoreCase) && !ArcanumFileText.Handles(extension)) || extension.Equals(".gam", StringComparison.OrdinalIgnoreCase) || extension.Equals(".sve", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>Every extension (with the dot, in lower case) that the program knows how to show, in order.</summary>
		public static IReadOnlyList<String> AllExtensions { get; } = Kinds.Keys.Select(extension => extension.ToLowerInvariant()).Order(StringComparer.Ordinal).ToList();

		public static Boolean IsViewable(String fileName)
		{
			return TryGetKind(fileName, out _);
		}

		/// <summary>Whether a file can be shown: the files of an unknown extension can when they are shown as a text.</summary>
		public static Boolean IsViewable(String fileName, Boolean unknownAsText)
		{
			return unknownAsText || IsViewable(fileName);
		}

		/// <summary>Finds how a file is shown; the files of an unknown extension are shown as a text when <paramref name="unknownAsText"/> says so.</summary>
		public static Boolean TryGetKind(String fileName, Boolean unknownAsText, out FileViewerKind kind)
		{
			if (TryGetKind(fileName, out kind))
				return true;

			kind = FileViewerKind.Text;
			return unknownAsText;
		}

		/// <summary>Finds how a file is shown, from its extension.</summary>
		public static Boolean TryGetKind(String fileName, out FileViewerKind kind)
		{
			String extension = Path.GetExtension(fileName);
			if (_associations.TryGetValue(extension, out ViewerReader reader))
			{
				kind = KindOf(reader);
				return true;
			}

			return Kinds.TryGetValue(extension, out kind);
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
			foreach (String extension in new[] { ".lst", ".txt", ".msg", ".bio", ".cfg", ".ini", ".log", ".ssl", ".int", ".scr", ".ent", ".chr", ".jmp", ".tfai", ".tfaf", ".gsi", ".tmf", ".tmi", ".lua", ".h", ".csv", ".xml", ".json", ".toml", ".yaml", ".yml", ".md", ".bat", ".cmd", ".ps1", ".vbs" })
			{
				kinds[extension] = FileViewerKind.Text;
			}

			// Pictures: animations (.frm, or .fr0 to .fr5 for one direction), splash screens and palettes of Fallout, the masks
			// of the world map (.msk), the art of Arcanum (.art), the pictures, tiles and sprites of Fallout Tactics (.zar, .til, .spr), the map of a mission of Fallout Tactics (.mis), the sectors of the maps of Arcanum (.sec), the areas of the town maps (.tma), and the usual picture files (.qoi and .ktx2 too).
			foreach (String extension in new[] { ".frm", ".fr0", ".fr1", ".fr2", ".fr3", ".fr4", ".fr5", ".rix", ".pal", ".act", ".art", ".png", ".bmp", ".jpg", ".jpeg", ".gif", ".aaf", ".fon", ".msk", ".zar", ".til", ".spr", ".mis", ".sec", ".qoi", ".ktx2", ".tma" })
			{
				kinds[extension] = FileViewerKind.Image;
			}

			// Movies, characters and prototypes of objects.
			kinds[".mve"] = FileViewerKind.Video;
			kinds[".gcd"] = FileViewerKind.Character;
			kinds[".lip"] = FileViewerKind.Character;
			kinds[".pro"] = FileViewerKind.Prototype;
			kinds[".mob"] = FileViewerKind.Prototype;
			kinds[".map"] = FileViewerKind.Map;

			// The messages and the dialogs of Arcanum, the variables of the game (.gam) and the subtitles of the movies (.sve), as tables.
			kinds[".mes"] = FileViewerKind.Table;
			kinds[".dlg"] = FileViewerKind.Table;
			kinds[".gam"] = FileViewerKind.Table;
			kinds[".sve"] = FileViewerKind.Table;

			// Sounds.
			foreach (String extension in new[] { ".acm", ".wav", ".mp3", ".snd" })
			{
				kinds[extension] = FileViewerKind.Audio;
			}

			return kinds;
		}
	}
}