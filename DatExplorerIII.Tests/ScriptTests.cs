namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models.Archives;
	using DatExplorerIII.Models.Comparison;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Scripts;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Syntax;
	using DatExplorerIII.Services.Texts;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>The decompiler of the compiled scripts (.int), the colors of the sources (.ssl), and how the viewer shows them.</summary>
	public sealed class ScriptTests : IDisposable
	{
		/// <summary>A compiled script of Fallout 2 (208 bytes): <c>if (script_action == 6) then load_map(33, 0);</c>.</summary>
		private static readonly Byte[] LoadMapScript = Convert.FromHexString(
			"8002c00100000012800dc0010000008480048010801a8020801a8021801a8022801a8023802480258026000000020000000600000000000000000000000000000096000000000000001800000000000000000000000000000096000000000000001a00102e2e2e2e2e2e2e2e2e2e2e2e2e2e00000006737461727400ffffffffffffffff"
			+ "802cc001000000008003c001000000968004802bc001000000b880c7c001000000068033802fc00100000021c0010000000080e4c00100000000800d8019802a8029800c801c802a8029801c");

		private readonly List<String> _folders = [];

		public void Dispose()
		{
			foreach (String folder in _folders)
			{
				try
				{
					Directory.Delete(folder, true);
				}
				catch (IOException)
				{ }
			}
		}

		private String Folder(params (String Directory, String Name, Byte[] Content)[] files)
		{
			String folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String full = Path.Combine(folder, directory, name);
				Directory.CreateDirectory(Path.GetDirectoryName(full)!);
				File.WriteAllBytes(full, content);
			}

			_folders.Add(folder);
			return folder;
		}

		#region Decompiler

		[Fact]
		public void Decompile_RebuildsTheSourceOfAScript()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(LoadMapScript);

			Assert.Empty(script.Messages);
			String[] lines = script.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
			Assert.Contains("procedure start;", lines);
			Assert.Contains("procedure start", lines);
			Assert.Contains("\tif (script_action == 6) then begin", lines);
			Assert.Contains("\t\tload_map(33, 0);", lines);
		}

		[Fact]
		public void Decompile_IndentsWithWhatTheOptionsSay()
		{
			DecompiledScript script = ScriptDecompiler.Decompile(LoadMapScript, new ScriptDecompilerOptions { Indent = "  " });

			Assert.Contains("\n  if (script_action == 6) then begin\n", script.Text.Replace("\r\n", "\n", StringComparison.Ordinal));
		}

		[Fact]
		public void Decompile_TheSameScriptTwice_GivesTheSameSource()
		{
			Assert.Equal(ScriptDecompiler.Decompile(LoadMapScript).Text, ScriptDecompiler.Decompile((Byte[])LoadMapScript.Clone(), new ScriptDecompilerOptions { Game = ScriptGame.Fallout1 }).Text);
			Assert.Equal(ScriptDecompiler.Decompile(LoadMapScript).Text, ScriptDecompiler.DecompileLeniently(LoadMapScript).Text);
		}

		[Fact]
		public void Decompile_WhatIsNotAScript_IsRefused()
		{
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile([]));
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile(Encoding.ASCII.GetBytes("procedure start begin end")));
			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.DecompileLeniently(new Byte[200]));
		}

		[Fact]
		public void Decompile_ADamagedScript_FailsOnlyWithTheExceptionOfTheFormat()
		{
			Random random = new(2024);
			for (Int32 round = 0; round < 600; round++)
			{
				Byte[] data = (Byte[])LoadMapScript.Clone();
				if (round % 3 == 0)
				{
					data = data[..random.Next(data.Length)];
				}
				else
				{
					for (Int32 index = 0; index < 1 + random.Next(4); index++)
					{
						data[random.Next(data.Length)] = (Byte)random.Next(256);
					}
				}

				try
				{
					ScriptDecompiler.Decompile(data);
				}
				catch (ScriptFormatException)
				{ }
			}
		}

		[Fact]
		public void Decompile_ANumberOfArgumentsThatIsImpossible_IsRefusedAtOnce()
		{
			// The line of the first procedure in the table ends with its number of arguments: the byte after its body offset (0x96).
			Byte[] data = (Byte[])LoadMapScript.Clone();
			Int32 body = 0x30 + data.AsSpan(0x30).IndexOf(new Byte[] { 0, 0, 0, 0x96 });
			Assert.True(body > 0x30);
			data[body + 4] = 0x7F;

			Assert.Throws<ScriptFormatException>(() => ScriptDecompiler.Decompile(data));
		}

		[Fact]
		public void Decompile_CanBeGivenUp_ByTheCaller()
		{
			using CancellationTokenSource cancellation = new();
			cancellation.Cancel();

			Assert.ThrowsAny<OperationCanceledException>(() => ScriptDecompiler.Decompile(LoadMapScript, null, cancellation.Token));
			Assert.ThrowsAny<OperationCanceledException>(() => ScriptDecompiler.DecompileLeniently(LoadMapScript, null, cancellation.Token));
		}

		[Fact]
		public void Decompile_ThatTakesTooLong_IsGivenUp_AsATimeOutOfTheFormat()
		{
			// A limit that is over as soon as it starts: the work ends with the source or, most often, with the time out, and with no other error.
			ScriptDecompilerOptions options = new() { Timeout = TimeSpan.FromTicks(1) };
			for (Int32 round = 0; round < 40; round++)
			{
				try
				{
					Assert.NotEmpty(ScriptDecompiler.Decompile(LoadMapScript, options).Text);
				}
				catch (ScriptFormatException exception)
				{
					Assert.True(exception.TimedOut);
				}
			}

			// No limit: always the source.
			Assert.NotEmpty(ScriptDecompiler.Decompile(LoadMapScript, new ScriptDecompilerOptions { Timeout = TimeSpan.Zero }).Text);
		}

		#endregion

		#region Colors

		private static String Words(String text, SyntaxKind kind)
		{
			return String.Join("|", SslSyntax.Tokenize(text).Where(span => span.Kind == kind).Select(span => text.Substring(span.Start, span.Length)));
		}

		[Fact]
		public void Ssl_TheKeywordsTheFunctionsTheNumbersAndTheStringsHaveTheirColors()
		{
			const String text = "procedure start begin\n\tif (script_action == 0x1F) then begin\n\t\tfloat_msg(self_obj, \"It says \\\"hi\\\"\", 2.5);\n\tend\nend";

			Assert.Equal("procedure|begin|if|then|begin|end|end", Words(text, SyntaxKind.Keyword));
			Assert.Equal("self_obj", Words(text, SyntaxKind.Function).Split('|').Last());
			Assert.Contains("float_msg", Words(text, SyntaxKind.Function).Split('|'));
			Assert.Equal("0x1F|2.5", Words(text, SyntaxKind.Number));
			Assert.Equal("\"It says \\\"hi\\\"\"", Words(text, SyntaxKind.String));
		}

		[Fact]
		public void Ssl_KeywordsDoNotCareAboutCase_AndAWordThatContainsOneIsNotOne()
		{
			const String text = "BEGIN Begin variable endless begin_x";

			Assert.Equal("BEGIN|Begin|variable", Words(text, SyntaxKind.Keyword));
		}

		[Fact]
		public void Ssl_CommentsRunToTheEndOfTheLine_OrBetweenTheMarks_OverSeveralLines()
		{
			const String text = "a // begin end\nb /* if\nthen */ c /* never closed\nbegin";

			Assert.Equal("// begin end|/* if\nthen */|/* never closed\nbegin", Words(text, SyntaxKind.Comment));
			Assert.Empty(Words(text, SyntaxKind.Keyword));
		}

		[Fact]
		public void Ssl_ALineOfThePreprocessorStartsWithItsWord()
		{
			const String text = "#define MAP_ID 12\n  #include \"..\\headers\\define.h\"\nx = a # b";

			Assert.Equal("#define|#include", Words(text, SyntaxKind.Directive));
			Assert.Equal("12", Words(text, SyntaxKind.Number));
			Assert.Equal("\"..\\headers\\define.h\"", Words(text, SyntaxKind.String));
		}

		[Fact]
		public void Ssl_AStringThatIsNotClosedStopsAtTheEndOfTheLine()
		{
			const String text = "say(\"never closed\nbegin";

			Assert.Equal("\"never closed", Words(text, SyntaxKind.String));
			Assert.Equal("begin", Words(text, SyntaxKind.Keyword));
		}

		[Theory]
		[InlineData("")]
		[InlineData("x")]
		[InlineData("\"")]
		[InlineData("/")]
		[InlineData("/*")]
		[InlineData("0x")]
		[InlineData("1.")]
		[InlineData("#")]
		[InlineData("a\r\nb\rc\n\n")]
		[InlineData("procedure p begin // c\r\n\tvariable v := \"s\\\r\nend")]
		public void Ssl_ThePartsCoverTheWholeText_WithNoGapAndNoOverlap(String text)
		{
			Int32 position = 0;
			foreach (SyntaxSpan span in SslSyntax.Tokenize(text))
			{
				Assert.Equal(position, span.Start);
				Assert.True(span.Length > 0);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
		}

		[Fact]
		public void Ssl_TheViewerColorsScriptsAndHeaders_ButNotOtherTexts()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] source = Encoding.ASCII.GetBytes("procedure start begin end");

			viewer.Show(source, source.Length, ".ssl");
			Assert.True(viewer.IsHighlighted);
			viewer.Show(source, source.Length, ".H");
			Assert.True(viewer.IsHighlighted);
			viewer.Show(source, source.Length, ".txt");
			Assert.False(viewer.IsHighlighted);
			viewer.Show(Encoding.ASCII.GetBytes("{100}{}{Hello}"), 14, ".msg");
			Assert.True(viewer.IsHighlighted);
		}

		#endregion

		#region Viewer and comparison

		[Fact]
		public void FileViewers_ACompiledScriptIsShownAsText_ButIsNotText()
		{
			Assert.True(FileViewers.TryGetKind("scripts\\ABEL.INT", out FileViewerKind kind));
			Assert.Equal(FileViewerKind.Text, kind);
			Assert.True(FileViewers.TryGetKind("abel.ssl", out kind));
			Assert.Equal(FileViewerKind.Text, kind);

			Assert.True(FileViewers.IsPlainText("abel.ssl"));
			Assert.False(FileViewers.IsPlainText("abel.int"));
			Assert.False(TextContentComparer.IsText("abel.int"));
		}

		[Fact]
		public async Task Viewer_ShowsACompiledScriptAsItsSource_WithColors()
		{
			DatArchive archive = DatArchive.OpenFolder(Folder(("scripts", "cath2mv.int", LoadMapScript), ("scripts", "broken.int", [1, 2, 3, 4])));
			using (archive)
			using (ViewerViewModel viewer = new(new ManualUiThread()))
			{
				await viewer.LoadAsync(archive, archive.Find("scripts\\cath2mv.int")!);

				Assert.True(viewer.IsText);
				Assert.False(viewer.HasMessage);
				Assert.Contains("if (script_action == 6) then begin", viewer.Text.Text, StringComparison.Ordinal);
				Assert.True(viewer.Text.IsHighlighted);
				Assert.Contains(viewer.Text.SyntaxSpans!, span => span.Kind == SyntaxKind.Keyword);
				Assert.Contains("Str.ViewerDecompiled", viewer.Text.Notice, StringComparison.Ordinal);

				// A script that cannot be followed is said so, not shown as garbage.
				await viewer.LoadAsync(archive, archive.Find("scripts\\broken.int")!);
				Assert.False(viewer.IsText);
				Assert.True(viewer.HasMessage);
			}
		}

		/// <summary>The script with another number in its test: <c>script_action == 7</c> instead of 6.</summary>
		private static Byte[] OtherNumberScript()
		{
			Byte[] data = (Byte[])LoadMapScript.Clone();
			Int32 at = data.AsSpan().IndexOf(new Byte[] { 0xC0, 0x01, 0, 0, 0, 6 });
			Assert.True(at > 0);
			data[at + 5] = 7;
			return data;
		}

		[Fact]
		public void Comparison_TwoCompiledScripts_AreTheSameWhenTheirSourcesAre_ForWhatTheOptionsIgnore()
		{
			DatArchive left = DatArchive.OpenFolder(Folder(("scripts", "a.int", LoadMapScript), ("scripts", "b.int", LoadMapScript), ("scripts", "c.int", LoadMapScript)));
			DatArchive right = DatArchive.OpenFolder(Folder(("scripts", "a.int", LoadMapScript), ("scripts", "b.int", OtherNumberScript()), ("scripts", "c.int", [1, 2, 3])));
			using (left)
			using (right)
			{
				DatCompareState StateOf(IReadOnlyList<DatCompareItem> items, String name) => items.Single(item => item.Path.EndsWith(name, StringComparison.OrdinalIgnoreCase)).State;

				// Not ignoring anything: the same bytes are the same, another number is a difference, what is not a script too.
				IReadOnlyList<DatCompareItem> strict = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(new ComparisonOptions()));
				Assert.Equal(DatCompareState.Identical, StateOf(strict, "a.int"));
				Assert.Equal(DatCompareState.Different, StateOf(strict, "b.int"));
				Assert.Equal(DatCompareState.Different, StateOf(strict, "c.int"));

				// Ignoring the numbers, the sources of the two scripts are the same (the bytes are not).
				IReadOnlyList<DatCompareItem> numbers = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(new ComparisonOptions { IgnoreNumbers = true }));
				Assert.Equal(DatCompareState.Identical, StateOf(numbers, "b.int"));
				Assert.Equal(DatCompareState.Different, StateOf(numbers, "c.int"));

				// Byte for byte: the sources do not count.
				IReadOnlyList<DatCompareItem> binary = DatComparer.Compare(left, right, null, default, TextContentComparer.CreateEquality(new ComparisonOptions { Method = CompareMethod.Binary, IgnoreNumbers = true }));
				Assert.Equal(DatCompareState.Different, StateOf(binary, "b.int"));
			}

			Assert.True(TextContentComparer.AppliesTo("scripts\\abel.INT"));
			Assert.True(TextContentComparer.AppliesTo("readme.txt"));
			Assert.False(TextContentComparer.AppliesTo("proto\\00000001.pro"));
		}

		[Fact]
		public async Task Viewer_AFileChosenAfterAnother_KeepsThePlace_WhateverTheOrderTheWorkEnds()
		{
			DatArchive archive = DatArchive.OpenFolder(Folder(("scripts", "first.int", LoadMapScript), ("data", "second.txt", Encoding.ASCII.GetBytes("the second file")), ("scripts", "third.int", LoadMapScript)));
			using (archive)
			using (ViewerViewModel viewer = new(new ManualUiThread()))
			{
				DatEntry script = archive.Find("scripts\\first.int")!;
				DatEntry text = archive.Find("data\\second.txt")!;
				DatEntry other = archive.Find("scripts\\third.int")!;

				for (Int32 round = 0; round < 40; round++)
				{
					// The script is chosen, then at once the text (or another script): the viewer ends on the last one chosen.
					Task first = viewer.LoadAsync(archive, script);
					Task second = viewer.LoadAsync(archive, round % 2 == 0 ? text : other);
					await Task.WhenAll(first, second);

					Assert.True(viewer.IsText);
					Assert.Equal(round % 2 == 0 ? "second.txt" : "third.int", viewer.Title);
					Assert.Contains(round % 2 == 0 ? "the second file" : "load_map(33, 0)", viewer.Text.Text, StringComparison.Ordinal);
				}
			}
		}

		[Fact]
		public void FileTextView_ACompiledScriptIsComparedAsItsSource()
		{
			Assert.Equal(FileTextResult.Lines, FileTextView.TryGetLines("scripts\\cath2mv.int", LoadMapScript, null, out IReadOnlyList<String> lines));
			Assert.Contains("procedure start;", lines);
			Assert.Contains("\t\tload_map(33, 0);", lines);

			Assert.Equal(FileTextResult.NotText, FileTextView.TryGetLines("scripts\\broken.int", [1, 2, 3], null, out _));
		}

		#endregion

	}
}