namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	/// <summary>What an opcode is in the source of a script.</summary>
	internal enum OpcodeAttributeType
	{
		/// <summary>A line of its own (<c>critter_heal(self_obj, 5);</c>).</summary>
		Statement,

		/// <summary>A value, used inside another expression (<c>obj_name(who)</c>).</summary>
		Expression,

		/// <summary>A call that is a value when something uses it, and a line when nothing does.</summary>
		ExpressionStatement,
	}

	/// <summary>Where the operator is written relative to its operands.</summary>
	internal enum OpcodeAttributeCategory
	{
		/// <summary>Before them, as a call: <c>name(a, b)</c>.</summary>
		Prefix,

		/// <summary>Between them: <c>a + b</c>.</summary>
		Infix,
	}

	/// <summary>How the decompiler writes an opcode.</summary>
	internal sealed class OpcodeAttributes
	{
		public OpcodeAttributes(String name, UInt32 argumentCount, OpcodeAttributeType type, OpcodeAttributeCategory category, UInt32[] procedureArguments)
		{
			Name = name;
			ArgumentCount = argumentCount;
			Type = type;
			Category = category;
			ProcedureArguments = procedureArguments;
		}

		/// <summary>What is written in the source: the name of the function, or the operator.</summary>
		public String Name { get; }

		/// <summary>How many values the opcode takes from the stack.</summary>
		public UInt32 ArgumentCount { get; }

		public OpcodeAttributeType Type { get; }

		public OpcodeAttributeCategory Category { get; }

		/// <summary>The positions of the arguments that are the names of procedures.</summary>
		public UInt32[] ProcedureArguments { get; }
	}

	/// <summary>Fills a table of opcodes: a line for each opcode, named by its kind.</summary>
	internal sealed class OpcodeAttributesBuilder
	{
		public Dictionary<Opcode, OpcodeAttributes> Attributes { get; } = [];

		/// <summary>An opcode that is a line of its own.</summary>
		public void Statement(Opcode opcode, String name, UInt32 argumentCount, params UInt32[] procedureArguments)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Statement, OpcodeAttributeCategory.Prefix, procedureArguments);
		}

		/// <summary>An opcode that is a value.</summary>
		public void Expression(Opcode opcode, String name, UInt32 argumentCount, params UInt32[] procedureArguments)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Expression, OpcodeAttributeCategory.Prefix, procedureArguments);
		}

		/// <summary>An operator written between its two values.</summary>
		public void Infix(Opcode opcode, String name, UInt32 argumentCount)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Expression, OpcodeAttributeCategory.Infix, []);
		}

		/// <summary>An opcode that is a value, or a line when nothing uses its value.</summary>
		public void ExpressionStatement(Opcode opcode, String name, UInt32 argumentCount)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.ExpressionStatement, OpcodeAttributeCategory.Prefix, []);
		}

		private void Add(Opcode opcode, String name, UInt32 argumentCount, OpcodeAttributeType type, OpcodeAttributeCategory category, UInt32[] procedureArguments)
		{
			Attributes[opcode] = new OpcodeAttributes(name, argumentCount, type, category, procedureArguments);
		}
	}
}