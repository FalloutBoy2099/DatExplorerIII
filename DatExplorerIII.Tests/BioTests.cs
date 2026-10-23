namespace DatExplorerIII.Tests
{
	using System;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>The biographies of the premade characters (.bio): what does not fit in the box of the game is marked.</summary>
	public sealed class BioTests
	{
		private static String Marked(String text)
		{
			return String.Join("|", BioSyntax.Tokenize(text).Where(span => span.Kind == SyntaxKind.Overflow).Select(span => text.Substring(span.Start, span.Length)));
		}

		/// <summary>The header (a blank line, the name, a blank line) then <paramref name="lines"/> lines of text.</summary>
		private static String Biography(Int32 lines, String line = "short line")
		{
			return "\r\nNARG :\r\n\r\n" + String.Concat(Enumerable.Range(1, lines).Select(index => line + index + "\r\n"));
		}

		[Fact]
		public void ABiographyThatFits_HasNothingMarked()
		{
			// 22 lines of text, each of 26 characters at most, and a header that is not counted (its name line may be long).
			String text = "\r\nTHE NAME OF A CHARACTER WHO HAS A VERY LONG NAME :\r\n\r\n" + String.Concat(Enumerable.Repeat("12345678901234567890123456\r\n", 22));

			Assert.Empty(Marked(text));
		}

		[Fact]
		public void TheLinesPastTheTwentySecond_AreMarked()
		{
			String text = Biography(24);

			Assert.Equal("short line23|short line24", Marked(text));
			Assert.DoesNotContain("short line22", Marked(text), StringComparison.Ordinal);
		}

		[Fact]
		public void TheCharactersPastTheTwentySixth_AreMarked()
		{
			// 26 characters are the last that fit: the 27th and the next ones are marked, not the line break.
			String text = "\r\nNARG :\r\n\r\nABCDEFGHIJKLMNOPQRSTUVWXYZ+-\r\nshort\r\n";

			Assert.Equal("+-", Marked(text));
		}

		[Fact]
		public void TheSpacesPastTheEndOfALine_AreMarkedToo()
		{
			String text = "\r\nNARG :\r\n\r\n" + "ABCDEFGHIJKLMNOPQRSTUVWXYZ" + "   \r\n";

			Assert.Equal("   ", Marked(text));
		}

		[Fact]
		public void TheHeader_IsNotMarked_WhateverItsLength()
		{
			String text = "A VERY LONG FIRST LINE THAT HAS MORE THAN TWENTY-SIX CHARACTERS\r\nTHE SECOND ONE IS THE NAME, AND IS ALSO LONG :\r\nA THIRD ONE THAT IS NOT BLANK BUT STILL THE HEADER\r\nfits\r\n";

			Assert.Empty(Marked(text));
		}

		[Theory]
		[InlineData("")]
		[InlineData("\r")]
		[InlineData("\n\n\n\n")]
		[InlineData("a\rb\nc\r\nd")]
		[InlineData("\r\n\r\n\r\n12345678901234567890123456789\r")]
		public void ThePartsCoverTheWholeText_WithNoGapAndNoOverlap(String text)
		{
			Int32 position = 0;
			foreach (SyntaxSpan span in BioSyntax.Tokenize(text))
			{
				Assert.Equal(position, span.Start);
				Assert.True(span.Length > 0);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);

			String many = Biography(40, "a line of more than twenty-six characters, ");
			position = 0;
			foreach (SyntaxSpan span in BioSyntax.Tokenize(many))
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(many.Length, position);
		}

		[Fact]
		public void TheViewer_ColorsBiographies_ButNotOtherTexts()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] bio = Encoding.ASCII.GetBytes(Biography(30));

			viewer.Show(bio, bio.Length, ".bio");
			Assert.True(viewer.IsHighlighted);
			Assert.Contains(viewer.SyntaxSpans!, span => span.Kind == SyntaxKind.Overflow);

			viewer.Show(bio, bio.Length, ".BIO");
			Assert.True(viewer.IsHighlighted);
			viewer.Show(bio, bio.Length, ".txt");
			Assert.False(viewer.IsHighlighted);
		}

		[Fact]
		public void TheTextBox_HoldsTheText_OnlyWhenItHasNoColors()
		{
			// The colored text is drawn by another box: the plain one is kept empty (a long text, even hidden, takes long to make).
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] bio = Encoding.ASCII.GetBytes(Biography(30));

			viewer.Show(bio, bio.Length, ".bio");
			Assert.Equal(String.Empty, viewer.PlainText);
			Assert.NotEqual(String.Empty, viewer.Text);

			viewer.Show(bio, bio.Length, ".txt");
			Assert.Equal(viewer.Text, viewer.PlainText);
		}
	}
}