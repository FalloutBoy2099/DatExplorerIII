namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	internal sealed partial class Decompilation
	{
		private static readonly Opcode[] ConditionStart = [Opcode.O_JMP, Opcode.O_CRITICAL_START];
		private static readonly Opcode[] ConditionEnd = [Opcode.O_CRITICAL_DONE, Opcode.O_STOP_PROG];

		/// <summary>
		/// Gives each node its arguments, which are the expressions that come before it (the code is a stack machine: operands first),
		/// and the bodies of <c>if</c> and <c>while</c> their statements. Goes on until <paramref name="endOffset"/>.
		/// </summary>
		private UInt32 BuildTreeBranch(List<Node> nodes, UInt32 startIndex, UInt32 endOffset, Boolean inLoop)
		{
			UInt32 index;
			for (index = startIndex; index < nodes.Count && nodes[(Int32)index].Offset < endOffset; index++)
			{
				ThrowIfCancelled();
				Opcode op = nodes[(Int32)index].Opcode.Operator;
				OpcodeAttributes attributes = nodes[(Int32)index].Opcode.GetAttributes(_options);
				UInt32 argumentCount = attributes.ArgumentCount;

				if (op == Opcode.O_FETCH_EXTERNAL || op == Opcode.O_STORE_EXTERNAL)
				{
					Int32 nameNodeIndex = NextNodeIndex(nodes, (Int32)index, -1);
					Opcode nameOperator = nodes[nameNodeIndex].Opcode.Operator;
					if (nameOperator != Opcode.O_STRINGOP && nameOperator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid reference to external variable");
					}

					SetExternalVariable(nodes[nameNodeIndex].Opcode.Argument);
				}
				else if (op == Opcode.O_CALL)
				{
					Int32 countNodeIndex = NextNodeIndex(nodes, (Int32)index, -2);
					if (nodes[countNodeIndex].Opcode.Operator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid opcode for procedure's number of arguments");
					}

					argumentCount = nodes[countNodeIndex].Opcode.Argument + 2;
				}
				else if (op == Opcode.O_ADDREGION)
				{
					Int32 countNodeIndex = NextNodeIndex(nodes, (Int32)index, -1);
					if (nodes[countNodeIndex].Opcode.Operator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid opcode for addRegion number of arguments");
					}

					argumentCount = nodes[countNodeIndex].Opcode.Argument + 1;
				}
				else if (op == Opcode.O_JMP && index > 0 && !nodes[NextNodeIndex(nodes, (Int32)index, -1)].IsExpression(_options) && inLoop)
				{
					argumentCount = 0;
					nodes[(Int32)index].Type = NodeType.Break;
				}

				UInt32 omittedStartIndex = argumentCount;
				Int32 nodeIndex = (Int32)index;
				for (UInt32 argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
				{
					nodeIndex = NextNodeIndex(nodes, nodeIndex, -1);
					if (!nodes[nodeIndex].IsExpression(_options))
					{
						if ((_options.IgnoreWrongArgumentCount || nodes[nodeIndex].Opcode.Operator == Opcode.O_JMP) && IsOmittedArgumentsAllowed(op))
						{
							_messages.Add("Warning: Omitted expression found");
							omittedStartIndex = argumentIndex;
							break;
						}

						throw new ScriptFormatException(_options.IgnoreWrongArgumentCount
							? $"Error: Not enough arguments for {nodes[(Int32)index].Offset:X} ({nodes[(Int32)index].Opcode.GetAttributes(_options).Name})"
							: $"Error: Expression required for {nodes[(Int32)index].Offset:X}");
					}
				}

				for (UInt32 argumentIndex = 0; argumentIndex < argumentCount; argumentIndex++)
				{
					if (argumentIndex < omittedStartIndex)
					{
						nodes[(Int32)index].Arguments.Insert(0, nodes[(Int32)index - 1]);
						nodes.RemoveAt((Int32)index - 1);
						index--;
					}
					else if (_options.InsertOmittedArgumentsBackward)
					{
						nodes[(Int32)index].Arguments.Add(new Node(NodeType.OmittedArgument));
					}
					else
					{
						nodes[(Int32)index].Arguments.Insert(0, new Node(NodeType.OmittedArgument));
					}
				}

				if (op == Opcode.O_IF)
				{
					List<Node> arguments = nodes[(Int32)index].Arguments;
					UInt32 elseOffset = arguments[0].Opcode.Argument;
					UInt32 elseIndex = BuildTreeBranch(nodes, index + 1, elseOffset, inLoop);
					UInt32 skipIndex = 0;
					Node jumpNode = nodes[(Int32)elseIndex - 1];
					if (jumpNode.Opcode.Operator == Opcode.O_JMP)
					{
						UInt32 skipOffset = jumpNode.Opcode.Argument;
						if (skipOffset > nodes[(Int32)index].Offset)
						{
							skipIndex = BuildTreeBranch(nodes, elseIndex, skipOffset, inLoop);
							if (elseIndex >= index + 3 && skipIndex == elseIndex + 1 && nodes[(Int32)elseIndex - 2].IsExpression(_options) && nodes[(Int32)skipIndex - 1].IsExpression(_options))
							{
								nodes[(Int32)index].Type = NodeType.ConditionalExpression;
								arguments.Insert(2, nodes[(Int32)elseIndex - 2]);
								arguments.Insert(3, nodes[(Int32)skipIndex - 1]);
								arguments.Insert(4, jumpNode);
								nodes.RemoveRange((Int32)index + 1, (Int32)skipIndex - ((Int32)index + 1));
								continue;
							}
						}
					}

					index = (skipIndex > 0 ? skipIndex : elseIndex) - 1;
				}
				else if (op == Opcode.O_WHILE)
				{
					UInt32 endLoopOffset = nodes[(Int32)index].Arguments[0].Opcode.Argument;
					UInt32 endLoopIndex = BuildTreeBranch(nodes, index + 1, endLoopOffset, true);
					index = endLoopIndex - 1;
				}
			}

			return index;
		}

		private void BuildTree(List<Node> nodes)
		{
			if (nodes.Count > 0)
			{
				BuildTreeBranch(nodes, 0, nodes[nodes.Count - 1].Offset + ScriptOpcode.OperatorSize, false);
			}
		}

		/// <summary>
		/// Takes out of a procedure the code of a condition (the <c>when</c> of a conditional procedure, or the test of a conditional
		/// call) and checks that it is one expression.
		/// </summary>
		private void ExtractAndReduceCondition(List<Node> source, List<Node> destination, Int32 startIndex)
		{
			Int32 nodeIndex;
			Node node = source[nodeIndex = NextNodeIndex(source, startIndex - 1, 1)];
			if (node.Opcode.Operator != Opcode.O_JMP || node.Arguments[0].Opcode.Operator != Opcode.O_INTOP)
			{
				throw new ScriptFormatException("Error: Invalid startup of condition");
			}

			UInt32 jumpOffset = node.Arguments[0].Opcode.Argument;
			do
			{
				node = source[nodeIndex = NextNodeIndex(source, nodeIndex, 1)];
			}
			while (node.Offset < jumpOffset);

			destination.Clear();
			for (Int32 index = 0; index < nodeIndex - startIndex; index++)
			{
				destination.Add(source[startIndex + index]);
			}

			if (!RemoveSequenceOfNodes(destination, 0, 2, ConditionStart))
			{
				throw new ScriptFormatException("Error: Invalid startup of condition");
			}

			if (!RemoveSequenceOfNodes(destination, destination.Count - 2, 2, ConditionEnd))
			{
				throw new ScriptFormatException("Error: Invalid cleanup of condition");
			}

			if (destination.Count != 1 || !destination[0].IsExpression(_options))
			{
				throw new ScriptFormatException("Error: Invalid condition. Only one expression allowed");
			}

			source.RemoveRange(startIndex, nodeIndex - startIndex);
		}
	}
}