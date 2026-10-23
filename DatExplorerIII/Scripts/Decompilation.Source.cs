namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	internal sealed partial class Decompilation
	{
		public String CreateSource()
		{
			StringBuilder output = new();
			WriteDefinitions(output);
			output.AppendLine();
			output.AppendLine();
			WriteDeclarations(output);
			return output.ToString();
		}

		private static Boolean IsOmittedArgumentsAllowed(Opcode opcode)
		{
			return ((UInt16)opcode >= (UInt16)Opcode.O_END_CORE && (UInt16)opcode < (UInt16)Opcode.O_END_OP) || opcode == Opcode.O_POP_RETURN;
		}

		/// <summary>
		/// Writes the procedures and the variables of the script that are declared before its code.
		/// </summary>
		private void WriteDefinitions(StringBuilder output)
		{
			Int32 nameCount = _namespace.Count;
			Int32 definitionCount = _definitions.Count;
			if (nameCount != definitionCount)
			{
				output.AppendLine("/*******************************************************");
				output.AppendLine("*      Some unreferenced imported varables found.      *");
				output.AppendLine("*      Because of it it is impossible to specify       *");
				output.AppendLine("*      the real names of global variables.             *");
				output.AppendLine("*******************************************************/");
				output.AppendLine();

				for (Int32 index = 0; index < _globalVariables.Count; index++)
				{
					output.AppendLine(CreateVariableDefinition("variable " + _globalVariableNames[index], _globalVariables[index], false) + ";");
				}

				output.AppendLine();
			}

			DefinitionOutputType lastOutput = DefinitionOutputType.Nothing;
			for (Int32 index = 0; index < _namespace.Count; index++)
			{
				UInt32 nameOffset = _namespace.GetOffsetByIndex(index);
				if (index == 0 && _namespace[nameOffset] == "..............")
					continue;

				String definition;
				DefinitionOutputType currentOutput;
				if (_definitions.TryGetValue(nameOffset, out DefinitionObject? definitionObject))
				{
					if (definitionObject.Type == DefinitionObjectType.Procedure)
					{
						ProcedureDescriptor procedure = _procedures[definitionObject.Data];
						String prefix = String.Empty;
						if ((procedure.Type & ObjectAttributes.Import) != 0)
						{
							prefix = "import ";
							currentOutput = DefinitionOutputType.ImportedProcedure;
						}
						else if ((procedure.Type & ObjectAttributes.Export) != 0)
						{
							prefix = "export ";
							currentOutput = DefinitionOutputType.ExportedProcedure;
						}
						else
						{
							currentOutput = DefinitionOutputType.Procedure;
						}

						definition = prefix + "procedure " + _namespace[nameOffset] + CreateProcedureArguments(procedure.ArgumentCount) + ";";
						if ((procedure.Type & ObjectAttributes.NotImplemented) != 0)
							definition += " /* Prodedure defined, but not implemented */";
					}
					else
					{
						String prefix = String.Empty;
						if ((definitionObject.Attributes & ObjectAttributes.Import) != 0)
						{
							prefix = "import ";
							currentOutput = DefinitionOutputType.ImportedVariable;
						}
						else if ((definitionObject.Attributes & ObjectAttributes.Export) != 0)
						{
							prefix = "export ";
							currentOutput = DefinitionOutputType.ExportedVariable;
						}
						else
						{
							currentOutput = DefinitionOutputType.GlobalVariable;
						}

						definition = prefix + "variable " + _namespace[nameOffset];
						if ((definitionObject.Attributes & ObjectAttributes.Import) == 0)
							definition = CreateVariableDefinition(definition, new ScriptOpcode((Opcode)(definitionObject.Attributes & 0xFFFF), definitionObject.Data), false);

						definition += ";";
					}
				}
				else
				{
					currentOutput = DefinitionOutputType.UnknownVariable;
					definition = "/* ?import? variable " + _namespace.GetStringByIndex(index) + "; */";
				}

				if (currentOutput != lastOutput && lastOutput != DefinitionOutputType.Nothing)
					output.AppendLine();

				lastOutput = currentOutput;
				output.AppendLine(definition);
			}
		}

		/// <summary>
		/// Writes the procedures with their code.
		/// </summary>
		private void WriteDeclarations(StringBuilder output)
		{
			for (UInt32 index = 0; index < _procedures.Count; index++)
			{
				if (index == 0 && _namespace[_procedures[index].NameOffset] == "..............")
					continue;

				if (_procedures.GetProcedureSize(index) == 0)
				{
					if ((_procedures[index].Type & ObjectAttributes.Import) == 0)
					{
						output.AppendLine("/*******************************************************");
						output.AppendLine("*    Found Procedure without body.                     *");
						output.AppendLine("*                                                      *");
						output.AppendLine(PadCommentLine("*    Name: " + _namespace[_procedures[index].NameOffset]));
						output.AppendLine("*                                                      *");
						output.AppendLine("*******************************************************/");
						output.AppendLine();
					}

					continue;
				}

				ProcedureDescriptor procedure = _procedures[index];
				String line = (procedure.Type & ObjectAttributes.Critical) != 0 ? "critical " : String.Empty;
				line += "procedure " + _namespace[procedure.NameOffset] + CreateProcedureArguments(procedure.ArgumentCount);
				if ((procedure.Type & ObjectAttributes.Timed) != 0)
				{
					line += String.Format(CultureInfo.InvariantCulture, " in {0}", procedure.Time);
				}
				else if ((procedure.Type & ObjectAttributes.Conditional) != 0)
				{
					line += " when (" + GetSource(_conditions[(Int32)index][0], false, procedure.ArgumentCount) + ")";
				}

				output.AppendLine(line);
				Boolean localVariable = true;
				UInt32 localVariableIndex = procedure.ArgumentCount;
				Int32 indentLevel = 0;
				List<Node> body = _procedureBodies[(Int32)index];
				for (Int32 nodeIndex = 0; nodeIndex < body.Count; nodeIndex++)
				{
					ThrowIfCancelled();
					Node node = body[nodeIndex];
					if (node.Type == NodeType.BeginOfBlock)
					{
						if (nodeIndex > 1 && body[nodeIndex - 1].Type == NodeType.EndOfBlock)
						{
							output.AppendLine(GetIndentString(indentLevel) + "else begin");
						}
						else
						{
							output.AppendLine("begin");
						}

						indentLevel++;
					}
					else if (node.Type == NodeType.EndOfBlock)
					{
						indentLevel--;
						output.AppendLine(GetIndentString(indentLevel) + "end");
					}
					else
					{
						Opcode op = node.Opcode.Operator;
						if ((node.Opcode.GetAttributes(_options).Type == OpcodeAttributeType.Expression) && op != Opcode.O_STRINGOP && op != Opcode.O_FLOATOP && op != Opcode.O_INTOP)
							_messages.Add(String.Format(CultureInfo.InvariantCulture, "Warning: Result of expression is left in stack. (Opcode 0x{0:X4} at 0x{1:X8})", (UInt16)op, node.Offset));

						if (op == Opcode.O_STRINGOP || op == Opcode.O_FLOATOP || op == Opcode.O_INTOP)
						{
							if (!localVariable)
								_messages.Add("Warning: Free value found. Converted to definition of local variable");

							output.AppendLine(GetIndentString(indentLevel) + String.Format(CultureInfo.InvariantCulture, "variable LVar{0} := {1};", localVariableIndex, GetSource(node, false, procedure.ArgumentCount)));
							localVariableIndex++;
						}
						else if (op == Opcode.O_IF)
						{
							output.Append(GetIndentString(indentLevel) + GetSource(node, false, procedure.ArgumentCount) + " then ");
						}
						else if (op == Opcode.O_WHILE)
						{
							if (node.Type == NodeType.ForLoop)
							{
								List<String> parts = [];
								for (Int32 argumentIndex = 0; argumentIndex < node.Arguments.Count; argumentIndex++)
								{
									parts.Add(GetSource(node.Arguments[argumentIndex], false, procedure.ArgumentCount));
								}

								output.Append(GetIndentString(indentLevel) + "for (" + String.Join("; ", parts) + ") ");
							}
							else
							{
								output.Append(GetIndentString(indentLevel) + GetSource(node, false, procedure.ArgumentCount) + " do ");
							}
						}
						else
						{
							output.AppendLine(GetIndentString(indentLevel) + GetSource(node, false, procedure.ArgumentCount) + ";");
							localVariable = false;
						}
					}
				}

				output.AppendLine();
			}
		}
	}
}