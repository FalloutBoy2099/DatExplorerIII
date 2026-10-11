namespace DatExplorerIII.Scripts
{
	using System;

	/// <summary>
	/// What a name of the namespace stands for: a procedure (<see cref="Data"/> is its number) or a variable (its value).
	/// </summary>
	internal sealed class DefinitionObject
	{
		public DefinitionObject(DefinitionObjectType type, UInt32 attributes, UInt32 data)
		{
			Type = type;
			Attributes = attributes;
			Data = data;
		}

		public DefinitionObjectType Type { get; }
		public UInt32 Attributes { get; }
		public UInt32 Data { get; }
	}

	internal enum DefinitionObjectType
	{
		Variable,
		Procedure
	}

	/// <summary>
	/// The flags of the type of a procedure, and of a variable.
	/// </summary>
	internal static class ObjectAttributes
	{
		public const UInt32 Timed = 0x01;
		public const UInt32 Conditional = 0x02;
		public const UInt32 Import = 0x04;
		public const UInt32 Export = 0x08;
		public const UInt32 Critical = 0x10;
		public const UInt32 NotImplemented = 0x80000000;
		public const UInt32 Global = 0x00010000;
	}

	/// <summary>
	/// The kinds of line of the list of declarations, which are written in groups.
	/// </summary>
	internal enum DefinitionOutputType
	{
		Nothing,
		UnknownVariable,
		GlobalVariable,
		ExportedVariable,
		ImportedVariable,
		Procedure,
		ExportedProcedure,
		ImportedProcedure
	}
}