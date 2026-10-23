namespace DatExplorerIII.Tests
{
	using System;
	using System.Linq;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Syntax;
	using DatExplorerIII.Models.Interface;

	/// <summary>The coloring of Lua scripts.</summary>
	public sealed class LuaSyntaxTests
	{
		private const String Script = "#!/usr/bin/lua\n-- a comment\nlocal x = 0x1F + 3.5e-2 -- trailing\nfunction greet(name)\n  print(\"hi \\\"there\\\"\", 'it\\'s')\n  return [==[long\n]] text]==] .. name\nend\n--[[ block\ncomment ]] y = nil\n";

		private static String Of(SyntaxKind kind)
		{
			return String.Concat(LuaSyntax.Tokenize(Script).Where(span => span.Kind == kind).Select(span => Script.Substring(span.Start, span.Length) + "|"));
		}

		[Fact]
		public void TheParts_CoverTheWholeText_WithNoGapAndNoOverlap()
		{
			Int32 position = 0;
			foreach (SyntaxSpan span in LuaSyntax.Tokenize(Script))
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(Script.Length, position);
		}

		[Fact]
		public void Comments_AreTheLineOfTheInterpreter_TheLinesAfterTwoDashes_AndTheLongOnes()
		{
			Assert.Equal("#!/usr/bin/lua|-- a comment|-- trailing|--[[ block\ncomment ]]|", Of(SyntaxKind.Comment));
		}

		[Fact]
		public void Keywords_Numbers_Strings_AndFunctions_AreFound()
		{
			Assert.Equal("local|function|return|end|nil|", Of(SyntaxKind.Keyword));
			Assert.Equal("0x1F|3.5e-2|", Of(SyntaxKind.Number));
			Assert.Equal("\"hi \\\"there\\\"\"|'it\\'s'|[==[long\n]] text]==]|", Of(SyntaxKind.String));
			Assert.Equal("greet|print|", Of(SyntaxKind.Function));
		}

		[Fact]
		public void LuaFiles_AreTextFilesOfTheViewer_AndNotBinary()
		{
			Assert.True(FileViewers.TryGetKind("main.LUA", out DatExplorerIII.Models.Interface.FileViewerKind kind));
			Assert.Equal(DatExplorerIII.Models.Interface.FileViewerKind.Text, kind);
			Assert.True(FileViewers.IsPlainText("main.lua"));
		}
	}
}