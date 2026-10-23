namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Formats;
	using Models;
	using Scripts;
	using ViewModels;
	using ViewModels.Viewer;

	/// <summary>Whether a file can be written as lines of text to be compared.</summary>
	public enum FileTextResult
	{
		/// <summary>The file was written as lines.</summary>
		Lines,

		/// <summary>The file is not made of text (a picture, a sound, a movie, or a text that looks binary).</summary>
		NotText,

		/// <summary>The file is too large to be compared line by line.</summary>
		TooLarge,
	}

	/// <summary>
	/// Writes a file as lines of text, to compare two versions of it: a text file is its lines; a file that the program reads
	/// (a prototype, a character, a map, a message file, a dialog) is what the viewer shows of it, one line per field, so that
	/// the differences are those of the fields.
	/// </summary>
	public static class FileTextView
	{
		/// <summary>The largest file whose lines are compared.</summary>
		public const Int32 MaxBytes = 3 * 1024 * 1024;

		/// <param name="fileName">The name of the file: its extension tells what it is.</param>
		/// <param name="data">The content of the file.</param>
		/// <param name="resources">The files of the game that a map needs to be read; null when they are not at hand.</param>
		/// <param name="lines">The lines when the result is <see cref="FileTextResult.Lines"/>.</param>
		public static FileTextResult TryGetLines(String fileName, Byte[] data, MapResources? resources, out IReadOnlyList<String> lines)
		{
			return TryGetLines(fileName, data, resources, ScriptGame.Fallout2, out lines);
		}

		/// <param name="game">The game the file is for: a compiled script (.int) is decompiled for it.</param>
		public static FileTextResult TryGetLines(String fileName, Byte[] data, MapResources? resources, ScriptGame game, out IReadOnlyList<String> lines)
		{
			lines = [];
			if (!FileViewers.TryGetKind(fileName, out FileViewerKind kind) || kind is FileViewerKind.Image or FileViewerKind.Audio or FileViewerKind.Video)
			{
				return FileTextResult.NotText;
			}

			// The variables of the game and the subtitles are lines of text (the viewer shows them as a table): compared as they are written.
			if (FileViewers.IsPlainText(fileName))
			{
				kind = FileViewerKind.Text;
			}

			if (data.Length > MaxBytes)
			{
				return FileTextResult.TooLarge;
			}

			try
			{
				switch (kind)
				{
					case FileViewerKind.Text when Path.GetExtension(fileName).Equals(".int", StringComparison.OrdinalIgnoreCase):
						// A compiled script is compared as the source it decompiles to.
						lines = SplitLines(ScriptDecompiler.DecompileLeniently(data, new ScriptDecompilerOptions { Game = game }).Text);
						return FileTextResult.Lines;

					case FileViewerKind.Text when Path.GetExtension(fileName).Equals(".scr", StringComparison.OrdinalIgnoreCase):
						// A script of Arcanum is compared as the text it is shown as.
						lines = SplitLines(ScrText.Render(data));
						return FileTextResult.Lines;

					case FileViewerKind.Text:
						DecodedText decoded = TextDecoder.Decode(data);
						if (decoded.LooksBinary)
						{
							return FileTextResult.NotText;
						}

						lines = SplitLines(decoded.Text);
						return FileTextResult.Lines;

					case FileViewerKind.Prototype:
						lines = SheetLines(ArcanumProReader.IsArcanumPro(data) ? PrototypeViewerViewModel.Build(ArcanumProReader.Read(data)) : PrototypeViewerViewModel.Build(ProReader.Read(data)));
						return FileTextResult.Lines;

					case FileViewerKind.Character:
						lines = SheetLines(Path.GetExtension(fileName).Equals(".lip", StringComparison.OrdinalIgnoreCase) ? LipSheet.Build(LipReader.Read(data)) : CharacterViewerViewModel.Build(GcdReader.Read(data)));
						return FileTextResult.Lines;

					case FileViewerKind.Map:
						lines = SheetLines(MapViewerViewModel.Build(MapReader.Read(data, resources)));
						return FileTextResult.Lines;

					case FileViewerKind.Table:
						lines = Path.GetExtension(fileName).Equals(".dlg", StringComparison.OrdinalIgnoreCase) ? DialogLines(DlgReader.Read(data)) : MessageLines(MesReader.Read(data));
						return FileTextResult.Lines;

					default:
						return FileTextResult.NotText;
				}
			}
			catch (Exception exception) when (exception is InvalidDataException or ScriptFormatException)
			{
				// A file the program cannot read is not compared as a text.
				return FileTextResult.NotText;
			}
		}

		/// <summary>The lines of a text, whatever way its lines end; a last line break does not make one more line.</summary>
		internal static IReadOnlyList<String> SplitLines(String text)
		{
			List<String> lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').ToList();
			if (lines.Count > 1 && lines[^1].Length == 0)
			{
				lines.RemoveAt(lines.Count - 1);
			}

			return lines;
		}

		/// <summary>A sheet of the viewer: a line for the title of each section, then a line for each of its rows.</summary>
		private static IReadOnlyList<String> SheetLines(IReadOnlyList<CharacterSection> sections)
		{
			List<String> lines = new();
			foreach (CharacterSection section in sections)
			{
				lines.Add("[" + section.Title + "]");
				lines.AddRange(section.Rows.Select(row => row.Value.Length == 0 ? row.Label : row.Label + ": " + row.Value));
			}

			return lines;
		}

		private static IReadOnlyList<String> MessageLines(MesFile file)
		{
			return file.Entries.Select(entry => entry.Number + ": " + OneLine(entry.Text)).ToList();
		}

		private static IReadOnlyList<String> DialogLines(DlgFile file)
		{
			return file.Entries.Select(entry => String.Join(" | ", new[] { entry.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), OneLine(entry.Text), OneLine(entry.Text2), entry.Iq, entry.Test, entry.Goto, entry.Effect })).ToList();
		}

		/// <summary>A text that runs over several lines on one line (the line breaks are shown as a mark).</summary>
		private static String OneLine(String text)
		{
			return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\n", " \u00B6 ", StringComparison.Ordinal);
		}
	}
}