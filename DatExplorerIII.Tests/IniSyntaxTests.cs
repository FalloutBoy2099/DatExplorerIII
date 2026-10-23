namespace DatExplorerIII.Tests
{
	using System;
	using System.Linq;
	using DatExplorerIII.Services.Syntax;
	using Xunit;

	public class IniSyntaxTests
	{
		private const String Sample = "; comment\r\n[Main]\r\nWidth = 640\r\nTitle=Hello ; trailing\r\n\r\n[Other]\r\nKey=0x1F\r\n";

		[Fact]
		public void ASectionAndItsKeys_AreTakenForAnIniText()
		{
			Assert.True(IniSyntax.LooksLikeIni(Sample));
		}

		[Fact]
		public void ProseAndMessageText_AreNotTakenForAnIniText()
		{
			Assert.False(IniSyntax.LooksLikeIni("Hello there.\r\nThis is a story.\r\nThe end = maybe\r\nNothing more.\r\n"));
			Assert.False(IniSyntax.LooksLikeIni(String.Empty));
		}

		[Fact]
		public void Tokenize_CoversTheWholeText_WithNoGapAndNoOverlap()
		{
			var spans = IniSyntax.Tokenize(Sample);
			Int32 position = 0;
			foreach (SyntaxSpan span in spans)
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(Sample.Length, position);
		}

		[Fact]
		public void Tokenize_ColorsSectionKeyValueAndComment()
		{
			var spans = IniSyntax.Tokenize(Sample);
			String Of(SyntaxKind kind) => String.Concat(spans.Where(span => span.Kind == kind).Select(span => Sample.Substring(span.Start, span.Length) + "|"));

			Assert.Equal("Main|Other|", Of(SyntaxKind.Keyword));
			Assert.Equal("Width|Title|Key|", Of(SyntaxKind.Identifier));
			Assert.Equal("640|0x1F|", Of(SyntaxKind.Number));
			Assert.Equal("Hello|", Of(SyntaxKind.String));
			Assert.Equal("; comment|; trailing|", Of(SyntaxKind.Comment));
		}

		[Fact]
		public void HashComments_AreTheLinesThatStartWithAHash_OfATextThatIsNotAnIni()
		{
			const String text = "# header\r\nA story.\r\n  # indented\r\nItem #3 is here.\r\n";

			Assert.True(HashCommentSyntax.HasComments(text));
			Assert.False(HashCommentSyntax.HasComments("Item #3 only.\n"));

			var spans = HashCommentSyntax.Tokenize(text);
			Int32 position = 0;
			foreach (SyntaxSpan span in spans)
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
			Assert.Equal("# header|# indented|", String.Concat(spans.Where(span => span.Kind == SyntaxKind.Comment).Select(span => text.Substring(span.Start, span.Length) + "|")));
		}
	}
}