namespace DatExplorerIII.Scripts
{
	using System;
	using System.Globalization;

	/// <summary>
	/// An opcode of the compiled code, with its argument when it has one (a number, a float or an offset in the strings).
	/// </summary>
	internal readonly struct ScriptOpcode
	{
		public const UInt32 OperatorSize = 2;
		public const UInt32 ArgumentSize = 4;

		public ScriptOpcode(Opcode op, UInt32 argument)
		{
			Operator = op;
			Argument = argument;
		}

		public Opcode Operator { get; }
		public UInt32 Argument { get; }
		public Boolean HasArgument => Operator == Opcode.O_STRINGOP || Operator == Opcode.O_FLOATOP || Operator == Opcode.O_INTOP;
		public Int32 Size => HasArgument ? 6 : 2;

		public static ScriptOpcode Read(BinaryScriptReader reader)
		{
			UInt32 offset = reader.Position;
			Opcode op = (Opcode)reader.ReadUInt16();
			if (((UInt16)op < (UInt16)Opcode.O_OPERATOR) ||
				((UInt16)op >= (UInt16)Opcode.O_END_OP && op != Opcode.O_STRINGOP && op != Opcode.O_FLOATOP && op != Opcode.O_INTOP))
			{
				throw new ScriptFormatException(String.Format(CultureInfo.InvariantCulture, "Error: Invalid opcode at 0x{0:X8}", offset));
			}

			UInt32 argument = 0;
			if (op == Opcode.O_STRINGOP || op == Opcode.O_FLOATOP || op == Opcode.O_INTOP)
			{
				argument = reader.ReadUInt32();
			}

			return new ScriptOpcode(op, argument);
		}

		public static ScriptOpcode ReadExpected(BinaryScriptReader reader, Opcode expected, Boolean argumentFound = false, UInt32 expectedArgument = 0)
		{
			ScriptOpcode opcode = Read(reader);
			if (opcode.Operator != expected)
			{
				throw new ScriptFormatException(String.Format(CultureInfo.InvariantCulture, "Error: Unexpected opcode (0x{0:X4} expected, but 0x{1:X4} found)", (UInt16)expected, (UInt16)opcode.Operator));
			}

			if (argumentFound && opcode.HasArgument && opcode.Argument != expectedArgument)
			{
				throw new ScriptFormatException(String.Format(CultureInfo.InvariantCulture, "Error: Unexpected argument of opcode. (0x{0:X8} expected, but 0x{1:X8} found)", expectedArgument, opcode.Argument));
			}

			return opcode;
		}

		public ScriptOpcode WithOperator(Opcode op)
		{
			return new ScriptOpcode(op, Argument);
		}

		public OpcodeAttributes GetAttributes(ScriptDecompilerOptions options)
		{
			return OpcodeTable.GetAttributes(Operator, options.Game);
		}
	}
}