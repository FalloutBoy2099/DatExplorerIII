namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	internal sealed partial class Decompilation
	{
		/// <summary>
		/// The source of a node and of its arguments. <paramref name="label"/> is true for a name that is a variable or a procedure,
		/// <paramref name="argumentCount"/> is the number of arguments of the procedure, to tell them from its local variables.
		/// </summary>
		private String GetSource(Node node, Boolean label, UInt32 argumentCount)
		{
			ThrowIfCancelled();

			if (node.Type == NodeType.OmittedArgument)
				return "/* Omitted argument */";

			Opcode op = node.Opcode.Operator;
			UInt32 argument = node.Opcode.Argument;
			if (op == Opcode.O_JMP)
			{
				if (node.Type == NodeType.Break)
					return "break";

				if (node.Type == NodeType.Continue)
					return "continue";
			}

			switch (op)
			{
				case Opcode.O_STRINGOP:
					return label ? SafeName(argument) : "\"" + Escape(_strings[argument]) + "\"";
				case Opcode.O_FLOATOP:
					return BitConverter.Int32BitsToSingle(unchecked((Int32)argument)).ToString("0.00000", CultureInfo.InvariantCulture);
				case Opcode.O_INTOP:
					return label ? SafeName(argument) : unchecked((Int32)argument).ToString(CultureInfo.InvariantCulture);
				case Opcode.O_POP_RETURN:
					return "return " + GetSource(node.Arguments[0], false, argumentCount);
				case Opcode.O_LOOKUP_STRING_PROC:
					return GetSource(node.Arguments[0], false, argumentCount);
				case Opcode.O_FETCH_GLOBAL:
					return _globalVariableNames[(Int32)node.Arguments[0].Opcode.Argument];
				case Opcode.O_STORE_GLOBAL:
					return _globalVariableNames[(Int32)node.Arguments[1].Opcode.Argument] + " := " + GetSource(node.Arguments[0], false, argumentCount);
				case Opcode.O_FETCH_EXTERNAL:
					return GetSource(node.Arguments[0], true, argumentCount);
				case Opcode.O_STORE_EXTERNAL:
					return GetSource(node.Arguments[1], true, argumentCount) + " := " + GetSource(node.Arguments[0], false, argumentCount);
				case Opcode.O_FETCH:
					return GetVariableName(node.Arguments[0].Opcode.Argument, argumentCount);
				case Opcode.O_STORE:
					return GetVariableName(node.Arguments[1].Opcode.Argument, argumentCount) + " := " + GetSource(node.Arguments[0], false, argumentCount);
				case Opcode.O_POP:
					String popSource = GetSource(node.Arguments[0], false, argumentCount);
					return node.Arguments[0].Opcode.Operator == Opcode.O_CALL ? "call " + popSource : popSource;
				case Opcode.O_CALL:
					String callName = node.Arguments[node.Arguments.Count - 1].Opcode.Operator == Opcode.O_INTOP
						? _namespace[_procedures[node.Arguments[node.Arguments.Count - 1].Opcode.Argument].NameOffset]
						: GetSource(node.Arguments[node.Arguments.Count - 1], false, argumentCount);
					List<String> callArguments = [];
					for (Int32 index = 0; index < node.Arguments.Count - 2; index++)
					{
						callArguments.Add(GetSource(node.Arguments[index], false, argumentCount));
					}

					return callName + "(" + String.Join(", ", callArguments) + ")";
				case Opcode.O_CALL_AT:
					return "call " + GetProcedureArgumentSource(node.Arguments[1], argumentCount) + " in (" + GetSource(node.Arguments[0], false, argumentCount) + ")";
				case Opcode.O_CALL_CONDITION:
					return "call " + GetProcedureArgumentSource(node.Arguments[1], argumentCount) + " when (" + GetSource(node.Arguments[0], false, argumentCount) + ")";
				case Opcode.O_ADDREGION:
					List<String> regionArguments = [];
					for (Int32 index = 1; index < node.Arguments.Count - 1; index++)
					{
						regionArguments.Add(GetSource(node.Arguments[index], false, argumentCount));
					}

					return "addRegion " + GetSource(node.Arguments[0], false, argumentCount) + " { " + String.Join(", ", regionArguments) + " }";
				case Opcode.O_REFRESHMOUSE:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 1U });
				case Opcode.O_ADDBUTTONPROC:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U, 3U, 4U, 5U });
				case Opcode.O_ADDBUTTONRIGHTPROC:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U, 3U });
				case Opcode.O_SAYOPTION:
					if (node.Arguments.Count > 1 && node.Arguments[1].Opcode.Operator == Opcode.O_INTOP)
						return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U });

					return "sayOption(" + GetSource(node.Arguments[0], false, argumentCount) + ", " + GetSource(node.Arguments[1], false, argumentCount) + ")";
				case Opcode.O_ADDREGIONPROC:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U, 3U, 4U, 5U });
				case Opcode.O_ADDREGIONRIGHTPROC:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U, 3U });
				case Opcode.O_ADDNAMEDEVENT:
				case Opcode.O_ADDNAMEDHANDLER:
				case Opcode.O_ADDKEY:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 2U });
				case Opcode.O_GSAY_OPTION:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 3U });
				case Opcode.O_GIQ_OPTION:
					return GetSourceWithProcedureArguments(node, argumentCount, new UInt32[] { 4U });
				case Opcode.O_CANCEL:
					return "cancel(" + _namespace[_procedures[node.Arguments[0].Opcode.Argument].NameOffset] + ")";
				case Opcode.O_NEGATE:
					return node.Arguments[0].Opcode.GetAttributes(_options).Category == OpcodeAttributeCategory.Infix
						? "-(" + GetSource(node.Arguments[0], label, argumentCount) + ")"
						: "-" + GetSource(node.Arguments[0], label, argumentCount);
				default:
					return GetDefaultSource(node, label, argumentCount);
			}
		}

		private String GetDefaultSource(Node node, Boolean label, UInt32 argumentCount)
		{
			OpcodeAttributes attributes = node.Opcode.GetAttributes(_options);
			if (attributes.ProcedureArguments.Length > 0)
				return GetSourceWithProcedureArguments(node, argumentCount, attributes.ProcedureArguments);

			if (node.Type == NodeType.ConditionalExpression)
			{
				String[] postfixes = { " if ", " else ", "" };
				Int32[] indexes = { 2, 1, 3 };
				String result = String.Empty;
				for (Int32 index = 0; index < indexes.Length; index++)
				{
					Node argument = node.Arguments[indexes[index]];
					Boolean parentheses = ArgumentNeedsParentheses(node, argument, Association.Right);
					result += (parentheses ? "(" : String.Empty) + GetSource(argument, label, argumentCount) + (parentheses ? ")" : String.Empty) + postfixes[index];
				}

				return result;
			}

			if (attributes.Category == OpcodeAttributeCategory.Infix)
			{
				String left = ArgumentNeedsParentheses(node, node.Arguments[0], Association.Left)
					? "(" + GetInfixArgumentSource(node, node.Arguments[0], argumentCount) + ")"
					: GetInfixArgumentSource(node, node.Arguments[0], argumentCount);
				String right = ArgumentNeedsParentheses(node, node.Arguments[1], Association.Right)
					? "(" + GetInfixArgumentSource(node, node.Arguments[1], argumentCount) + ")"
					: GetInfixArgumentSource(node, node.Arguments[1], argumentCount);
				return left + " " + attributes.Name + " " + right;
			}

			List<String> arguments = [];
			for (Int32 index = 0; index < node.Arguments.Count; index++)
			{
				arguments.Add(GetSource(node.Arguments[index], false, argumentCount));
			}

			String prefix = attributes.Name;
			if (node.Opcode.Operator == Opcode.O_AND_ALSO || node.Opcode.Operator == Opcode.O_OR_ELSE)
			{
				if (_options.UseOldShortCircuitKeywords)
				{
					prefix = node.Opcode.Operator == Opcode.O_AND_ALSO ? "and" : "or";
				}
				else
				{
					prefix = node.Opcode.Operator == Opcode.O_AND_ALSO ? "andAlso" : "orElse";
				}
			}

			if (arguments.Count == 0)
				return prefix;

			if (node.Opcode.Operator == Opcode.O_IF || node.Opcode.Operator == Opcode.O_WHILE)
				prefix += " ";

			return prefix + "(" + String.Join(", ", arguments) + ")";
		}

		private String GetInfixArgumentSource(Node node, Node argument, UInt32 argumentCount)
		{
			if (IsBitwiseOperator(node.Opcode.Operator))
				return GetBitwiseArgumentSource(argument, argumentCount);

			return GetSource(argument, false, argumentCount);
		}

		private String GetBitwiseArgumentSource(Node argument, UInt32 argumentCount)
		{
			if (argument.Opcode.Operator == Opcode.O_INTOP)
				return String.Format(CultureInfo.InvariantCulture, "0x{0:X8}", argument.Opcode.Argument);

			if (argument.Opcode.Operator == Opcode.O_NEGATE &&
				argument.Arguments.Count == 1 &&
				argument.Arguments[0].Opcode.Operator == Opcode.O_INTOP)
			{
				return "-" + String.Format(CultureInfo.InvariantCulture, "0x{0:X8}", argument.Arguments[0].Opcode.Argument);
			}

			if (argument.IsInfix(_options) && argument.Arguments.Count >= 2)
			{
				OpcodeAttributes attributes = argument.Opcode.GetAttributes(_options);
				String left = ArgumentNeedsParentheses(argument, argument.Arguments[0], Association.Left)
					? "(" + GetBitwiseArgumentSource(argument.Arguments[0], argumentCount) + ")"
					: GetBitwiseArgumentSource(argument.Arguments[0], argumentCount);
				String right = ArgumentNeedsParentheses(argument, argument.Arguments[1], Association.Right)
					? "(" + GetBitwiseArgumentSource(argument.Arguments[1], argumentCount) + ")"
					: GetBitwiseArgumentSource(argument.Arguments[1], argumentCount);
				return left + " " + attributes.Name + " " + right;
			}

			return GetSource(argument, false, argumentCount);
		}

		private static Boolean IsBitwiseOperator(Opcode op)
		{
			return op == Opcode.O_BWAND || op == Opcode.O_BWOR || op == Opcode.O_BWXOR;
		}

		private String GetSourceWithProcedureArguments(Node node, UInt32 argumentCount, UInt32[] procedureArgumentIndexes)
		{
			OpcodeAttributes attributes = node.Opcode.GetAttributes(_options);
			List<String> arguments = [];
			for (Int32 index = 0; index < node.Arguments.Count; index++)
			{
				Boolean isProcedureArgument = Array.IndexOf(procedureArgumentIndexes, (UInt32)(index + 1)) >= 0;
				arguments.Add(isProcedureArgument ? GetProcedureArgumentSource(node.Arguments[index], argumentCount) : GetSource(node.Arguments[index], false, argumentCount));
			}

			return attributes.Name + "(" + String.Join(", ", arguments) + ")";
		}

		private String GetProcedureArgumentSource(Node node, UInt32 argumentCount)
		{
			if (node.Opcode.Operator == Opcode.O_INTOP)
				return _namespace[_procedures[node.Opcode.Argument].NameOffset];

			return GetSource(node, false, argumentCount);
		}

		private Boolean ArgumentNeedsParentheses(Node node, Node argument, Association association)
		{
			return argument.IsInfix(_options) && (GetPriority(argument.Opcode.Operator) != GetPriority(node.Opcode.Operator) || GetAssociation(node.Opcode.Operator) != association);
		}

		/// <summary>
		/// How tightly an operator binds, for the parentheses: the higher, the tighter.
		/// </summary>
		private static Int32 GetPriority(Opcode op)
		{
			switch (op)
			{
				case Opcode.O_IF:
					return 0;
				case Opcode.O_OR:
				case Opcode.O_AND:
				case Opcode.O_AND_ALSO:
				case Opcode.O_OR_ELSE:
					return 2;
				case Opcode.O_GREATER:
				case Opcode.O_LESS:
				case Opcode.O_GREATER_EQUAL:
				case Opcode.O_LESS_EQUAL:
				case Opcode.O_EQUAL:
				case Opcode.O_NOT_EQUAL:
					return 3;
				case Opcode.O_ADD:
				case Opcode.O_SUB:
				case Opcode.O_BWXOR:
				case Opcode.O_BWOR:
				case Opcode.O_BWAND:
					return 4;
				case Opcode.O_MUL:
				case Opcode.O_DIV:
				case Opcode.O_MOD:
				case Opcode.O_TS_POW:
				case Opcode.O_TS_DIV:
					return 5;
				default:
					return 6;
			}
		}

		/// <summary>
		/// Which way operators of the same priority group: <c>a - b - c</c> is <c>(a - b) - c</c>.
		/// </summary>
		private static Association GetAssociation(Opcode op)
		{
			switch (op)
			{
				case Opcode.O_GREATER:
				case Opcode.O_LESS:
				case Opcode.O_GREATER_EQUAL:
				case Opcode.O_LESS_EQUAL:
				case Opcode.O_EQUAL:
				case Opcode.O_NOT_EQUAL:
					return Association.None;
				case Opcode.O_OR:
				case Opcode.O_AND:
				case Opcode.O_AND_ALSO:
				case Opcode.O_OR_ELSE:
				case Opcode.O_ADD:
				case Opcode.O_SUB:
				case Opcode.O_BWXOR:
				case Opcode.O_BWOR:
				case Opcode.O_BWAND:
				case Opcode.O_MUL:
				case Opcode.O_DIV:
				case Opcode.O_MOD:
					return Association.Left;
				default:
					return Association.None;
			}
		}
	}
}