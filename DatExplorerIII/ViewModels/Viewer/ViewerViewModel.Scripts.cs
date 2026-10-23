namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Core;
	using Formats;
	using Models;
	using Services;
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