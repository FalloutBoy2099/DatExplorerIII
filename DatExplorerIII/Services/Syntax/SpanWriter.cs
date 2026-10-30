namespace DatExplorerIII.Services.Syntax
{
	using System;
	using System.Collections.Generic;

	/// <summary>Writes the parts of a text one after the other: every part goes from where the last one stopped, so that no character is left out.</summary>
	internal sealed class SpanWriter(Int32 length)
	{
		private readonly Int32 _length = length;

		public List<SyntaxSpan> Spans { get; } = [];

		/// <summary>Where the next part starts.</summary>
		public Int32 Position { get; private set; }

		/// <summary>Gives the text up to <paramref name="end"/> (not included) the kind; nothing when it is already past.</summary>
		public void To(Int32 end, SyntaxKind kind)
		{
			end = Math.Min(end, _length);
			if (end > Position)
			{
				SyntaxSpans.Add(Spans, Position, end - Position, kind);
				Position = end;
			}
		}

		/// <summary>Gives a part of the text its kind; what comes before it, from the last part, is plain.</summary>
		public void Part(Int32 start, Int32 end, SyntaxKind kind)
		{
			To(start, SyntaxKind.Plain);
			To(end, kind);
		}

		/// <summary>The rest of the text is plain; returns the parts.</summary>
		public IReadOnlyList<SyntaxSpan> Finish()
		{
			To(_length, SyntaxKind.Plain);
			return Spans;
		}
	}
}
