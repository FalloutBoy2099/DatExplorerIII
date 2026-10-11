namespace DatExplorerIII.Models
{
	using System;
	using System.Text.Json.Serialization;

	/// <summary>How the content of two files is compared.</summary>
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum CompareMethod
	{
		/// <summary>The files made of text are compared line by line, and the differences the options ignore do not count.</summary>
		Text,

		/// <summary>Every file is compared byte for byte.</summary>
		Binary,
	}

	/// <summary>
	/// What the comparison of two archives ignores. The options about the text only apply to the files made of text (and to the
	/// <see cref="CompareMethod.Text"/> method): the other files are the same when their bytes are.
	/// </summary>
	public sealed record ComparisonOptions
	{
		public CompareMethod Method { get; init; } = CompareMethod.Text;

		/// <summary>Spaces and tabs (all the white space characters) do not count.</summary>
		public Boolean IgnoreWhitespace { get; init; }

		/// <summary>The lines that are empty (or hold only white space) do not count.</summary>
		public Boolean IgnoreBlankLines { get; init; }

		/// <summary>"A" and "a" are the same.</summary>
		public Boolean IgnoreCase { get; init; }

		/// <summary>A file with Windows, Unix or Mac line endings is the same as one with the others.</summary>
		public Boolean IgnoreLineEndings { get; init; }

		/// <summary>A text saved with another encoding (UTF-8, Windows-1252...) is the same when it reads the same.</summary>
		public Boolean IgnoreCodePage { get; init; }

		/// <summary>The numbers do not count: "12" and "345" are the same.</summary>
		public Boolean IgnoreNumbers { get; init; }

		/// <summary>The comments (<c>//</c>, <c>/* */</c>, a line that starts with <c>;</c> or <c>#</c>) do not count.</summary>
		public Boolean IgnoreComments { get; init; }

		/// <summary>A last line with a line break is the same as one without.</summary>
		public Boolean IgnoreMissingFinalEol { get; init; }

		/// <summary>Nothing is ignored.</summary>
		public static ComparisonOptions Strict { get; } = new();

		/// <summary>Whether the lines of a text are compared as they are written.</summary>
		[JsonIgnore]
		public Boolean KeepsLinesAsWritten => !IgnoreWhitespace && !IgnoreBlankLines && !IgnoreCase && !IgnoreNumbers && !IgnoreComments;

		/// <summary>What really applies: the binary method ignores nothing.</summary>
		[JsonIgnore]
		public ComparisonOptions Effective => Method == CompareMethod.Binary ? Strict : this;
	}
}