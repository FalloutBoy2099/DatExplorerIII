namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;

	/// <summary>A place of a map that leads to another map (.jmp).</summary>
	/// <param name="Flags">The flags of the jump point (0 in practice).</param>
	/// <param name="SourceX">The tile where the jump point is.</param>
	/// <param name="SourceY">The tile where the jump point is.</param>
	/// <param name="DestinationMap">The number of the map it leads to.</param>
	/// <param name="DestinationX">The tile where it leads to.</param>
	/// <param name="DestinationY">The tile where it leads to.</param>
	public sealed record JumpPoint(UInt32 Flags, Int32 SourceX, Int32 SourceY, Int32 DestinationMap, Int32 DestinationX, Int32 DestinationY);

	/// <summary>The jump points of a map of Arcanum (.jmp).</summary>
	public sealed record JmpFile(IReadOnlyList<JumpPoint> Jumps);

	/// <summary>A file or a folder of the index of a saved game of Arcanum (.tfai).</summary>
	/// <param name="Name">The name of the file or the folder.</param>
	/// <param name="Size">The size of the file in the data (.tfaf); 0 for a folder.</param>
	/// <param name="Children">What a folder holds (empty for a file).</param>
	/// <param name="IsFolder">True for a folder.</param>
	public sealed record TfaiNode(String Name, Int32 Size, IReadOnlyList<TfaiNode> Children, Boolean IsFolder);

	/// <summary>The index of the files of a saved game (.tfai): the data (.tfaf) is these files one after the other, in this order.</summary>
	public sealed record TfaiFile(IReadOnlyList<TfaiNode> Root);

	/// <summary>The information of a saved game of Arcanum (.gsi), the one that the list of the saved games shows.</summary>
	public sealed record GsiFile(
		Int32 Version,
		String ModuleName,
		String LeaderName,
		String DisplayName,
		Int32 MapId,
		Int32 GameTimeDays,
		Int32 GameTimeMs,
		Int32 LeaderPortrait,
		Int32 LeaderLevel,
		Int32 LeaderTileX,
		Int32 LeaderTileY,
		Int32 StoryState);

	/// <summary>The fog of a town map (.tmf): a bit for each tile of the map, set when the tile was discovered.</summary>
	/// <param name="Bits">The bits as they are in the file.</param>
	public sealed record TmfFile(Byte[] Bits)
	{
		public Int32 TotalTiles => Bits.Length * 8;

		public Int32 RevealedTiles
		{
			get
			{
				Int32 count = 0;
				foreach (Byte value in Bits)
				{
					count += System.Numerics.BitOperations.PopCount(value);
				}

				return count;
			}
		}
	}
}
