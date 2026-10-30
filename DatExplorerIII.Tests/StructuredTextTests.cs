namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using DatExplorerIII.Models.Interface;
	using DatExplorerIII.Services.Platform;
	using DatExplorerIII.Services.Syntax;
	using DatExplorerIII.Services.Texts;

	/// <summary>JSON, XML, YAML, TOML and INI: the colors cover the whole text, and the beautifier writes it with a regular indentation.</summary>
	public sealed class StructuredTextTests
	{
		private const String Json = "{\"name\": \"a\", \"n\": -1.5e3, \"ok\": true, \"list\": [1, null]} // end";
		private const String Xml = "<?xml version=\"1.0\"?>\n<!-- c --><root a=\"1\"><b>x &amp; y</b><c/><![CDATA[z]]></root>";
		private const String Yaml = "# c\n---\nname: x\nlist:\n  - 1\n  - true\ntext: |\n  hello\n  world\nref: &a \"q\" # tail\n";
		private const String Toml = "# c\n[table]\nkey = \"v\"\nn = 12\nok = true\nmulti = \"\"\"\nline\n\"\"\"\narr = [1, 2]\n";

		public static IEnumerable<Object[]> Samples()
		{
			yield return [Json, (Func<String, IReadOnlyList<SyntaxSpan>>)JsonSyntax.Tokenize];
			yield return [Xml, (Func<String, IReadOnlyList<SyntaxSpan>>)XmlSyntax.Tokenize];
			yield return [Yaml, (Func<String, IReadOnlyList<SyntaxSpan>>)YamlSyntax.Tokenize];
			yield return [Toml, (Func<String, IReadOnlyList<SyntaxSpan>>)TomlSyntax.Tokenize];
		}

		[Theory]
		[MemberData(nameof(Samples))]
		public void TheColors_CoverTheWholeTextWithoutGapOrOverlap(String text, Func<String, IReadOnlyList<SyntaxSpan>> tokenize)
		{
			IReadOnlyList<SyntaxSpan> spans = tokenize(text);
			Int32 position = 0;
			foreach (SyntaxSpan span in spans)
			{
				Assert.Equal(position, span.Start);
				position += span.Length;
			}

			Assert.Equal(text.Length, position);
		}

		[Theory]
		[MemberData(nameof(Samples))]
		public void TheColors_NeverFail_OnATextCutAnywhere(String text, Func<String, IReadOnlyList<SyntaxSpan>> tokenize)
		{
			for (Int32 length = 0; length <= text.Length; length++)
			{
				String part = text[..length];
				Assert.Equal(part.Length, tokenize(part).Sum(span => span.Length));
			}
		}

		[Fact]
		public void Json_MarksNamesAndValues()
		{
			IReadOnlyList<SyntaxSpan> spans = JsonSyntax.Tokenize(Json);
			String Of(SyntaxKind kind) => String.Join("|", spans.Where(span => span.Kind == kind).Select(span => Json.Substring(span.Start, span.Length)));

			Assert.Equal("\"name\"|\"n\"|\"ok\"|\"list\"", Of(SyntaxKind.Identifier));
			Assert.Equal("\"a\"", Of(SyntaxKind.String));
			Assert.Equal("-1.5e3|1", Of(SyntaxKind.Number));
			Assert.Equal("true|null", Of(SyntaxKind.Keyword));
			Assert.Equal("// end", Of(SyntaxKind.Comment));
		}

		[Fact]
		public void Yaml_MarksKeysAndTheBlockText()
		{
			IReadOnlyList<SyntaxSpan> spans = YamlSyntax.Tokenize(Yaml);
			String Of(SyntaxKind kind) => String.Join("|", spans.Where(span => span.Kind == kind).Select(span => Yaml.Substring(span.Start, span.Length)));

			Assert.Equal("name|list|text|ref", Of(SyntaxKind.Identifier));
			Assert.Contains("hello", Of(SyntaxKind.String));
			Assert.Equal("# c|# tail", Of(SyntaxKind.Comment));
		}

		[Fact]
		public void Beautify_Json_IndentsIt()
		{
			String? result = TextBeautifier.Beautify(".json", "{\"a\":[1,2],\"b\":{\"c\":null}}");

			Assert.NotNull(result);
			Assert.Contains("\n  \"a\": [", result);
			Assert.Contains("\n    \"c\": null", result);
		}

		[Fact]
		public void Beautify_Xml_IndentsIt()
		{
			String? result = TextBeautifier.Beautify(".xml", "<a><b><c/></b></a>");

			Assert.NotNull(result);
			Assert.Contains("\n  <b>", result);
			Assert.Contains("\n    <c />", result);
		}

		[Fact]
		public void Beautify_Yaml_UsesTwoSpacesPerLevel()
		{
			String? result = TextBeautifier.Beautify(".yaml", "a:\n    b:\n        c: 1   \n\n\n\n    d: 2\n");

			Assert.Equal("a:\n  b:\n    c: 1\n\n  d: 2\n", result);
		}

		[Fact]
		public void Beautify_AnInvalidText_GivesNothing()
		{
			Assert.Null(TextBeautifier.Beautify(".json", "{ not json"));
			Assert.Null(TextBeautifier.Beautify(".xml", "<a>"));
		}

		[Fact]
		public void AnAssociation_ChangesHowAnExtensionIsRead()
		{
			try
			{
				Assert.False(FileViewers.TryGetKind("x.foo", false, out _));

				FileViewers.SetAssociations(new Dictionary<String, ViewerReader> { ["foo"] = ViewerReader.Json });

				Assert.True(FileViewers.TryGetKind("x.FOO", false, out FileViewerKind kind));
				Assert.Equal(FileViewerKind.Text, kind);
				Assert.Equal(".json", FileViewers.EffectiveExtension("x.foo"));
				Assert.Equal(".xml", FileViewers.EffectiveExtension("x.xml"));
			}
			finally
			{
				FileViewers.SetAssociations(null);
			}
		}

		[Fact]
		public void PreviewLimit_IsTheOneOfTheExtension_ElseTheGlobalOne()
		{
			try
			{
				PreviewLimits.Set(10, new Dictionary<String, Int32> { ["mve"] = 500, [".txt"] = 5000 });

				Assert.Equal(10L * 1024 * 1024, PreviewLimits.For("a.bmp"));
				Assert.Equal(500L * 1024 * 1024, PreviewLimits.For("movie.MVE"));
				Assert.Equal(2000L * 1024 * 1024, PreviewLimits.For("x.txt"));
			}
			finally
			{
				PreviewLimits.Set(PreviewLimits.DefaultMegabytes, PreviewLimits.DefaultByExtension());
			}
		}
	}
}
