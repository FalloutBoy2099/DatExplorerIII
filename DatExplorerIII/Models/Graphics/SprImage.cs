namespace DatExplorerIII.Models.Graphics
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A picture of a sprite of Fallout Tactics and where it is drawn: the offset of its top left corner from the origin of the sprite
	/// (the point where the character stands), so that the pictures of an animation stay aligned.
	/// </summary>
	/// <param name="OffsetX">Distance of the left of the picture from the origin.</param>
	/// <param name="OffsetY">Distance of the top of the picture from the origin.</param>
	/// <param name="Image">The picture (without pixels for the frames that a collection does not hold).</param>
	public sealed record SprFrame(Int32 OffsetX, Int32 OffsetY, ZarImage Image);

	/// <summary>
	/// A named animation of a sprite ("Stand", "Walk"...): which frames of its collection are shown, in order.
	/// </summary>
	/// <param name="Name">The name of the animation.</param>
	/// <param name="CollectionIndex">The collection of pictures that it draws from.</param>
	/// <param name="Frames">The numbers (from 0) of the frames, in the order they are played.</param>
	public sealed record SprSequence(String Name, Int32 CollectionIndex, IReadOnlyList<Int32> Frames);

	/// <summary>
	/// The pictures of a sprite for every direction and frame; the four layers of the file are already drawn one over the other.
	/// </summary>
	public sealed class SprCollection
	{
		/// <param name="frames">The pictures, frame after frame, the directions of a frame one after the other.</param>
		public SprCollection(String name, Int32 directions, Int32 framesPerDirection, IReadOnlyList<SprFrame> frames)
		{
			Name = name;
			Directions = directions;
			FramesPerDirection = framesPerDirection;
			Frames = frames;

			// The area that holds every picture, so that the sprite does not move when the direction or the frame changes.
			Int32 left = Int32.MaxValue, top = Int32.MaxValue, right = Int32.MinValue, bottom = Int32.MinValue;
			foreach (SprFrame frame in frames)
			{
				if (frame.Image.Width == 0 || frame.Image.Height == 0)
					continue;

				left = Math.Min(left, frame.OffsetX);
				top = Math.Min(top, frame.OffsetY);
				right = Math.Max(right, frame.OffsetX + frame.Image.Width);
				bottom = Math.Max(bottom, frame.OffsetY + frame.Image.Height);
			}

			if (left > right)
				left = top = right = bottom = 0;

			Left = left;
			Top = top;
			Width = Math.Max(1, right - left);
			Height = Math.Max(1, bottom - top);
		}

		public String Name { get; }

		public Int32 Directions { get; }

		public Int32 FramesPerDirection { get; }

		/// <summary>Where the area of the pictures starts, from the origin of the sprite.</summary>
		public Int32 Left { get; }

		public Int32 Top { get; }

		/// <summary>The size of the area that holds every picture.</summary>
		public Int32 Width { get; }

		public Int32 Height { get; }

		public IReadOnlyList<SprFrame> Frames { get; }

		public SprFrame GetFrame(Int32 direction, Int32 frame)
		{
			return Frames[(frame * Directions) + direction];
		}
	}

	/// <summary>
	/// A sprite of Fallout Tactics (.spr): named animations that draw from collections of pictures. A collection is decoded when it is
	/// asked for.
	/// </summary>
	public sealed class SprFile
	{
		private readonly Func<Int32, SprCollection> _loader;
		private readonly Dictionary<Int32, SprCollection> _collections = [];

		/// <param name="sequences">The animations that can be shown (the ones whose collection and frames exist).</param>
		/// <param name="collectionCount">How many collections the file holds.</param>
		/// <param name="loader">Decodes a collection from its number.</param>
		public SprFile(IReadOnlyList<SprSequence> sequences, Int32 collectionCount, Func<Int32, SprCollection> loader)
		{
			Sequences = sequences;
			CollectionCount = collectionCount;
			_loader = loader;
		}

		public IReadOnlyList<SprSequence> Sequences { get; }

		public Int32 CollectionCount { get; }

		/// <summary>The animation shown first: "Stand", else one that starts with "StandBreathe", else the first one.</summary>
		public Int32 DefaultSequence
		{
			get
			{
				for (Int32 i = 0; i < Sequences.Count; i++)
				{
					if (Sequences[i].Name.Equals("Stand", StringComparison.OrdinalIgnoreCase))
						return i;
				}

				for (Int32 i = 0; i < Sequences.Count; i++)
				{
					if (Sequences[i].Name.StartsWith("StandBreathe", StringComparison.OrdinalIgnoreCase))
						return i;
				}

				return 0;
			}
		}

		/// <exception cref="System.IO.InvalidDataException">The collection is damaged.</exception>
		public SprCollection GetCollection(Int32 index)
		{
			if (!_collections.TryGetValue(index, out SprCollection? collection))
			{
				collection = _loader(index);
				_collections[index] = collection;
			}

			return collection;
		}
	}
}
