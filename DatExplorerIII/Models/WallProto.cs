namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A wall.
	/// </summary>
	public sealed class WallProto : ProtoFile
	{
		public override ProtoType Type => ProtoType.Wall;

		public Int32 ExtendedFlags { get; init; }

		/// <summary>The script of the wall, or -1.</summary>
		public Int32 ScriptId { get; init; }

		public Int32 Material { get; init; }
	}
}