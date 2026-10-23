namespace DatExplorerIII.Tests
{
	using System;
	using System.Data;
	using System.Linq;
	using DatExplorerIII.Formats.GameFiles;
	using DatExplorerIII.Models.GameFiles;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.ViewModels.Viewer;

	/// <summary>The comments (two slashes) of the message files and of the dialogs: at the end of a line, and between the lines.</summary>
	public sealed class BraceCommentsTests
	{
		private const String Mes = "// header of the file\r\n{1}{one} // first\r\n// before two\r\n// and more\r\n{2}{two}\r\n{3}{three} // last // really\r\n// the end\r\n";

		[Fact]
		public void AMessageFile_KeepsTheCommentsAtTheEndOfTheLines()
		{
			MesFile file = MesReader.Parse(Mes);

			Assert.Equal([3, 3], [file.Entries.Count, file.Entries.Count]);
			Assert.Equal(["first", "", "last // really"], file.Entries.Select(entry => entry.Comment).ToArray());
		}

		[Fact]
		public void AMessageFile_KeepsTheCommentsBetweenTheLines_WithTheirPlace()
		{
			MesFile file = MesReader.Parse(Mes);

			Assert.Equal(
				[(0, "header of the file"), (1, "before two"), (1, "and more"), (3, "the end")],
				file.Comments.Select(comment => (comment.EntriesBefore, comment.Text)).ToArray());
		}

		[Fact]
		public void ADialog_KeepsItsComments_Too()
		{
			DlgFile file = DlgReader.Parse("// intro\n{1}{hello}{hello}{}{}{2}{} // says hello\n");

			Assert.Equal("says hello", file.Entries.Single().Comment);
			Assert.Equal("intro", file.Comments.Single().Text);
		}

		[Fact]
		public void TheTable_ShowsTheCommentsOnlyWhenAsked()
		{
			TableViewerViewModel table = new();
			table.Show(MesReader.Parse(Mes));

			Assert.True(table.HasComments);
			Assert.Equal(3, table.Rows!.Count);
			Assert.False(table.Rows.Table!.Columns.Contains("Comment"));

			table.ShowLineComments = true;
			Assert.Equal("first", table.Rows!.Table!.Rows[0]["Comment"]);

			table.ShowBlockComments = true;
			DataTable rows = table.Rows!.Table!;
			Assert.Equal(7, rows.Rows.Count);
			Assert.Equal("// header of the file", rows.Rows[0]["Text"]);
			Assert.Equal(true, rows.Rows[0]["IsComment"]);
			Assert.Equal("one", rows.Rows[1]["Text"]);
			Assert.Equal("// before two", rows.Rows[2]["Text"]);
			Assert.Equal("// the end", rows.Rows[6]["Text"]);
		}
	}
}