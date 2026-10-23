namespace DatExplorerIII.Models.Prototypes
{
	using System;

	/// <summary>
	/// A miscellaneous object (an exit grid, a marker...).
	/// </summary>
	public sealed class MiscProto : ProtoFile
	{
		public override ProtoType Type => ProtoType.Misc;

		public Int32 ExtendedFlags { get; init; }
	}
}