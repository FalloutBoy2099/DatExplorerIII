namespace DatExplorerIII.Scripts
{
	using System;

	/// <summary>
	/// A line of the table of procedures of the compiled script.
	/// </summary>
	internal sealed class ProcedureDescriptor
	{
		public UInt32 NameOffset { get; set; }
		public UInt32 Type { get; set; }
		public UInt32 Time { get; set; }
		public UInt32 ExpressionOffset { get; set; }
		public UInt32 BodyOffset { get; set; }
		public UInt32 ArgumentCount { get; set; }

		public static ProcedureDescriptor Read(BinaryScriptReader reader)
		{
			return new ProcedureDescriptor
			{
				NameOffset = reader.ReadUInt32(),
				Type = reader.ReadUInt32(),
				Time = reader.ReadUInt32(),
				ExpressionOffset = reader.ReadUInt32(),
				BodyOffset = reader.ReadUInt32(),
				ArgumentCount = reader.ReadUInt32()
			};
		}
	}
}