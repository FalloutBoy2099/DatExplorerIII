namespace DatExplorerIII.Formats.Movies
{
	using System;
	using System.Collections.Generic;
	using Models.Graphics;

	/// <summary>
	/// The state of the decoding of a movie just before a picture: where to go on in the segments, the palette and the
	/// pictures the next ones are made from. It makes it possible to start from there instead of from the beginning.
	/// </summary>
	internal sealed record MveCheckpoint(Int32 Frame, Int32 SegmentIndex, Int32 MapOffset, Int32 MapLength, UInt32[] Palette, Byte[] Current, Byte[] Last, Byte[] SecondLast);

	/// <summary>The saved states of a movie, found by the number of the picture. Readers on several threads can share it.</summary>
	internal sealed class MveCheckpoints
	{
		private readonly SortedList<Int32, MveCheckpoint> _items = [];
		private readonly Object _lock = new();

		public Boolean Contains(Int32 frame)
		{
			lock (_lock)
			{
				return _items.ContainsKey(frame);
			}
		}

		public void Add(MveCheckpoint checkpoint)
		{
			lock (_lock)
			{
				_items.TryAdd(checkpoint.Frame, checkpoint);
			}
		}

		/// <summary>The last saved state that is not after a picture, or null when there is none.</summary>
		public MveCheckpoint? FindAtOrBefore(Int32 frame)
		{
			lock (_lock)
			{
				MveCheckpoint? best = null;
				foreach (KeyValuePair<Int32, MveCheckpoint> item in _items)
				{
					if (item.Key > frame)
						break;

					best = item.Value;
				}

				return best;
			}
		}
	}
}