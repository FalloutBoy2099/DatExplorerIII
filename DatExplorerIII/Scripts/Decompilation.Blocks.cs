namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	internal sealed partial class Decompilation
	{
		/// <summary>
		/// Puts the <c>begin</c> and <c>end</c> of the procedure and of its loops in the list, and finds which jumps are a <c>break</c>,
		/// a <c>continue</c>, and which loops are <c>for</c> loops.
		/// </summary>
		private void SetBlockBorders(List<Node> nodes)
		{
			if (nodes.Count == 0)
			{
				return;
			}

			UInt32 offset;
			if (nodes[0].Opcode.Operator == Opcode.O_PUSH_BASE)
			{
				offset = nodes[0].Offset;
				nodes[0] = new Node(NodeType.BeginOfBlock) { Offset = offset };
			}

			Int32 lastNodeIndex = nodes.Count - 1;
			if (nodes[lastNodeIndex].Opcode.Operator == Opcode.O_POP_RETURN && nodes[lastNodeIndex].Arguments.Count > 0 && nodes[lastNodeIndex].Arguments[0].Opcode.Operator == Opcode.O_INTOP)
			{
				offset = nodes[lastNodeIndex].Offset;
				nodes[lastNodeIndex] = new Node(NodeType.EndOfBlock) { Offset = offset };
			}
			else
			{
				offset = nodes[lastNodeIndex].Offset;
				nodes.Insert(lastNodeIndex + 1, new Node(NodeType.EndOfBlock) { Offset = offset });
			}

			for (Int32 index = 0; index < nodes.Count; index++)
			{
				ThrowIfCancelled();
				if (nodes[index].Opcode.Operator == Opcode.O_WHILE)
				{
					Node node = nodes[index].Arguments[0];
					UInt32 loopOffset = nodes[index].Arguments[1].GetTopOffset();
					if (node.Opcode.Operator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid opcode for jump-address");
					}

					nodes.Insert(index + 1, new Node(NodeType.BeginOfBlock) { Offset = nodes[index].Offset });
					UInt32 endOffset = node.Opcode.Argument;
					Int32 nodeIndex = index + 1;
					List<Int32> jumps = [];
					do
					{
						node = nodes[nodeIndex = NextNodeIndex(nodes, nodeIndex, 1)];
						if (node.Opcode.Operator == Opcode.O_JMP && node.Type == NodeType.Normal && node.Arguments.Count > 0)
						{
							UInt32 jumpOffset = node.Arguments[0].Opcode.Argument;
							if (jumpOffset == endOffset)
							{
								nodes[nodeIndex].Type = NodeType.Break;
							}
							else if (jumpOffset == loopOffset)
							{
								nodes[nodeIndex].Type = NodeType.Continue;
							}
							else
							{
								jumps.Add(nodeIndex);
							}
						}
					}
					while (node.Offset < endOffset);

					Boolean isForLoop = false;
					if (nodes[nodeIndex - 2].Arguments.Count > 0 && index > 0)
					{
						loopOffset = nodes[nodeIndex - 2].GetTopOffset();
						for (Int32 jumpIndex = 0; jumpIndex < jumps.Count; jumpIndex++)
						{
							if (nodes[jumps[jumpIndex]].Arguments[0].Opcode.Argument == loopOffset)
							{
								nodes[jumps[jumpIndex]].Type = NodeType.Continue;
								isForLoop = true;
							}
						}
					}

					UInt32 endBlockOffset = nodeIndex < nodes.Count ? nodes[nodeIndex].Offset : nodes[nodes.Count - 1].Offset;
					nodes.Insert(nodeIndex, new Node(NodeType.EndOfBlock) { Offset = endBlockOffset });
					node = nodes[nodeIndex - 1];
					if (node.Opcode.Operator != Opcode.O_JMP || node.Arguments[0].Opcode.Operator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid tail of 'while' statement");
					}

					if (isForLoop)
					{
						nodes[index].Type = NodeType.ForLoop;
						nodes[index].Arguments[0] = nodes[index - 1];
						nodes[index].Arguments.Add(nodes[nodeIndex - 2]);
						nodes.RemoveAt(index - 1);
						nodes.RemoveRange(nodeIndex - 3, 2);
					}
					else
					{
						nodes.RemoveAt(nodeIndex - 1);
						nodes[index].Arguments.RemoveAt(0);
					}
				}
				else if (nodes[index].Opcode.Operator == Opcode.O_IF)
				{
					if (nodes[index].Type == NodeType.ConditionalExpression)
					{
						List<Node> args = nodes[index].Arguments;
						if (args[2].Opcode.GetAttributes(_options).Type != OpcodeAttributeType.ExpressionStatement || args[3].Opcode.GetAttributes(_options).Type != OpcodeAttributeType.ExpressionStatement)
						{
							throw new ScriptFormatException("Error: Conditional expression left in stack.");
						}

						_messages.Add("Warning: Conditional expression left in stack. Restoring as IF.");
						nodes[index].Type = NodeType.Normal;
						nodes.Insert(index + 1, args[2]);
						nodes.Insert(index + 2, args[4]);
						nodes.Insert(index + 3, args[3]);
						args.RemoveRange(2, args.Count - 2);
					}

					Node node = nodes[index].Arguments[0];
					if (node.Opcode.Operator != Opcode.O_INTOP)
					{
						throw new ScriptFormatException("Error: Invalid opcode for jump-address");
					}

					nodes.Insert(index + 1, new Node(NodeType.BeginOfBlock) { Offset = nodes[index + 1].Offset });
					UInt32 jumpOffset = node.Opcode.Argument;
					Int32 nodeIndex = index + 1;
					do
					{
						node = nodes[nodeIndex = NextNodeIndex(nodes, nodeIndex, 1)];
					}
					while (node.Offset < jumpOffset);

					UInt32 ifEndBlockOffset = nodeIndex < nodes.Count ? nodes[nodeIndex].Offset : nodes[nodes.Count - 1].Offset;
					nodes.Insert(nodeIndex, new Node(NodeType.EndOfBlock) { Offset = ifEndBlockOffset });
					node = nodes[nodeIndex - 1];
					if (node.Opcode.Operator == Opcode.O_JMP && node.Type != NodeType.Break && node.Type != NodeType.Continue)
					{
						if (node.Arguments[0].Opcode.Operator != Opcode.O_INTOP)
						{
							throw new ScriptFormatException("Error: Invalid opcode for jump-address");
						}

						jumpOffset = node.Arguments[0].Opcode.Argument;
						if (jumpOffset > node.Offset)
						{
							nodes.RemoveAt(nodeIndex - 1);
							UInt32 elseBeginBlockOffset = nodeIndex + 1 < nodes.Count ? nodes[nodeIndex + 1].Offset : nodes[nodes.Count - 1].Offset;
							nodes.Insert(nodeIndex, new Node(NodeType.BeginOfBlock) { Offset = elseBeginBlockOffset });
							Node previous = nodes[nodeIndex - 1];
							if (previous.Type == NodeType.EndOfBlock)
							{
								do
								{
									node = nodes[nodeIndex = NextNodeIndex(nodes, nodeIndex, 1)];
								}
								while (node.Offset < jumpOffset);

								UInt32 elseEndBlockOffset = nodeIndex < nodes.Count ? nodes[nodeIndex].Offset : nodes[nodes.Count - 1].Offset;
								nodes.Insert(nodeIndex, new Node(NodeType.EndOfBlock) { Offset = elseEndBlockOffset });
							}
						}
					}

					nodes[index].Arguments.RemoveAt(0);
					index++;
				}
			}
		}
	}
}