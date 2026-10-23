namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Models.Archives;

	/// <summary>How a file of two archives compares.</summary>
	public enum DatCompareState
	{
		/// <summary>The file is in both archives and has the same content.</summary>
		Identical,

		/// <summary>The file is in both archives and its content is not the same.</summary>
		Different,

		/// <summary>The file is only in the left archive.</summary>
		LeftOnly,

		/// <summary>The file is only in the right archive.</summary>
		RightOnly,
	}

	/// <summary>One file of the comparison of two archives.</summary>
	/// <param name="Path">The path of the file in the archives (the case of the left one when both have it).</param>
	/// <param name="State">How the file compares.</param>
	/// <param name="Left">The file in the left archive; null when the right one only has it.</param>
	/// <param name="Right">The file in the right archive; null when the left one only has it.</param>
	public sealed record DatCompareItem(String Path, DatCompareState State, DatEntry? Left, DatEntry? Right);

	/// <summary>
	/// Compares the files of two archives (the archives may have been made from folders): which files both have, with the same
	/// content or not, and which only one has.
	/// </summary>
	public static class DatComparer
	{
		/// <summary>How many pairs of files are read and compared at the same time (the files can be large).</summary>
		private const Int32 MaxParallelism = 4;

		/// <summary>
		/// Compares two archives. Paths are compared without regard to case. Two files with the same size are read to see
		/// if they are the same; a file that cannot be read counts as different. The files made of text are read whatever their
		/// size when <paramref name="text"/> tells how to compare them (they may differ in what the user ignores).
		/// </summary>
		/// <returns>One item for every path of either archive, ordered by path.</returns>
		/// <exception cref="OperationCanceledException">The comparison was cancelled.</exception>
		public static IReadOnlyList<DatCompareItem> Compare(DatArchive left, DatArchive right, IProgress<DatProgress>? progress = null, CancellationToken cancellation = default, TextEquality? text = null)
		{
			Dictionary<String, DatEntry> leftByKey = Index(left);
			Dictionary<String, DatEntry> rightByKey = Index(right);

			List<String> keys = leftByKey.Keys.Union(rightByKey.Keys).OrderBy(key => key, StringComparer.Ordinal).ToList();
			DatCompareItem[] items = new DatCompareItem[keys.Count];
			List<Int32> toRead = [];

			for (Int32 index = 0; index < keys.Count; index++)
			{
				leftByKey.TryGetValue(keys[index], out DatEntry? leftEntry);
				rightByKey.TryGetValue(keys[index], out DatEntry? rightEntry);

				if (leftEntry != null && rightEntry != null)
				{
					// Not the same size: not the same file, no need to read them (unless a text may differ only in what is ignored).
					Boolean isText = text != null && text.AppliesTo(leftEntry.Name);
					DatCompareState state = leftEntry.UnpackedSize != rightEntry.UnpackedSize && !isText ? DatCompareState.Different : DatCompareState.Identical;
					items[index] = new DatCompareItem(leftEntry.FullPath, state, leftEntry, rightEntry);
					if (state == DatCompareState.Identical)
						toRead.Add(index);
				}
				else
				{
					DatEntry only = (leftEntry ?? rightEntry)!;
					items[index] = new DatCompareItem(only.FullPath, leftEntry != null ? DatCompareState.LeftOnly : DatCompareState.RightOnly, leftEntry, rightEntry);
				}
			}

			DatProgressThrottle throttle = new(progress, toRead.Count);
			Int32 done = 0;
			ParallelOptions options = new() { MaxDegreeOfParallelism = Math.Min(MaxParallelism, Environment.ProcessorCount), CancellationToken = cancellation };

			Parallel.ForEach(toRead, options, index =>
			{
				DatCompareItem item = items[index];
				if (!ContentEquals(left, item.Left!, right, item.Right!, text))
					items[index] = item with { State = DatCompareState.Different };

				throttle.Report(item.Path, Interlocked.Increment(ref done));
			});

			return items;
		}

		/// <summary>
		/// Whether two files have the same content (for the files made of text, as <paramref name="text"/> says). Two files stored with the
		/// same compressed bytes are the same without being inflated; otherwise a file that cannot be read is not the same as another.
		/// </summary>
		public static Boolean ContentEquals(DatArchive left, DatEntry leftEntry, DatArchive right, DatEntry rightEntry, TextEquality? text = null)
		{
			Boolean isText = text != null && text.AppliesTo(leftEntry.Name);
			if ((leftEntry.UnpackedSize != rightEntry.UnpackedSize && !isText) || leftEntry.IsDamaged || rightEntry.IsDamaged)
				return false;

			try
			{
				Byte[] leftData;
				Byte[] rightData;
				if (SameCompression(left, leftEntry, right, rightEntry))
				{
					// The same compressed bytes are the same file: most files that did not change are found without inflating them.
					Byte[] leftStored = left.ReadStored(leftEntry);
					Byte[] rightStored = right.ReadStored(rightEntry);
					if (leftStored.AsSpan().SequenceEqual(rightStored))
						return true;

					leftData = left.Unpack(leftEntry, leftStored);
					rightData = right.Unpack(rightEntry, rightStored);
				}
				else
				{
					leftData = left.ReadData(leftEntry);
					rightData = right.ReadData(rightEntry);
				}

				return leftData.AsSpan().SequenceEqual(rightData) || (isText && text!.AreEqual(new ComparedFile(leftEntry.Name, left.Format, leftData), new ComparedFile(rightEntry.Name, right.Format, rightData)));
			}
			catch (Exception exception) when (exception is DatException or IOException or UnauthorizedAccessException or InvalidDataException)
			{
				return false;
			}
		}

		/// <summary>
		/// True when two stored files are compressed the same way with the same compressed size: equal compressed bytes then mean equal
		/// files (the files of a ZIP are read by the ZIP, not as stored bytes).
		/// </summary>
		private static Boolean SameCompression(DatArchive left, DatEntry leftEntry, DatArchive right, DatEntry rightEntry)
		{
			return leftEntry is { IsNew: false, IsCompressed: true }
				&& rightEntry is { IsNew: false, IsCompressed: true }
				&& leftEntry.PackedSize == rightEntry.PackedSize
				&& left.Format != DatFormat.Zip
				&& right.Format != DatFormat.Zip
				&& (left.Format == DatFormat.Fallout1) == (right.Format == DatFormat.Fallout1);
		}

		/// <summary>The files of an archive by lower case path (the first one when two have the same path).</summary>
		private static Dictionary<String, DatEntry> Index(DatArchive archive)
		{
			Dictionary<String, DatEntry> byKey = [with(StringComparer.Ordinal)];
			foreach (DatEntry entry in archive.Entries)
			{
				byKey.TryAdd(entry.Key, entry);
			}

			return byKey;
		}
	}
}