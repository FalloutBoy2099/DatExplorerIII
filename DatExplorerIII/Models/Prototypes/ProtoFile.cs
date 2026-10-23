namespace DatExplorerIII.Models.Prototypes
{
	using System;

	/// <summary>
	/// The description of an object of the game, read from a prototype file (.pro): the numbers every kind of object has,
	/// and, in the classes that derive from it, those of each kind.
	/// </summary>
	public abstract class ProtoFile
	{
		/// <summary>The kind of object.</summary>
		public abstract ProtoType Type { get; }

		/// <summary>The identifier of the object: its kind in the high byte, its number in the others.</summary>
		public Int32 Pid { get; init; }

		/// <summary>The number of the object among those of its kind.</summary>
		public Int32 Id => Pid & 0x00FFFFFF;

		/// <summary>The number of the name of the object in the message file of its kind (the description follows it).</summary>
		public Int32 MessageId { get; init; }

		/// <summary>The identifier of the picture of the object (see <c>.frm</c> files).</summary>
		public Int32 Fid { get; init; }

		/// <summary>How far the object lights.</summary>
		public Int32 LightDistance { get; init; }

		/// <summary>How much the object lights.</summary>
		public Int32 LightIntensity { get; init; }

		public Int32 Flags { get; init; }
	}
}