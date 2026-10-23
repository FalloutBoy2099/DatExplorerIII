namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	internal sealed partial class Decompilation
	{
		/// <summary>
		/// Turns the opcodes of every procedure into trees: removes the code the compiler adds, builds the expressions, the conditions of
		/// the procedures that have one, and the blocks.
		/// </summary>
		public void ProcessCode()
		{
			InitialReduce();
			for (Int32 index = 0; index < _procedureBodies.Count; index++)
			{
				BuildTree(_procedureBodies[index]);
			}

			for (Int32 index = 0; index < _procedureBodies.Count; index++)
			{
				if ((_procedures[(UInt32)index].Type & ObjectAttributes.Conditional) != 0)
					ExtractAndReduceCondition(_procedureBodies[index], _conditions[index], 0);

				for (Int32 nodeIndex = 0; nodeIndex < _procedureBodies[index].Count; nodeIndex++)
				{
					ThrowIfCancelled();
					if (_procedureBodies[index][nodeIndex].Opcode.Operator == Opcode.O_CALL_CONDITION)
					{
						Node node = _procedureBodies[index][nodeIndex].Arguments[0];
						if (node.Opcode.Operator != Opcode.O_INTOP)
							throw new ScriptFormatException("Error: Invalid opcode for start address of condition");

						UInt32 conditionStartAddress = node.Opcode.Argument;
						do
						{
							node = _procedureBodies[index][nodeIndex = NextNodeIndex(_procedureBodies[index], nodeIndex, -1)];
						}
						while (node.Offset != conditionStartAddress);

						nodeIndex = NextNodeIndex(_procedureBodies[index], nodeIndex, -1);
						List<Node> condition = [];
						ExtractAndReduceCondition(_procedureBodies[index], condition, nodeIndex);
						_procedureBodies[index][nodeIndex].Arguments[0] = condition[0];
					}
				}
			}

			for (Int32 index = 0; index < _procedureBodies.Count; index++)
			{
				SetBlockBorders(_procedureBodies[index]);
			}

			TryRenameGlobalVariables();
			TryRenameImportedVariables();
		}

		private static Int32 NextNodeIndex(List<Node> nodes, Int32 currentIndex, Int32 step)
		{
			Int32 result = currentIndex + step;
			if (result < 0 || result >= nodes.Count)
				throw new ScriptFormatException("Error: Index of node out of range");

			return result;
		}

		/// <summary>
		/// Whether the nodes from <paramref name="startIndex"/> are exactly <paramref name="sequence"/>; when they are, the first
		/// <paramref name="removeCount"/> of them are removed.
		/// </summary>
		private static Boolean RemoveSequenceOfNodes(List<Node> nodes, Int32 startIndex, Int32 removeCount, Opcode[] sequence)
		{
			if (startIndex < 0 || startIndex + sequence.Length > nodes.Count)
				return false;

			for (Int32 index = 0; index < sequence.Length; index++)
			{
				if (nodes[startIndex + index].Opcode.Operator != sequence[index])
					return false;
			}

			if (removeCount > 0)
				nodes.RemoveRange(startIndex, removeCount);

			return true;
		}

		// The opcodes the compilers put around the code of a procedure and of its expressions.
		private static readonly Opcode[] TailOfProcedure = [Opcode.O_POP_TO_BASE, Opcode.O_POP_BASE, Opcode.O_POP_RETURN];
		private static readonly Opcode[] TailOfCriticalProcedure = [Opcode.O_POP_TO_BASE, Opcode.O_POP_BASE, Opcode.O_CRITICAL_DONE, Opcode.O_POP_RETURN];
		private static readonly Opcode[] CheckArgumentCount = [Opcode.O_DUP, Opcode.O_INTOP, Opcode.O_CHECK_ARG_COUNT];
		private static readonly Opcode[] ShortCircuitAnd = [Opcode.O_DUP, Opcode.O_INTOP, Opcode.O_SWAP, Opcode.O_IF, Opcode.O_POP];
		private static readonly Opcode[] ShortCircuitOr = [Opcode.O_DUP, Opcode.O_INTOP, Opcode.O_SWAP, Opcode.O_NOT, Opcode.O_IF, Opcode.O_POP];
		private static readonly Opcode[] StoreReturnAddress = [Opcode.O_INTOP, Opcode.O_D_TO_A];
		private static readonly Opcode[] ReturnSequence = [Opcode.O_D_TO_A, Opcode.O_SWAPA, Opcode.O_POP_TO_BASE, Opcode.O_POP_BASE, Opcode.O_A_TO_D, Opcode.O_POP_RETURN];
		private static readonly Opcode[] CriticalReturnSequence = [Opcode.O_D_TO_A, Opcode.O_SWAPA, Opcode.O_POP_TO_BASE, Opcode.O_POP_BASE, Opcode.O_A_TO_D, Opcode.O_CRITICAL_DONE, Opcode.O_POP_RETURN];

		/// <summary>
		/// Removes from each procedure the code the compiler adds (its end, the check of the number of arguments, the saving of the return
		/// address) and rewrites the short-circuit sequences of <c>andAlso</c> and <c>orElse</c> as one operator.
		/// </summary>
		private void InitialReduce()
		{
			for (Int32 procedureIndex = 0; procedureIndex < _procedureBodies.Count; procedureIndex++)
			{
				List<Node> body = _procedureBodies[procedureIndex];
				if (body.Count > 0)
				{
					Opcode[] tail = (_procedures[(UInt32)procedureIndex].Type & ObjectAttributes.Critical) != 0 ? TailOfCriticalProcedure : TailOfProcedure;
					if (!RemoveSequenceOfNodes(body, body.Count - tail.Length, tail.Length, tail))
						throw new ScriptFormatException("Error: Invalid tail of procedure's body");
				}

				for (Int32 nodeIndex = 0; nodeIndex < body.Count; nodeIndex++)
				{
					ThrowIfCancelled();
					switch (body[nodeIndex].Opcode.Operator)
					{
						case Opcode.O_DUP:
							if (!RemoveSequenceOfNodes(body, nodeIndex, 3, CheckArgumentCount))
							{
								Opcode actualOperator = RemoveSequenceOfNodes(body, nodeIndex, 0, ShortCircuitAnd)
									? Opcode.O_AND_ALSO
									: RemoveSequenceOfNodes(body, nodeIndex, 0, ShortCircuitOr) ? Opcode.O_OR_ELSE : (Opcode)0;

								if (actualOperator == 0)
									throw new ScriptFormatException("Error: Unknown sequence of opcodes");

								UInt32 skipOffset = body[nodeIndex + 1].Opcode.Argument;
								Int32 insertIndex = nodeIndex - 1;
								do
								{
									insertIndex = NextNodeIndex(body, insertIndex, 1);
								}
								while (skipOffset > body[insertIndex].Offset);

								Node inserted = body[nodeIndex].Clone();
								inserted.Opcode = inserted.Opcode.WithOperator(actualOperator);
								inserted.Offset = body[insertIndex - 1].Offset + ScriptOpcode.OperatorSize;
								body.Insert(insertIndex, inserted);
								body.RemoveRange(nodeIndex, actualOperator == Opcode.O_AND_ALSO ? 5 : 6);
							}

							nodeIndex--;
							break;

						case Opcode.O_D_TO_A:
							Opcode[] sequence = (_procedures[(UInt32)procedureIndex].Type & ObjectAttributes.Critical) != 0 ? CriticalReturnSequence : ReturnSequence;
							if (!RemoveSequenceOfNodes(body, nodeIndex, sequence.Length - 1, sequence))
							{
								if (!RemoveSequenceOfNodes(body, nodeIndex - 1, 2, StoreReturnAddress))
									throw new ScriptFormatException("Error: Unknown sequence of opcodes");
							}

							nodeIndex--;
							break;
					}
				}
			}
		}
	}
}