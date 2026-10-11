namespace DatExplorerIII.Models
{
	using System;

	/// <summary>The numbers of a door.</summary>
	/// <param name="OpenFlags">How it opens.</param>
	/// <param name="KeyCode">The code of the key that opens it, or -1.</param>
	public sealed record DoorData(Int32 OpenFlags, Int32 KeyCode);

	/// <summary>The numbers of stairs.</summary>
	/// <param name="DestinationTile">The tile they lead to.</param>
	/// <param name="DestinationMap">The map they lead to, or -1.</param>
	public sealed record StairsData(Int32 DestinationTile, Int32 DestinationMap);

	/// <summary>The numbers of an elevator.</summary>
	/// <param name="ElevatorType">Which elevator it is (its buttons).</param>
	/// <param name="Level">The level it stops at.</param>
	public sealed record ElevatorData(Int32 ElevatorType, Int32 Level);

	/// <summary>
	/// A piece of scenery: a door, stairs, an elevator, a ladder or something that is only there to be seen. Only the data
	/// of its kind is set.
	/// </summary>
	public sealed class SceneryProto : ProtoFile
	{
		public override ProtoType Type => ProtoType.Scenery;

		public Int32 ExtendedFlags { get; init; }

		/// <summary>The script of the scenery, or -1.</summary>
		public Int32 ScriptId { get; init; }

		public SceneryType SceneryType { get; init; }

		public Int32 Material { get; init; }

		/// <summary>The letter that makes the name of its sounds.</summary>
		public Char SoundId { get; init; }

		public DoorData? Door { get; init; }

		public StairsData? Stairs { get; init; }

		public ElevatorData? Elevator { get; init; }

		/// <summary>The tile a ladder leads to.</summary>
		public Int32? LadderDestination { get; init; }
	}
}