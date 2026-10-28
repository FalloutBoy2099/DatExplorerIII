namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats.GameFiles;
	using Models.Archives;
	using Models.Interface;
	using Services.ArcanumScripts;
	using Services.Interface;
	using Services.Texts;
	using Scripts;

	/// <summary>The scripts: a compiled script of Fallout (.int) is shown as the source it decompiles to, one of Arcanum (.scr) as a text.</summary>
	public sealed partial class ViewerViewModel
	{
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
					note += Loc.Format("Str.ViewerDecompiledWarnings", script.Messages.Count);

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
		/// Shows a file of Arcanum made of a few fields as text: a jump point list (.jmp), the index (.tfai) and the data (.tfaf) of a saved
		/// game, the information of a saved game (.gsi), the fog of a town map (.tmf). The data of a saved game is listed with the help
		/// of the index that has its name, when the archive has it.
		/// </summary>
		private void ShowArcanumFile(DatArchive archive, DatEntry entry, Byte[] data)
		{
			String extension = Path.GetExtension(entry.Name);
			try
			{
				Byte[]? index = null;
				if (extension.Equals(".tfaf", StringComparison.OrdinalIgnoreCase) && archive.Find(Path.Combine(entry.Directory, Path.GetFileNameWithoutExtension(entry.Name) + ".tfai")) is { IsDamaged: false } indexEntry)
					index = archive.ReadData(indexEntry);

				Byte[] text = System.Text.Encoding.UTF8.GetBytes(ArcanumFileText.Render(extension, data, index));
				Text.Show(text, text.Length, ".txt", null);
				Show(FileViewerKind.Text, String.Empty);
			}
			catch (InvalidDataException exception)
			{
				Show(null, Loc.Format("Str.ViewerScriptError", exception.Message));
			}
		}

		/// <summary>Shows an entity of Fallout Tactics (.ent) as text: its class and its properties.</summary>
		private void ShowEnt(Byte[] data)
		{
			try
			{
				Byte[] text = System.Text.Encoding.UTF8.GetBytes(EntReader.ToText(EntReader.Read(data)));
				Text.Show(text, text.Length, ".txt", Loc.Get("Str.ViewerEnt"));
				Show(FileViewerKind.Text, String.Empty);
			}
			catch (InvalidDataException exception)
			{
				Show(null, Loc.Format("Str.ViewerScriptError", exception.Message));
			}
		}

		/// <summary>Shows a script of Arcanum (.scr) as text, with the colors of a source, like a decompiled script.</summary>
		private void ShowScr(DatArchive archive, Byte[] data)
		{
			try
			{
				// The scripts that a script calls are named after the files of the open archives (this one first).
				IReadOnlyList<DatArchive> archives = GameArchives(archive);
				Byte[] text = System.Text.Encoding.UTF8.GetBytes(ScrText.Render(data, ScrScriptNames.For(archives)));
				Text.Show(text, text.Length, ".ssl", Loc.Get("Str.ViewerScr"));
				Show(FileViewerKind.Text, String.Empty);
			}
			catch (ScrLib.ScrFormatException exception)
			{
				Show(null, Loc.Format("Str.ViewerScriptError", exception.Message));
			}
		}
	}
}