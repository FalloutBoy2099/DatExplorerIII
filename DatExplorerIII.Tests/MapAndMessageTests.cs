namespace DatExplorerIII.Tests
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using DatExplorerIII.Core;
	using DatExplorerIII.Formats;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;
	using DatExplorerIII.ViewModels;
	using DatExplorerIII.Views;

	/// <summary>
	/// The maps of Fallout (.map) and the colors of the message files (.msg).
	/// </summary>
	public sealed class MapAndMessageTests : IDisposable
	{
		private readonly String _folder = Path.Combine(Path.GetTempPath(), "DatExplorerIII.Tests", Path.GetRandomFileName());

		public MapAndMessageTests()
		{
			Directory.CreateDirectory(_folder);
		}

		public void Dispose()
		{
			Directory.Delete(_folder, true);
		}

		#region Map files

		/// <summary>Writes big endian 32 bit words.</summary>
		private sealed class Words
		{
			private readonly List<Byte> _bytes = new();

			public Words Add(params Int64[] values)
			{
				foreach (Int64 value in values)
				{
					Byte[] bytes = new Byte[4];
					BinaryPrimitives.WriteUInt32BigEndian(bytes, unchecked((UInt32)value));
					_bytes.AddRange(bytes);
				}

				return this;
			}

			public Words Zeros(Int32 words) => Add(new Int64[words]);

			public Byte[] ToArray() => _bytes.ToArray();
		}

		/// <summary>An object: the 22 common words, and the ones that depend on what it is.</summary>
		private static Words AddObject(Words words, Int64 pid, Int64 position, Int64 carried, params Int64[] extra)
		{
			Int64[] common = new Int64[22];
			common[1] = position;
			common[11] = pid;
			common[18] = carried;
			return words.Add(common).Add(extra);
		}

		private static Byte[] BuildMap(Boolean withItem = false)
		{
			Words words = new();
			words.Add(20);
			Byte[] name = new Byte[16];
			Encoding.ASCII.GetBytes("TESTMAP.MAP").CopyTo(name, 0);
			words.Add(name.Chunk(4).Select(chunk => (Int64)BinaryPrimitives.ReadUInt32BigEndian(chunk)).ToArray());

			// Player start (hexagon, elevation, direction), local variables, script, flags (elevations 2 and 3 are missing),
			// darkness, global variables, map number; the rest of the header is zeros.
			words.Add(18492, 0, 2, 3, 5, 12, 1, 2, 9);
			words.Zeros((236 - 4 - 16 - 9 * 4) / 4);

			// The variables.
			words.Zeros(5);

			// The tiles of the only elevation: roof then floor, 1 is empty. Three floors and one roof are set.
			for (Int32 index = 0; index < 10000; index++)
			{
				Int64 roof = index == 0 ? 7 : 1;
				Int64 floor = index < 3 ? 5 : 1;
				Byte[] tile = new Byte[4];
				BinaryPrimitives.WriteUInt16BigEndian(tile, (UInt16)roof);
				BinaryPrimitives.WriteUInt16BigEndian(tile.AsSpan(2), (UInt16)floor);
				words.Add(BinaryPrimitives.ReadUInt32BigEndian(tile));
			}

			// Scripts: none of the system type, one spatial script, none of the timer type, 17 of the item type (two groups of 16
			// slots), none of the critter type.
			words.Add(0);
			words.Add(1);
			for (Int32 slot = 0; slot < 16; slot++)
			{
				if (slot == 0)
				{
					words.Add((1L << 24) | 1, -1, (0L << 29) | 1234, 4);
				}
				else
				{
					words.Add(0xFFFFFFFFL, -1);
				}

				// flags, program, and the rest of the 14 words.
				words.Add(0, slot == 0 ? 7 : 0).Zeros(12);
			}

			words.Add(1, 0);
			words.Add(0);
			words.Add(17);
			for (Int32 group = 0; group < 2; group++)
			{
				for (Int32 slot = 0; slot < 16; slot++)
				{
					words.Add((3L << 24) | (group * 16 + slot), -1, 0, group * 16 + slot < 17 ? 11 : 0).Zeros(12);
				}

				words.Add(group == 0 ? 16 : 1, 0);
			}

			words.Add(0);

			// Objects: a creature that carries a tile, an exit grid and a wall (and an item when asked: its prototype is needed).
			Int32 total = withItem ? 4 : 3;
			words.Add(total);
			words.Add(total);
			AddObject(words, (1L << 24) | 3, 15000, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
			words.Add(1);
			AddObject(words, (4L << 24) | 1, 0, 0);
			AddObject(words, (5L << 24) | 16, 21718, 0, 3, 28707, 0, 1);
			AddObject(words, (3L << 24) | 30, 20000, 0);
			if (withItem)
			{
				AddObject(words, (0L << 24) | 5, 20001, 0);
			}

			// The two other elevations hold nothing.
			words.Add(0);
			words.Add(0);
			return words.ToArray();
		}

		[Fact]
		public void IsMap_AcceptsTheTwoVersionsOfTheGame_AndNothingElse()
		{
			Byte[] map = BuildMap();
			Assert.True(MapReader.IsMap(map));

			Byte[] fallout1 = (Byte[])map.Clone();
			fallout1[3] = 19;
			Assert.True(MapReader.IsMap(fallout1));

			Byte[] other = (Byte[])map.Clone();
			other[3] = 21;
			Assert.False(MapReader.IsMap(other));
			Assert.False(MapReader.IsMap(map.AsSpan(0, 100)));
			Assert.False(MapReader.IsMap(Array.Empty<Byte>()));
			Assert.Throws<InvalidDataException>(() => MapReader.Read(other));
		}

		[Fact]
		public void Read_GivesTheHeader_TheTilesTheScriptsAndTheObjects()
		{
			MapFile map = MapReader.Read(BuildMap());

			Assert.Null(map.Note);
			Assert.Equal(20, map.Header.Version);
			Assert.Equal("TESTMAP.MAP", map.Header.Name);
			Assert.Equal(18492, map.Header.PlayerTile);
			Assert.Equal(2, map.Header.PlayerOrientation);
			Assert.Equal(5, map.Header.ScriptId);
			Assert.Equal(12, map.Header.Flags);
			Assert.Equal(1, map.Header.Darkness);
			Assert.Equal(9, map.Header.MapNumber);
			Assert.Equal(2, map.GlobalVariableCount);
			Assert.Equal(3, map.LocalVariableCount);
			Assert.Equal(3, map.TotalObjects);

			MapElevation elevation = Assert.Single(map.Elevations);
			Assert.Equal(0, elevation.Index);
			Assert.Equal(3, elevation.FloorTiles);
			Assert.Equal(1, elevation.RoofTiles);
			Assert.Equal(new[] { 0, 1, 0, 1, 0, 1 }, elevation.ObjectsByType);
			Assert.Equal(1, elevation.CarriedItems);

			// The padding of the groups of 16 slots is not a script.
			Assert.Equal(18, map.Scripts.Count);
			MapScriptEntry spatial = Assert.Single(map.Scripts, script => script.Type == 1);
			Assert.Equal(1234, spatial.Tile);
			Assert.Equal(0, spatial.Elevation);
			Assert.Equal(4, spatial.Radius);
			Assert.Equal(7, spatial.ScriptId);
			Assert.Equal(17, map.Scripts.Count(script => script.Type == 3));

			MapTransition exit = Assert.Single(map.Transitions);
			Assert.Equal(MapTransitionKind.ExitGrid, exit.Kind);
			Assert.Equal(21718, exit.Tile);
			Assert.Equal(3, exit.DestinationMap);
			Assert.Equal(28707, exit.DestinationTile);
		}

		[Fact]
		public void Read_AnItemNeedsItsPrototype_WithoutItTheReadingStopsWithANote()
		{
			MapFile map = MapReader.Read(BuildMap(withItem: true));

			Assert.NotNull(map.Note);
			Assert.Contains("prototype", map.Note);

			// What came before is kept.
			Assert.Equal(3, Assert.Single(map.Elevations).FloorTiles);
			Assert.Equal(18, map.Scripts.Count);
			Assert.Equal(4, map.TotalObjects);
		}

		[Fact]
		public void Read_ANeverFailsWhateverTheContent()
		{
			Byte[] valid = BuildMap();

			// Cut anywhere.
			for (Int32 length = 0; length < valid.Length; length += 37)
			{
				Byte[] data = valid[..length];
				if (MapReader.IsMap(data))
				{
					MapFile map = MapReader.Read(data);
					Assert.NotNull(map.Note);
				}
			}

			// Changed anywhere.
			Random random = new(2026);
			for (Int32 round = 0; round < 300; round++)
			{
				Byte[] data = (Byte[])valid.Clone();
				for (Int32 change = 0; change < random.Next(1, 8); change++)
				{
					data[random.Next(0, data.Length)] = (Byte)random.Next(256);
				}

				if (MapReader.IsMap(data))
				{
					MapReader.Read(data);
				}
			}
		}

		[Fact]
		public void Read_AMapThatEndsWithMoreBytes_SaysSo()
		{
			Byte[] valid = BuildMap();
			MapFile map = MapReader.Read(valid.Concat(new Byte[] { 1, 2, 3 }).ToArray());

			Assert.NotNull(map.Note);
			Assert.Contains("3 bytes", map.Note);
		}

		[Fact]
		public async Task Viewer_ShowsWhatAMapHolds()
		{
			using DatArchive archive = CreateArchive(("maps", "testmap.map", BuildMap()), ("maps", "broken.map", BuildMap()[..5000]), ("data", "a.txt", Encoding.ASCII.GetBytes("text")));
			using ViewerViewModel viewer = new(new ManualUiThread());

			await viewer.LoadAsync(archive, archive.Find("maps\\testmap.map")!);
			Assert.True(viewer.IsMap);
			Assert.Equal(4, viewer.Map.Sections.Count);

			// General, elevation 1, scripts, exits.
			Assert.Contains(viewer.Map.Sections[0].Rows, row => row.Value == "TESTMAP.MAP");
			Assert.Equal("(92, 92)", MapViewerViewModel.Position(18492));
			Assert.Equal("\u2014", MapViewerViewModel.Position(-1));
			Assert.Contains(viewer.Map.Sections[1].Rows, row => row.Value == "3");
			Assert.Equal(2, viewer.Map.Sections[2].Rows.Count);
			Assert.Single(viewer.Map.Sections[3].Rows);

			// A map cut short is shown with what was read, and says so.
			await viewer.LoadAsync(archive, archive.Find("maps\\broken.map")!);
			Assert.True(viewer.IsMap);
			Assert.True(viewer.Map.Sections[0].Rows.Count > 10);

			await viewer.LoadAsync(archive, archive.Find("data\\a.txt")!);
			Assert.False(viewer.IsMap);
			Assert.Empty(viewer.Map.Sections);
		}

		[Fact]
		public void Resources_FindTheNamesInTheListsOfTheGame()
		{
			Dictionary<String, Byte[]> files = new(StringComparer.OrdinalIgnoreCase)
			{
				[@"scripts\scripts.lst"] = Encoding.ASCII.GetBytes("first.int ; Dialog\r\nsecond.int ; Map # local_vars=3\r\n"),
				[@"data\maps.txt"] = Encoding.ASCII.GetBytes("[Map 000]\r\nlookup_name=Desert\r\nmap_name=desert1\r\n\r\n[Map 001]\r\nmap_name=only_file\r\n"),
			};

			MapResources resources = new(path => files.GetValueOrDefault(path));

			Assert.Equal("first.int", resources.ScriptName(0));
			Assert.Equal("second.int", resources.ScriptName(1));
			Assert.Null(resources.ScriptName(2));
			Assert.Null(resources.ScriptName(-1));
			Assert.Equal("Desert (desert1)", resources.MapName(0));
			Assert.Equal("only_file", resources.MapName(1));
			Assert.Null(resources.MapName(7));
			Assert.Null(resources.SubtypeOf(0x00000001));
			Assert.Null(resources.SubtypeOf(0x01000001));

			// Nothing is found when the game files are not there.
			MapResources empty = new(_ => null);
			Assert.Null(empty.ScriptName(0));
			Assert.Null(empty.MapName(0));
		}

		[Fact]
		public void Resources_ASectionOfTheListOfMapsWithoutNames_GivesNoName()
		{
			Dictionary<String, Byte[]> files = new(StringComparer.OrdinalIgnoreCase)
			{
				[@"data\maps.txt"] = Encoding.ASCII.GetBytes("[Map 002]\r\nmusic=something\r\n"),
			};

			Assert.Null(new MapResources(path => files.GetValueOrDefault(path)).MapName(2));
		}

		private DatArchive CreateArchive(params (String Directory, String Name, Byte[] Content)[] files)
		{
			DatArchive archive = DatArchive.Create(Path.Combine(_folder, "game.dat"), DatFormat.Fallout2);
			foreach ((String directory, String name, Byte[] content) in files)
			{
				String source = Path.Combine(_folder, Guid.NewGuid().ToString("N"));
				File.WriteAllBytes(source, content);
				archive.AddFile(source, directory, name);
			}

			return archive;
		}

		#endregion

		#region Message files

		private static String Kinds(String text)
		{
			return String.Concat(MsgSyntax.Tokenize(text).Select(span => span.Kind switch
			{
				SyntaxKind.Plain => 'p',
				SyntaxKind.Comment => 'c',
				SyntaxKind.Brace => 'b',
				SyntaxKind.Identifier => 'i',
				SyntaxKind.Sound => 's',
				SyntaxKind.Message => 'm',
				_ => 'h',
			}));
		}

		[Fact]
		public void MsgSyntax_ColorsTheThreePartsOfALine()
		{
			IReadOnlyList<SyntaxSpan> spans = MsgSyntax.Tokenize("{100}{sound01}{Hello there.}");

			// Braces that touch each other are one part.
			Assert.Equal("bibsbmb", Kinds("{100}{sound01}{Hello there.}"));
			Assert.Equal(new[] { "{", "100", "}{", "sound01", "}{", "Hello there.", "}" }, spans.Select(span => "{100}{sound01}{Hello there.}".Substring(span.Start, span.Length)));
			Assert.Equal(SyntaxKind.Identifier, spans[1].Kind);
			Assert.Equal(SyntaxKind.Sound, spans[3].Kind);
			Assert.Equal(SyntaxKind.Message, spans[5].Kind);
		}

		[Fact]
		public void MsgSyntax_CommentsRunToTheEndOfTheLine_AndTheTextCanRunOverSeveralLines()
		{
			const String text = "# a comment with {braces}\r\n{1}{}{first\r\nsecond}\r\n  stray words\r\n{2}{}{x}";
			IReadOnlyList<SyntaxSpan> spans = MsgSyntax.Tokenize(text);

			SyntaxSpan comment = spans[0];
			Assert.Equal(SyntaxKind.Comment, comment.Kind);
			Assert.Equal("# a comment with {braces}", text.Substring(comment.Start, comment.Length));

			SyntaxSpan message = spans.First(span => span.Kind == SyntaxKind.Message);
			Assert.Equal("first\r\nsecond", text.Substring(message.Start, message.Length));

			// Words outside the braces are ignored by the game, so they are shown as comments.
			Assert.Contains(spans, span => span.Kind == SyntaxKind.Comment && text.Substring(span.Start, span.Length) == "stray");

			// The second line starts again with an identifier.
			Assert.Equal("2", text.Substring(spans.First(span => span.Kind == SyntaxKind.Identifier && span.Start > message.Start).Start, 1));
		}

		[Fact]
		public void MsgSyntax_ShowsThePlacesWhereTheGamePutsAValue()
		{
			const String text = "{5}{}{You have %d caps, %s. 100% sure, %%.}";
			IReadOnlyList<SyntaxSpan> spans = MsgSyntax.Tokenize(text);

			Assert.Equal(new[] { "%d", "%s", "%%" }, spans.Where(span => span.Kind == SyntaxKind.Placeholder).Select(span => text.Substring(span.Start, span.Length)));
		}

		[Theory]
		[InlineData("")]
		[InlineData("{")]
		[InlineData("{1}{2}{3")]
		[InlineData("}}}{{{")]
		[InlineData("{1}{}{50% off")]
		[InlineData("\r\n\r\n#\r\n")]
		[InlineData("{1}{}{a}{b}{c}{d}")]
		public void MsgSyntax_CoversAnyText_WithoutGapOrOverlap(String text)
		{
			AssertCovers(text);
		}

		[Fact]
		public void MsgSyntax_CoversRandomTexts()
		{
			Random random = new(7);
			const String alphabet = "{}#%sd0123 \r\n\tabc.";
			for (Int32 round = 0; round < 500; round++)
			{
				AssertCovers(new String(Enumerable.Range(0, random.Next(0, 80)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()));
			}
		}

		private static void AssertCovers(String text)
		{
			Int32 position = 0;
			foreach (SyntaxSpan span in MsgSyntax.Tokenize(text))
			{
				Assert.Equal(position, span.Start);
				Assert.True(span.Length > 0);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
		}

		[Fact]
		public void TextViewer_ColorsOnlyMessageFiles()
		{
			Byte[] text = Encoding.ASCII.GetBytes("{1}{}{Hello}\r\n");
			TextViewerViewModel viewer = new(new ManualUiThread());

			viewer.Show(text, text.Length, ".msg");
			Assert.True(viewer.IsHighlighted);
			Assert.NotEmpty(viewer.SyntaxSpans!);

			viewer.Show(text, text.Length, ".MSG");
			Assert.True(viewer.IsHighlighted);

			viewer.Show(text, text.Length, ".txt");
			Assert.False(viewer.IsHighlighted);

			viewer.Show(text, text.Length);
			Assert.False(viewer.IsHighlighted);

			// A file that looks binary and a file that is too large are shown plainly.
			viewer.Show(Enumerable.Range(0, 400).Select(index => (Byte)(index % 5)).ToArray(), 400, ".msg");
			Assert.False(viewer.IsHighlighted);

			viewer.Show(Enumerable.Repeat((Byte)'a', TextViewerViewModel.MaxColoredLength + 10).ToArray(), TextViewerViewModel.MaxColoredLength + 10, ".msg");
			Assert.False(viewer.IsHighlighted);

			viewer.Show(text, text.Length, ".msg");
			viewer.Clear();
			Assert.False(viewer.IsHighlighted);
		}

		[Fact]
		public void TextViewer_ChangingTheLanguage_SurvivesTheBoxThatEmptiesItsSelection()
		{
			Byte[] text = Encoding.ASCII.GetBytes("{1}{}{Hello}\r\n");
			TextViewerViewModel viewer = new(new ManualUiThread());
			viewer.Show(text, text.Length, ".msg");

			// A combo box whose list is replaced gives up its selection and writes null in the property it is bound to.
			viewer.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(TextViewerViewModel.Options))
				{
					viewer.SelectedEncoding = null!;
				}
			};

			viewer.RefreshLanguage();

			Assert.NotNull(viewer.SelectedEncoding);
			Assert.Equal(TextEncodingOption.Automatic, viewer.SelectedEncoding.CodePage);
			Assert.Contains("Hello", viewer.Text);
			Assert.True(viewer.IsHighlighted);
		}

		[Fact]
		public void AudioViewer_ChangingTheLanguage_SurvivesTheBoxThatEmptiesItsSelection()
		{
			using AudioViewerViewModel viewer = new(new ManualUiThread());
			ChannelChoice before = viewer.SelectedChannels;

			viewer.PropertyChanged += (_, args) =>
			{
				if (args.PropertyName == nameof(AudioViewerViewModel.ChannelChoices))
				{
					viewer.SelectedChannels = null!;
				}
			};

			viewer.RefreshLanguage();
			viewer.RefreshLanguage();

			Assert.NotNull(viewer.SelectedChannels);
			Assert.Equal(before.Channels, viewer.SelectedChannels.Channels);
		}

		[Fact]
		public void HighlightedDocument_HasOneParagraphPerLine_AndTheSameText()
		{
			const String text = "# comment\r\n{1}{snd}{Hello %s}\r\n{2}{}{two\r\nlines}\r\n";
			System.Windows.Documents.FlowDocument document = HighlightedDocument.Build(text, MsgSyntax.Tokenize(text));

			String shown = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text;
			Assert.Equal(text.Replace("\r\n", "\n").Split('\n'), shown.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').Concat(new[] { String.Empty }));
			Assert.Equal(5, document.Blocks.Count);
		}

		[Fact]
		public void TheParagraphs_AreMadeOneAtATime_AndAreTheSameAsTheWholeDocument()
		{
			const String text = "# comment\r\n{1}{snd}{Hello %s}\r\n{2}{}{two\r\nlines}\r\n";
			IReadOnlyList<SyntaxSpan> spans = MsgSyntax.Tokenize(text);

			// The first paragraph is there without the others being made (the viewer shows a screen at once).
			using IEnumerator<System.Windows.Documents.Paragraph> paragraphs = HighlightedDocument.Paragraphs(text, spans).GetEnumerator();
			Assert.True(paragraphs.MoveNext());
			Assert.Equal("# comment", new System.Windows.Documents.TextRange(paragraphs.Current.ContentStart, paragraphs.Current.ContentEnd).Text);

			List<String> all = HighlightedDocument.Paragraphs(text, spans).Select(paragraph => new System.Windows.Documents.TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text).ToList();
			System.Windows.Documents.FlowDocument document = HighlightedDocument.Build(text, spans);
			Assert.Equal(document.Blocks.Count, all.Count);
			Assert.Equal("{2}{}{two", all[2]);
		}

		#endregion
	}
}