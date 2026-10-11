namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	internal sealed partial class Decompilation
	{
		/// <summary>
		/// Reads the whole file: the startup code, the tables of procedures, names and strings, the variables the script exports and
		/// imports, and the code of each procedure as a list of opcodes.
		/// </summary>
		public void Read()
		{
			ReadStartupCode();
			_procedures.Read(_reader);
			_namespace.Read(_reader);
			_strings.Read(_reader);

			ScriptOpcode opcode = ScriptOpcode.ReadExpected(_reader, Opcode.O_SET_GLOBAL);
			UInt32 currentOffset = _reader.Position;
			List<ScriptOpcode> headerTail = [];

			while (currentOffset < _procedures.OffsetOfProcedureSection)
			{
				opcode = ScriptOpcode.Read(_reader);
				currentOffset += (UInt32)opcode.Size;
				headerTail.Add(opcode);
			}

			CheckStartupTail(headerTail);
			ExtractCodeElements(headerTail, _exportedVariables, Opcode.O_EXPORT_VAR, 2, IsExportVariableCode, "Error: Malformed \"Export var\" section");
			ExtractCodeElements(headerTail, _exportedVariableValues, Opcode.O_STORE_EXTERNAL, 3, IsSetExportedVariableValueCode, "Error: Malformed \"Set exported var values\" section");
			ExtractCodeElements(headerTail, _exportedProcedures, Opcode.O_EXPORT_PROC, 3, IsExportProcedureCode, "Error: Malformed \"Export procedures\" section");

			for (Int32 index = 0; index < headerTail.Count; index++)
			{
				Opcode op = headerTail[index].Operator;
				if (op != Opcode.O_STRINGOP && op != Opcode.O_FLOATOP && op != Opcode.O_INTOP)
				{
					throw new ScriptFormatException("Error: Malformed \"Global variables\" section");
				}

				_globalVariables.Add(headerTail[index]);
			}

			_procedureBodies.Clear();
			_conditions.Clear();

			for (UInt32 index = 0; index < _procedures.Count; index++)
			{
				List<Node> body = [];
				UInt32 offset = _procedures[index].BodyOffset;
				UInt32 size = _procedures.GetProcedureSize(index);
				_reader.Seek(offset);

				while (offset < _procedures[index].BodyOffset + size)
				{
					ThrowIfCancelled();
					Node node = new();
					node.Opcode = ScriptOpcode.Read(_reader);
					node.Offset = offset;
					body.Add(node);
					offset += (UInt32)node.Opcode.Size;
				}

				_procedureBodies.Add(body);
				_conditions.Add([]);
			}
		}

		/// <summary>
		/// Checks the fixed code every compiled script starts with (the same opcodes in the same order).
		/// </summary>
		private void ReadStartupCode()
		{
			Opcode[] expected =
			{
				Opcode.O_CRITICAL_START, Opcode.O_INTOP, Opcode.O_D_TO_A, Opcode.O_INTOP, Opcode.O_JMP,
				Opcode.O_EXIT_PROG, Opcode.O_POP, Opcode.O_POP_FLAGS_RETURN, Opcode.O_POP, Opcode.O_POP_FLAGS_EXIT,
				Opcode.O_POP, Opcode.O_POP_FLAGS_RETURN_EXTERN, Opcode.O_POP, Opcode.O_POP_FLAGS_EXIT_EXTERN,
				Opcode.O_POP_FLAGS_RETURN_VAL_EXTERN, Opcode.O_POP_FLAGS_RETURN_VAL_EXIT, Opcode.O_POP_FLAGS_RETURN_VAL_EXIT_EXTERN
			};

			for (Int32 index = 0; index < expected.Length; index++)
			{
				if (index == 1)
				{
					ScriptOpcode.ReadExpected(_reader, expected[index], true, 18);
				}
				else
				{
					ScriptOpcode.ReadExpected(_reader, expected[index]);
				}
			}
		}

		/// <summary>
		/// Looks at what ends the code of the header (the jump to <c>start</c>, the number of its arguments), and warns when it is not as
		/// the compilers write it.
		/// </summary>
		private void CheckStartupTail(List<ScriptOpcode> headerTail)
		{
			if (headerTail.Count > 0)
			{
				Int32 startIndex = GetIndexOfProcedure("start");
				UInt32 startProcedureAddress = startIndex != -1 ? _procedures[(UInt32)startIndex].BodyOffset : 18U;
				ScriptOpcode opcode = headerTail[headerTail.Count - 1];
				if (opcode.Operator == Opcode.O_JMP)
				{
					headerTail.RemoveAt(headerTail.Count - 1);
					if (headerTail.Count == 0)
					{
						_messages.Add("Warning: Omitted address of jump");
					}
					else
					{
						opcode = headerTail[headerTail.Count - 1];
						if (opcode.Operator == Opcode.O_INTOP)
						{
							headerTail.RemoveAt(headerTail.Count - 1);
							if (opcode.Argument != startProcedureAddress)
							{
								_messages.Add("Warning: Invalid jump address");
							}
						}
						else
						{
							_messages.Add("Warning: Invalid opcode for jump addres");
						}
					}
				}
				else
				{
					_messages.Add("Warning: Omitted  \"Jump to 'start' procedure\" / \"Jump to end of statup code\"");
				}
			}

			if (headerTail.Count > 0)
			{
				ScriptOpcode opcode = headerTail[headerTail.Count - 1];
				if (opcode.Operator == Opcode.O_CRITICAL_DONE)
				{
					headerTail.RemoveAt(headerTail.Count - 1);
					if (headerTail.Count > 0 && headerTail[headerTail.Count - 1].HasArgument)
					{
						headerTail.RemoveAt(headerTail.Count - 1);
					}
					else
					{
						_messages.Add("Warning: Omitted  \"# of argument to 'start' procedure\"");
					}
				}
				else
				{
					_messages.Add("Warning: Omitted expected opcode 0x8003. Opcode with # of arguments will be not analysed");
				}
			}
			else
			{
				_messages.Add("Warning: Omitted  \"# of argument to 'start' procedure\"");
			}
		}

		/// <summary>
		/// Takes out of the header code the groups of opcodes that end with <paramref name="delimiter"/> (each is <paramref name="itemSize"/>
		/// opcodes long) and puts their operands in <paramref name="destination"/>.
		/// </summary>
		private static void ExtractCodeElements(List<ScriptOpcode> source, List<ScriptOpcode> destination, Opcode delimiter, UInt32 itemSize, Func<Opcode, Int32, Boolean> check, String errorMessage)
		{
			Int32 index = 0;
			for (; index < source.Count; index++)
			{
				if (source[index].Operator == delimiter)
				{
					break;
				}
			}

			if (index >= source.Count)
			{
				return;
			}

			if (index < itemSize - 1)
			{
				throw new ScriptFormatException(errorMessage);
			}

			while (index < source.Count && source[index].Operator == delimiter)
			{
				for (Int32 checkIndex = 0; checkIndex < itemSize - 1; checkIndex++)
				{
					if (!check(source[index - (Int32)itemSize + 1 + checkIndex].Operator, checkIndex))
					{
						throw new ScriptFormatException(errorMessage);
					}
				}

				Int32 removeOffset = index - (Int32)itemSize + 1;
				for (Int32 copyIndex = 0; copyIndex < itemSize - 1; copyIndex++)
				{
					destination.Add(source[removeOffset]);
					source.RemoveAt(removeOffset);
				}

				source.RemoveAt(removeOffset);
				if (index + 1 > source.Count)
				{
					break;
				}
			}
		}

		private static Boolean IsExportVariableCode(Opcode opcode, Int32 index)
		{
			return index == 0 && opcode == Opcode.O_STRINGOP;
		}

		private static Boolean IsSetExportedVariableValueCode(Opcode opcode, Int32 index)
		{
			return index == 0
				? opcode == Opcode.O_STRINGOP || opcode == Opcode.O_FLOATOP || opcode == Opcode.O_INTOP
				: index == 1 && opcode == Opcode.O_STRINGOP;
		}

		private static Boolean IsExportProcedureCode(Opcode opcode, Int32 index)
		{
			return (index == 0 || index == 1) && opcode == Opcode.O_INTOP;
		}
	}
}