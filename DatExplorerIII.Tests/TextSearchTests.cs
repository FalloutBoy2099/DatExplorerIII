namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using DatExplorerIII.ViewModels;

	/// <summary>The search of a text in the viewer: find, find next, go to a line, go to a message by its number.</summary>
	public sealed class TextSearchTests
	{
		private sealed class Answers
		{
			public Queue<String?> Values { get; } = new();

			public List<Func<String, String?>?> Validators { get; } = new();

			public List<String> Labels { get; } = new();

			public List<String> Informed { get; } = new();

			public String? Ask(String title, String label, String initial, Func<String, String?>? validate)
			{
				Labels.Add(label);
				Validators.Add(validate);
				return Values.Count > 0 ? Values.Dequeue() : null;
			}
		}

		private static TextViewerViewModel Viewer(String text, String extension, Answers answers)
		{
			TextViewerViewModel viewer = new(new ManualUiThread()) { Ask = answers.Ask, Inform = answers.Informed.Add };
			Byte[] data = Encoding.ASCII.GetBytes(text);
			viewer.Show(data, data.Length, extension);
			return viewer;
		}

		[Fact]
		public void WithoutAWayToAsk_OrWithoutText_NothingCanBeSearched()
		{
			TextViewerViewModel viewer = new(new ManualUiThread());
			Byte[] data = Encoding.ASCII.GetBytes("some text");
			viewer.Show(data, data.Length, ".txt");

			Assert.False(viewer.FindCommand.CanExecute(null));
			Assert.False(viewer.GoToLineCommand.CanExecute(null));

			viewer.Ask = new Answers().Ask;
			Assert.True(viewer.FindCommand.CanExecute(null));
			Assert.True(viewer.GoToLineCommand.CanExecute(null));
			Assert.False(viewer.FindNextCommand.CanExecute(null));
			Assert.False(viewer.GoToIdCommand.CanExecute(null));

			viewer.Clear();
			Assert.False(viewer.FindCommand.CanExecute(null));
		}

		[Fact]
		public void Find_SelectsTheTextWithoutCaringAboutTheCase_AndFindNextGoesOnAndStartsAgain()
		{
			Answers answers = new();
			TextViewerViewModel viewer = Viewer("Hello world\r\nsecond WORLD here", ".txt", answers);
			answers.Values.Enqueue("world");

			viewer.FindCommand.Execute(null);
			Assert.Equal(new TextReveal(6, 5, 1), viewer.Reveal);

			Assert.True(viewer.FindNextCommand.CanExecute(null));
			viewer.FindNextCommand.Execute(null);
			Assert.Equal(new TextReveal(20, 5, 2), viewer.Reveal);

			// After the last one, from the top again.
			viewer.FindNextCommand.Execute(null);
			Assert.Equal(new TextReveal(6, 5, 3), viewer.Reveal);
			Assert.Empty(answers.Informed);
		}

		[Fact]
		public void AText_ThatIsNotThere_IsSaidSo_AndNothingMoves()
		{
			Answers answers = new();
			TextViewerViewModel viewer = Viewer("Hello world", ".txt", answers);
			answers.Values.Enqueue("zzz");

			viewer.FindCommand.Execute(null);

			Assert.Equal(["Str.TextNotFound"], answers.Informed);
			Assert.Null(viewer.Reveal);

			// An answer that is empty, or a window that is closed, does nothing.
			answers.Values.Enqueue(String.Empty);
			viewer.FindCommand.Execute(null);
			answers.Values.Enqueue(null);
			viewer.FindCommand.Execute(null);
			Assert.Single(answers.Informed);
			Assert.Null(viewer.Reveal);
		}

		[Fact]
		public void ANewText_StartsTheSearchAgainFromTheTop()
		{
			Answers answers = new();
			TextViewerViewModel viewer = Viewer("a b a b", ".txt", answers);
			answers.Values.Enqueue("b");
			viewer.FindCommand.Execute(null);
			viewer.FindNextCommand.Execute(null);
			Assert.Equal(6, viewer.Reveal!.Start);

			Byte[] other = Encoding.ASCII.GetBytes("b b");
			viewer.Show(other, other.Length, ".txt");
			viewer.FindNextCommand.Execute(null);

			Assert.Equal(0, viewer.Reveal!.Start);
		}

		[Fact]
		public void GoToLine_SelectsTheLine_WithoutItsLineBreak_AndOnlyAcceptsALineThatIsThere()
		{
			Answers answers = new();
			TextViewerViewModel viewer = Viewer("a\r\nbb\r\nccc", ".txt", answers);
			answers.Values.Enqueue("2");

			viewer.GoToLineCommand.Execute(null);

			Assert.Equal(new TextReveal(3, 2, 1), viewer.Reveal);
			Func<String, String?> validate = answers.Validators[0]!;
			Assert.Null(validate("1"));
			Assert.Null(validate("3"));
			Assert.NotNull(validate("0"));
			Assert.NotNull(validate("4"));
			Assert.NotNull(validate("x"));
			Assert.NotNull(validate("-1"));
			Assert.NotNull(validate(String.Empty));

			answers.Values.Enqueue("3");
			viewer.GoToLineCommand.Execute(null);
			Assert.Equal(new TextReveal(7, 3, 2), viewer.Reveal);

			answers.Values.Enqueue("1");
			viewer.GoToLineCommand.Execute(null);
			Assert.Equal(new TextReveal(0, 1, 3), viewer.Reveal);
		}

		[Fact]
		public void GoToId_FindsTheMessageThatHasTheNumber_EvenWithLeadingZeros_ButNotInAComment()
		{
			Answers answers = new();
			const String text = "# {100}{}{a comment}\r\n{100}{}{first}\r\n  {0105}{}{second}\r\n{1050}{}{other}\r\n";
			TextViewerViewModel viewer = Viewer(text, ".msg", answers);

			Assert.True(viewer.IsMessageFile);
			Assert.True(viewer.GoToIdCommand.CanExecute(null));

			answers.Values.Enqueue("100");
			viewer.GoToIdCommand.Execute(null);
			Assert.Equal(text.IndexOf("{100}{}{first}", StringComparison.Ordinal), viewer.Reveal!.Start);
			Assert.Equal(5, viewer.Reveal.Length);

			answers.Values.Enqueue("105");
			viewer.GoToIdCommand.Execute(null);
			Assert.Equal(text.IndexOf("{0105}", StringComparison.Ordinal), viewer.Reveal!.Start);
			Assert.Equal(6, viewer.Reveal.Length);

			// 10 is not the start of 105 or of 1050.
			answers.Values.Enqueue("10");
			viewer.GoToIdCommand.Execute(null);
			Assert.Equal(["Str.TextIdNotFound"], answers.Informed);

			Func<String, String?> validate = answers.Validators[0]!;
			Assert.Null(validate("12"));
			Assert.NotNull(validate("abc"));
			Assert.NotNull(validate("-4"));
		}

		[Fact]
		public void GoToId_IsOnlyForAMessageFile()
		{
			Answers answers = new();
			TextViewerViewModel viewer = Viewer("{1}{}{a}\r\n", ".txt", answers);
			List<String?> changes = new();
			viewer.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

			Assert.False(viewer.IsMessageFile);
			Assert.False(viewer.GoToIdCommand.CanExecute(null));

			Byte[] data = Encoding.ASCII.GetBytes("{1}{}{a}\r\n");
			viewer.Show(data, data.Length, ".msg");
			Assert.True(viewer.IsMessageFile);
			Assert.Contains(nameof(TextViewerViewModel.IsMessageFile), changes);
			Assert.True(viewer.GoToIdCommand.CanExecute(null));
		}
	}
}