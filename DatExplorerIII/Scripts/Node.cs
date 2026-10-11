namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A node of the tree of a procedure: an opcode with the nodes that are its arguments.
	/// </summary>
	internal sealed class Node
	{
		public Node(NodeType type = NodeType.Normal)
		{
			Type = type;
			Opcode = new ScriptOpcode(global::DatExplorerIII.Scripts.Opcode.O_NOOP, 0);
			Arguments = [];
		}

		public UInt32 Offset { get; set; }
		public ScriptOpcode Opcode { get; set; }
		public NodeType Type { get; set; }
		public List<Node> Arguments { get; }

		public Node Clone()
		{
			Node clone = new(Type) { Offset = Offset, Opcode = Opcode };
			for (Int32 index = 0; index < Arguments.Count; index++)
			{
				clone.Arguments.Add(Arguments[index].Clone());
			}

			return clone;
		}

		public UInt32 GetTopOffset()
		{
			return Arguments.Count > 0 ? Arguments[0].GetTopOffset() : Offset;
		}

		public Boolean IsExpression(ScriptDecompilerOptions options)
		{
			OpcodeAttributeType type = Opcode.GetAttributes(options).Type;
			return type == OpcodeAttributeType.Expression || type == OpcodeAttributeType.ExpressionStatement || Type == NodeType.ConditionalExpression;
		}

		public Boolean IsInfix(ScriptDecompilerOptions options)
		{
			return Opcode.GetAttributes(options).Category == OpcodeAttributeCategory.Infix || Type == NodeType.ConditionalExpression;
		}
	}

	/// <summary>
	/// What a node is, when it is not just an opcode.
	/// </summary>
	internal enum NodeType
	{
		Normal,
		BeginOfBlock,
		EndOfBlock,
		OmittedArgument,
		Break,
		Continue,
		ForLoop,
		ConditionalExpression
	}
}