namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DatExplorerIII.Models.Archives;
	using DatExplorerIII.ViewModels;

	/// <summary>The list of the files found by a search: its order and the file it opens.</summary>
	public sealed class SearchResultsTests
	{
		private static DatEntry Entry(String directory, String name, Int32 size)
		{
			return new DatEntry(directory, name) { UnpackedSize = size };
		}

		private static List<DatEntry> Found()
		{
			return
			[
				Entry("art\\walls\\", "block2.frm", 508),
				Entry("art\\scenery\\", "Block.frm", 75),
				Entry("art\\walls\\", "block.frm", 75),
				Entry("text\\", "alpha.msg", 9000),
			];
		}

		[Fact]
		public void List_StartsSortedByName_ThenByFolder()
		{
			SearchResultsViewModel results = new(Found(), _ => { });
			Assert.Equal(["alpha.msg", "Block.frm", "block.frm", "block2.frm"], results.Items.Select(item => item.Name).ToList());
			Assert.Equal("art\\scenery\\", results.Items[1].Entry!.Directory);
			Assert.Equal("art\\walls\\", results.Items[2].Entry!.Directory);
			Assert.Equal("", results.NameSortGlyph);
			Assert.Equal(String.Empty, results.SizeSortGlyph);
		}

		[Fact]
		public void SecondClickOnAColumn_ReversesIt()
		{
			SearchResultsViewModel results = new(Found(), _ => { });
			results.SortCommand.Execute("Size");
			Assert.Equal([75, 75, 508, 9000], results.Items.Select(item => item.Entry!.UnpackedSize).ToList());
			Assert.Equal("", results.SizeSortGlyph);
			Assert.Equal(String.Empty, results.NameSortGlyph);

			results.SortCommand.Execute("Size");
			Assert.Equal([9000, 508, 75, 75], results.Items.Select(item => item.Entry!.UnpackedSize).ToList());
			Assert.Equal("", results.SizeSortGlyph);
		}

		[Fact]
		public void SortByFolder_KeepsTheNamesInOrderInsideAFolder()
		{
			SearchResultsViewModel results = new(Found(), _ => { });
			results.SortBy(SearchSortColumn.Folder);
			Assert.Equal(["art\\scenery\\", "art\\walls\\", "art\\walls\\", "text\\"], results.Items.Select(item => item.Entry!.Directory).ToList());
			Assert.Equal(["block.frm", "block.frm", "block2.frm", "alpha.msg"], results.Items.Select(item => item.Name.ToLowerInvariant()).ToList());

			// Reversed, the names of a folder stay in their order.
			results.SortBy(SearchSortColumn.Folder);
			Assert.Equal("text\\", results.Items[0].Entry!.Directory);
			Assert.Equal(["block.frm", "block2.frm"], results.Items.Where(item => item.Entry!.Directory == "art\\walls\\").Select(item => item.Name).ToList());
		}

		[Fact]
		public void Open_ShowsTheFileOfTheParameter_OrTheSelectedOne()
		{
			List<DatEntry> shown = [];
			SearchResultsViewModel results = new(Found(), shown.Add);
			Assert.False(results.OpenCommand.CanExecute(null));

			results.OpenCommand.Execute(results.Items[0]);
			results.SelectedItem = results.Items[2];
			Assert.True(results.OpenCommand.CanExecute(null));
			results.OpenCommand.Execute(null);

			Assert.Equal([results.Items[0].Entry, results.Items[2].Entry], shown.Cast<DatEntry?>().ToList());
		}

		[Fact]
		public void UnknownColumn_IsNotASort()
		{
			SearchResultsViewModel results = new(Found(), _ => { });
			Assert.False(results.SortCommand.CanExecute("Colour"));
			Assert.False(results.SortCommand.CanExecute(null));
		}
	}
}