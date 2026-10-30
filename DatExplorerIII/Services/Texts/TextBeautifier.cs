namespace DatExplorerIII.Services.Texts
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.Encodings.Web;
	using System.Text.Json;
	using System.Xml;
	using System.Xml.Linq;

	/// <summary>Writes a JSON, an XML or a YAML text again with a regular indentation.</summary>
	public static class TextBeautifier
	{
		/// <summary>Whether a text of this extension (with the dot) can be beautified.</summary>
		public static Boolean CanBeautify(String? extension)
		{
			return extension != null && extension.ToLowerInvariant() is ".json" or ".xml" or ".yaml" or ".yml";
		}

		/// <summary>The beautified text, or null when it cannot be (it is not valid JSON or XML).</summary>
		public static String? Beautify(String extension, String text)
		{
			try
			{
				return extension.ToLowerInvariant() switch
				{
					".json" => Json(text),
					".xml" => Xml(text),
					".yaml" or ".yml" => Yaml(text),
					_ => null,
				};
			}
			catch (Exception exception) when (exception is JsonException or XmlException or InvalidOperationException)
			{
				return null;
			}
		}

		private static String Json(String text)
		{
			using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
			return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
		}

		private static String Xml(String text)
		{
			XDocument document = XDocument.Parse(text, LoadOptions.None);
			StringBuilder builder = new();
			XmlWriterSettings settings = new() { Indent = true, IndentChars = "  ", OmitXmlDeclaration = document.Declaration == null, NewLineHandling = NewLineHandling.Replace };
			using (XmlWriter writer = XmlWriter.Create(builder, settings))
			{
				document.Save(writer);
			}

			// The writer names UTF-16 for what it writes into a string.
			String result = builder.ToString();
			if (document.Declaration?.Encoding is { } encoding)
				result = result.Replace("encoding=\"utf-16\"", "encoding=\"" + encoding + "\"", StringComparison.Ordinal);

			return result;
		}

		/// <summary>
		/// A YAML text without a library: the indentation of every level becomes two spaces, tabs and spaces at the end of lines are
		/// taken away and a run of empty lines is one. The text of a block (after | or &gt;) keeps its own layout.
		/// </summary>
		private static String Yaml(String text)
		{
			String[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
			List<String> result = [];
			List<Int32> levels = [];
			Int32 blockIndent = -1;
			Int32 blockShift = 0;
			Boolean previousBlank = true;

			foreach (String raw in lines)
			{
				String line = raw.TrimEnd();
				if (line.Length == 0)
				{
					if (!previousBlank)
						result.Add(String.Empty);

					previousBlank = true;
					continue;
				}

				previousBlank = false;
				Int32 indent = line.Length - line.TrimStart(' ', '\t').Length;
				String content = line.TrimStart(' ', '\t');

				if (blockIndent >= 0 && indent > blockIndent)
				{
					result.Add(new String(' ', Math.Max(0, indent + blockShift)) + content);
					continue;
				}

				blockIndent = -1;
				while (levels.Count > 0 && levels[^1] > indent)
				{
					levels.RemoveAt(levels.Count - 1);
				}

				if (levels.Count == 0 || levels[^1] < indent)
					levels.Add(indent);

				Int32 depth = levels.Count - 1;
				result.Add(new String(' ', depth * 2) + content);

				String tail = content;
				Int32 comment = tail.IndexOf(" #", StringComparison.Ordinal);
				if (comment >= 0)
					tail = tail[..comment].TrimEnd();

				if (tail.EndsWith('|') || tail.EndsWith('>') || tail.EndsWith("|-", StringComparison.Ordinal) || tail.EndsWith(">-", StringComparison.Ordinal) || tail.EndsWith("|+", StringComparison.Ordinal) || tail.EndsWith(">+", StringComparison.Ordinal))
				{
					blockIndent = indent;
					blockShift = (depth * 2) - indent;
				}
			}

			while (result.Count > 0 && result[^1].Length == 0)
			{
				result.RemoveAt(result.Count - 1);
			}

			return String.Join("\n", result) + "\n";
		}
	}
}
