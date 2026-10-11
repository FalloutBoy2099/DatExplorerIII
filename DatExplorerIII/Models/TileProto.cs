namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A tile of the floor or of the roof.
	/// </summary>
	public sealed class TileProto : ProtoFile
	{
		public override ProtoType Type => ProtoType.Tile;

		public Int32 Material { get; init; }
	}
}