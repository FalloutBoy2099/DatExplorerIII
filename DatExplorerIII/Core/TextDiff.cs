namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Text.RegularExpressions;
	using Models.Comparison;

	/// <summary>How a line of the comparison of two texts is.</summary>
	public enum DiffKind
	{
		/// <summary>The line is the same on both sides.</summary>
		Same,

		/// <summary>A line of the left text became the line of the right text.</summary>
		Changed,

		/// <summary>The line is only in the left text.</summary>
		LeftOnly,

		/// <summary>The line is only in the right text.</summary>
		RightOnly,

		/// <summary>A line the comparison was told to ignore (an empty line, a comment): it is shown, and is not a difference.</summary>
		Ignored,
	}

	/// <summary>A line of the two texts side by side: the line of each (0 and an empty text when a side has none).</summary>
	public sealed record DiffRow(DiffKind Kind, Int32 LeftNumber, String LeftText, Int32 RightNumber, String RightText)
	{
		public String LeftNumberText => LeftNumber > 0 ? LeftNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : String.Empty;

		public String RightNumberText => RightNumber > 0 ? RightNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : String.Empty;
	}

	/// <summary>
	/// Compares two texts line by line, as a program that shows the differences of two files does: what is the same stays side by
	/// side, a line that changed is shown with its new version, a line that only one text has faces an empty place.
	/// </summary>
	public static class TextDiff
	{
		/// <summary>The most lines that can differ before the texts are said to be too different to be compared line by line.</summary>
		public const Int32 MaxDifferences = 2000;

		private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

		/// <summary>
		/// The two texts as lines side by side. When they differ by more than <see cref="MaxDifferences"/> lines (the search of the
		/// best match would take too long) the lines between the same start and the same end are paired one by one, and
		/// <paramref name="exact"/> is false.
		/// </summary>
		public static IReadOnlyList<DiffRow> Compare(IReadOnlyList<String> left, IReadOnlyList<String> right, out Boolean exact)
		{
			return Compare(left, right, null, out exact);
		}

		/// <summary>
		/// The same, ignoring what <paramref name="options"/> says to ignore: lines that are the same for it face each other as
		/// the same line (each side shows its own text), and the lines it ignores altogether are shown but are no difference.
		/// </summary>
		public static IReadOnlyList<DiffRow> Compare(IReadOnlyList<String> left, IReadOnlyList<String> right, ComparisonOptions? options, out Boolean exact)
		{
			LineSet leftSet = LineSet.Create(left, options);
			LineSet rightSet = LineSet.Create(right, options);
			List<Pair> pairs = Align(leftSet.Keys, rightSet.Keys, out exact);
			return BuildRows(left, right, leftSet.Kept, rightSet.Kept, pairs);
		}

		/// <summary>Whether two texts are the same once what <paramref name="options"/> ignores is left out.</summary>
		public static Boolean AreEquivalent(IReadOnlyList<String> left, IReadOnlyList<String> right, ComparisonOptions options)
		{
			IReadOnlyList<String> leftKeys = LineSet.Create(left, options).Keys;
			IReadOnlyList<String> rightKeys = LineSet.Create(right, options).Keys;
			if (leftKeys.Count != rightKeys.Count)
				return false;

			for (Int32 index = 0; index < leftKeys.Count; index++)
			{
				if (!String.Equals(leftKeys[index], rightKeys[index], StringComparison.Ordinal))
					return false;
			}

			return true;
		}

		/// <summary>The rows that start a group of lines that differ: where "next difference" goes.</summary>
		public static IReadOnlyList<Int32> GroupStarts(IReadOnlyList<DiffRow> rows)
		{
			List<Int32> starts = [];
			for (Int32 index = 0; index < rows.Count; index++)
			{
				if (IsDifference(rows[index].Kind) && (index == 0 || !IsDifference(rows[index - 1].Kind)))
					starts.Add(index);
			}

			return starts;
		}

		/// <summary>
		/// The lines without their comments: from <c>//</c> to the end of the line, between <c>/*</c> and <c>*/</c> (over several
		/// lines too), and a whole line that starts with <c>;</c> or with <c>#</c> and a sign or a space (not <c>#define</c>).
		/// </summary>
		internal static IReadOnlyList<String> StripComments(IReadOnlyList<String> lines)
		{
			String[] result = new String[lines.Count];
			Boolean inBlock = false;
			for (Int32 index = 0; index < lines.Count; index++)
			{
				String line = lines[index];
				String trimmed = line.TrimStart();
				if (!inBlock && trimmed.Length > 0 && (trimmed[0] == ';' || (trimmed[0] == '#' && (trimmed.Length == 1 || !Char.IsLetter(trimmed[1])))))
				{
					result[index] = String.Empty;
					continue;
				}

				StringBuilder builder = new(line.Length);
				Int32 position = 0;
				while (position < line.Length)
				{
					if (inBlock)
					{
						Int32 close = line.IndexOf("*/", position, StringComparison.Ordinal);
						if (close < 0)
							break;

						inBlock = false;
						position = close + 2;
						continue;
					}

					if (line[position] == '/' && position + 1 < line.Length)
					{
						if (line[position + 1] == '/')
						{
							// What comes before a comment at the end of a line is not a part of it.
							while (builder.Length > 0 && Char.IsWhiteSpace(builder[^1]))
							{
								builder.Length--;
							}

							break;
						}

						if (line[position + 1] == '*')
						{
							inBlock = true;
							position += 2;
							continue;
						}
					}

					builder.Append(line[position]);
					position++;
				}

				result[index] = builder.ToString();
			}

			return result;
		}

		private static Boolean IsDifference(DiffKind kind) => kind is DiffKind.Changed or DiffKind.LeftOnly or DiffKind.RightOnly;

		private enum EditKind
		{
			Equal,
			Delete,
			Insert,
		}

		private readonly record struct Edit(EditKind Kind);

		/// <summary>A line of the left text facing a line of the right one (an index is -1 when the side has no line there).</summary>
		private readonly record struct Pair(DiffKind Kind, Int32 Left, Int32 Right);

		/// <summary>The lines of a text that take part in the comparison, each as its key (the line without what is ignored).</summary>
		/// <param name="Keys">The keys of the lines that are compared.</param>
		/// <param name="Kept">The index of each of these lines in the text; null when they are all of them.</param>
		private sealed record LineSet(IReadOnlyList<String> Keys, List<Int32>? Kept)
		{
			public static LineSet Create(IReadOnlyList<String> lines, ComparisonOptions? options)
			{
				if (options == null || options.KeepsLinesAsWritten)
					return new LineSet(lines, null);

				IReadOnlyList<String> text = options.IgnoreComments ? StripComments(lines) : lines;
				List<String> keys = new(lines.Count);
				List<Int32> kept = new(lines.Count);
				for (Int32 index = 0; index < lines.Count; index++)
				{
					Boolean blank = String.IsNullOrWhiteSpace(lines[index]);
					Boolean commentOnly = options.IgnoreComments && !blank && String.IsNullOrWhiteSpace(text[index]);
					if ((options.IgnoreBlankLines && blank) || commentOnly)
						continue;

					keys.Add(Normalize(text[index], options));
					kept.Add(index);
				}

				return new LineSet(keys, kept);
			}

			private static String Normalize(String line, ComparisonOptions options)
			{
				String key = options.IgnoreNumbers ? Digits.Replace(line, "0") : line;
				if (options.IgnoreWhitespace)
				{
					StringBuilder builder = new(key.Length);
					foreach (Char character in key)
					{
						if (!Char.IsWhiteSpace(character))
							builder.Append(character);
					}

					key = builder.ToString();
				}

				return options.IgnoreCase ? key.ToLowerInvariant() : key;
			}
		}

		/// <summary>How the lines of the keys face each other. The same start and the same end are matched first.</summary>
		private static List<Pair> Align(IReadOnlyList<String> left, IReadOnlyList<String> right, out Boolean exact)
		{
			exact = true;
			Int32 start = 0;
			while (start < left.Count && start < right.Count && String.Equals(left[start], right[start], StringComparison.Ordinal))
			{
				start++;
			}

			Int32 end = 0;
			while (end < left.Count - start && end < right.Count - start && String.Equals(left[left.Count - 1 - end], right[right.Count - 1 - end], StringComparison.Ordinal))
			{
				end++;
			}

			List<Pair> pairs = new(Math.Max(left.Count, right.Count));
			for (Int32 index = 0; index < start; index++)
			{
				pairs.Add(new Pair(DiffKind.Same, index, index));
			}

			Int32 leftCount = left.Count - start - end;
			Int32 rightCount = right.Count - start - end;
			List<Edit>? edits = leftCount == 0 || rightCount == 0 ? SimpleEdits(leftCount, rightCount) : Myers(left, right, start, leftCount, rightCount);
			if (edits == null)
			{
				exact = false;
				edits = SimpleEdits(leftCount, rightCount);
			}

			AddPairs(pairs, edits, start);

			for (Int32 index = 0; index < end; index++)
			{
				pairs.Add(new Pair(DiffKind.Same, left.Count - end + index, right.Count - end + index));
			}

			return pairs;
		}

		/// <summary>
		/// Everything is deleted then everything is inserted: when one side is empty that is the answer, and otherwise the lines of
		/// the two sides are replaced one by one (the rows pair them).
		/// </summary>
		private static List<Edit> SimpleEdits(Int32 leftCount, Int32 rightCount)
		{
			List<Edit> edits = [];
			for (Int32 index = 0; index < leftCount; index++)
			{
				edits.Add(new Edit(EditKind.Delete));
			}

			for (Int32 index = 0; index < rightCount; index++)
			{
				edits.Add(new Edit(EditKind.Insert));
			}

			return edits;
		}

		/// <summary>
		/// The shortest list of deletions and insertions that turns the middle of the left text into the middle of the right one
		/// (the algorithm of Myers); null when it needs more than <see cref="MaxDifferences"/>.
		/// </summary>
		private static List<Edit>? Myers(IReadOnlyList<String> left, IReadOnlyList<String> right, Int32 start, Int32 n, Int32 m)
		{
			Int32 limit = Math.Min(n + m, MaxDifferences);
			Int32 offset = limit + 1;
			Int32[] v = new Int32[2 * limit + 3];
			List<Int32[]> trace = [];

			Boolean found = false;
			for (Int32 d = 0; d <= limit && !found; d++)
			{
				// What the search knew before this distance (the part that can be reached: from -d to d).
				Int32[] snapshot = new Int32[2 * d + 3];
				Array.Copy(v, offset - d - 1, snapshot, 0, snapshot.Length);
				trace.Add(snapshot);

				for (Int32 k = -d; k <= d; k += 2)
				{
					Int32 x = k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]) ? v[offset + k + 1] : v[offset + k - 1] + 1;
					Int32 y = x - k;
					while (x < n && y < m && String.Equals(left[start + x], right[start + y], StringComparison.Ordinal))
					{
						x++;
						y++;
					}

					v[offset + k] = x;
					if (x >= n && y >= m)
					{
						found = true;
						break;
					}
				}
			}

			if (!found)
				return null;

			// From the end to the start: what the search did at each distance.
			List<Edit> reversed = [];
			Int32 endX = n;
			Int32 endY = m;
			for (Int32 d = trace.Count - 1; d >= 0; d--)
			{
				Int32[] before = trace[d];
				Int32 Value(Int32 k) => before[k + d + 1];
				Int32 diagonal = endX - endY;
				Int32 previousDiagonal = diagonal == -d || (diagonal != d && Value(diagonal - 1) < Value(diagonal + 1)) ? diagonal + 1 : diagonal - 1;
				Int32 previousX = d == 0 ? 0 : Value(previousDiagonal);
				Int32 previousY = d == 0 ? 0 : previousX - previousDiagonal;

				while (endX > previousX && endY > previousY)
				{
					reversed.Add(new Edit(EditKind.Equal));
					endX--;
					endY--;
				}

				if (d > 0)
					reversed.Add(new Edit(endX == previousX ? EditKind.Insert : EditKind.Delete));

				endX = previousX;
				endY = previousY;
			}

			reversed.Reverse();
			return reversed;
		}

		/// <summary>
		/// The pairs of a list of edits: equal lines face each other, and for a group of deletions and insertions the lines are
		/// paired (the first deleted with the first inserted...) as changed lines, the others face an empty place.
		/// </summary>
		private static void AddPairs(List<Pair> pairs, List<Edit> edits, Int32 start)
		{
			Int32 leftIndex = start;
			Int32 rightIndex = start;
			Int32 position = 0;

			while (position < edits.Count)
			{
				if (edits[position].Kind == EditKind.Equal)
				{
					pairs.Add(new Pair(DiffKind.Same, leftIndex++, rightIndex++));
					position++;
					continue;
				}

				Int32 deleted = 0;
				Int32 inserted = 0;
				while (position < edits.Count && edits[position].Kind != EditKind.Equal)
				{
					if (edits[position].Kind == EditKind.Delete)
					{
						deleted++;
					}
					else
					{
						inserted++;
					}

					position++;
				}

				Int32 paired = Math.Min(deleted, inserted);
				for (Int32 index = 0; index < paired; index++)
				{
					pairs.Add(new Pair(DiffKind.Changed, leftIndex++, rightIndex++));
				}

				for (Int32 index = paired; index < deleted; index++)
				{
					pairs.Add(new Pair(DiffKind.LeftOnly, leftIndex++, -1));
				}

				for (Int32 index = paired; index < inserted; index++)
				{
					pairs.Add(new Pair(DiffKind.RightOnly, -1, rightIndex++));
				}
			}
		}

		/// <summary>
		/// The rows of the pairs, with the text of each line as it is written; the lines that were left out of the comparison come
		/// back where they were, as ignored lines.
		/// </summary>
		private static List<DiffRow> BuildRows(IReadOnlyList<String> left, IReadOnlyList<String> right, List<Int32>? leftKept, List<Int32>? rightKept, List<Pair> pairs)
		{
			List<DiffRow> rows = new(pairs.Count);
			Int32 nextLeft = 0;
			Int32 nextRight = 0;

			foreach (Pair pair in pairs)
			{
				Int32 leftIndex = pair.Left < 0 ? -1 : leftKept == null ? pair.Left : leftKept[pair.Left];
				Int32 rightIndex = pair.Right < 0 ? -1 : rightKept == null ? pair.Right : rightKept[pair.Right];
				AddIgnored(rows, left, right, ref nextLeft, ref nextRight, leftIndex < 0 ? nextLeft : leftIndex, rightIndex < 0 ? nextRight : rightIndex);

				rows.Add(new DiffRow(
					pair.Kind,
					leftIndex < 0 ? 0 : leftIndex + 1,
					leftIndex < 0 ? String.Empty : left[leftIndex],
					rightIndex < 0 ? 0 : rightIndex + 1,
					rightIndex < 0 ? String.Empty : right[rightIndex]));

				if (leftIndex >= 0)
					nextLeft = leftIndex + 1;

				if (rightIndex >= 0)
					nextRight = rightIndex + 1;
			}

			AddIgnored(rows, left, right, ref nextLeft, ref nextRight, left.Count, right.Count);
			return rows;
		}

		/// <summary>The lines before the given ones that were not compared, face to face as far as both sides have some.</summary>
		private static void AddIgnored(List<DiffRow> rows, IReadOnlyList<String> left, IReadOnlyList<String> right, ref Int32 nextLeft, ref Int32 nextRight, Int32 untilLeft, Int32 untilRight)
		{
			while (nextLeft < untilLeft || nextRight < untilRight)
			{
				Boolean hasLeft = nextLeft < untilLeft;
				Boolean hasRight = nextRight < untilRight;
				rows.Add(new DiffRow(DiffKind.Ignored, hasLeft ? nextLeft + 1 : 0, hasLeft ? left[nextLeft] : String.Empty, hasRight ? nextRight + 1 : 0, hasRight ? right[nextRight] : String.Empty));
				nextLeft += hasLeft ? 1 : 0;
				nextRight += hasRight ? 1 : 0;
			}
		}
	}
}