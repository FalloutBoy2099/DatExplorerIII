namespace DatExplorerIII.Tests
{
	using System;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;

	/// <summary>The list files (.lst): the comments, from the semicolon to the end of the line, have their color.</summary>
	public sealed class LstTests
	{
		private static String Comments(String text)
		{
			return String.Join("|", LstSyntax.Tokenize(text).Where(span => span.Kind == SyntaxKind.Comment).Select(span => text.Substring(span.Start, span.Length)));
		}

		[Fact]
		public void TheComment_StartsAtTheSemicolonItIncluded_AndRunsToTheEndOfTheLine()
		{
			const String text = "STRENGTH.FRM    ; basic stat\r\nPERCEPTION.FRM;perception\r\nENDURANCE.FRM\r\n";

			Assert.Equal("; basic stat|;perception", Comments(text));
		}

		[Fact]
		public void ALineWithoutSemicolon_HasNoComment_AndASemicolonInAComment_IsPartOfIt()
		{
			Assert.Empty(Comments("a.frm\nb.frm\n"));
			Assert.Equal("; one ; two # three", Comments("x ; one ; two # three\n"));
		}

		[Fact]
		public void AWholeLineThatIsAComment_IsOne()
		{
			Assert.Equal("; nothing but a comment", Comments("; nothing but a comment\nfile.frm"));
		}

		[Theory]
		[InlineData("")]
		[InlineData(";")]
		[InlineData("\r\n\r\n")]
		[InlineData("a;b\rc;d\ne")]
		[InlineData("name ;\r\n")]
		public void ThePartsCoverTheWholeText_WithNoGapAndNoOverlap(String text)
		{
			Int32 position = 0;
			foreach (SyntaxSpan span in LstSyntax.Tokenize(text))
			{
				Assert.Equal(position, span.Start);
				Assert.True(span.Length > 0);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
		}

		[Fact]
		public void TheViewer_DoesNotColorATextWithTooManyParts_AndSaysSo()
		{
			// Every line has a name and a comment (the line break joins the name of the next line): two parts.
			Byte[] big = Encoding.ASCII.GetBytes(String.Concat(Enumerable.Repeat("a.frm ; c\r\n", (TextViewerViewModel.MaxColoredParts / 2) + 10)));
			TextViewerViewModel viewer = new();

			viewer.Show(big, big.Length, ".lst");

			Assert.False(viewer.IsHighlighted);
			Assert.Equal("Str.ViewerNotColored", viewer.Notice);

			// A text with fewer parts is colored, and has no such remark.
			Byte[] small = Encoding.ASCII.GetBytes("a.frm ; c\r\n");
			viewer.Show(small, small.Length, ".lst");
			Assert.True(viewer.IsHighlighted);
			Assert.Empty(viewer.Notice);
		}

		[Fact]
		public void TheViewer_ColorsLists_ButNotOtherTexts()
		{
			TextViewerViewModel viewer = new();
			Byte[] list = Encoding.ASCII.GetBytes("ART.FRM ; art\r\n");

			viewer.Show(list, list.Length, ".lst");
			Assert.True(viewer.IsHighlighted);
			Assert.Contains(viewer.SyntaxSpans!, span => span.Kind == SyntaxKind.Comment);
			viewer.Show(list, list.Length, ".LST");
			Assert.True(viewer.IsHighlighted);
			viewer.Show(list, list.Length, ".txt");
			Assert.False(viewer.IsHighlighted);
		}
	}
}