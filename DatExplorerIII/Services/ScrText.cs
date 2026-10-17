namespace DatExplorerIII.Services
{
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using ScrLib;

	/// <summary>
	/// Writes a script of Arcanum (.scr) as text, the way a decompiled script (.int) is shown: a line of the script is an
	/// <c>if</c> with its condition, a <c>then</c> and an <c>else</c> with their actions, and the viewer gives it the colors of a source.
	/// </summary>
	public static class ScrText
	{
		/// <summary>Reads a script and writes it.</summary>
		/// <exception cref="ScrFormatException">The data is not a script.</exception>
		public static String Render(Byte[] data)
		{
			return Render(ScrReader.Read(data));
		}

		public static String Render(ScrFile script)
		{
			StringBuilder text = new();
			text.Append("// ").AppendLine(script.Description.Length > 0 ? script.Description : "(no description)");
			text.AppendLine(String.Create(CultureInfo.InvariantCulture, $"// {script.LineCount} lines, capacity {script.Capacity}, flags {script.Flags}"));

			foreach (ScrLine line in script.Lines)
			{
				text.AppendLine();
				text.AppendLine(String.Create(CultureInfo.InvariantCulture, $"// line {line.Index}"));
				text.Append("if ").AppendLine(Describe(line.Condition));
				text.Append("then ").AppendLine(Describe(line.Then));

				// An else that does nothing is not written.
				if (line.Else.Type != ScriptActionType.DoNothing || line.Else.Operands.Any(operand => operand.TypeTag != 0 || operand.Value != 0))
				{
					text.Append("else ").AppendLine(Describe(line.Else));
				}
			}

			return text.ToString();
		}

		/// <summary>The name of the instruction of the engine and its operands (the unused ones at the end are left out): <c>SAT_GOTO(Number:5)</c>.</summary>
		private static String Describe(ScrInstruction instruction)
		{
			Int32 used = instruction.Operands.Count;
			while (used > 0 && instruction.Operands[used - 1] is { TypeTag: 0, Value: 0 })
			{
				used--;
			}

			String name = instruction.IsKnown ? instruction.NativeName : String.Create(CultureInfo.InvariantCulture, $"UNKNOWN_{instruction.RawOpcode}");
			return name + "(" + String.Join(", ", instruction.Operands.Take(used).Select(Describe)) + ")";
		}

		private static String Describe(ScrOperand operand)
		{
			String tag = Enum.IsDefined((ScriptValueType)operand.TypeTag) ? ((ScriptValueType)operand.TypeTag).ToString() : "Tag" + operand.TypeTag.ToString(CultureInfo.InvariantCulture);
			return tag + ":" + operand.Value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
