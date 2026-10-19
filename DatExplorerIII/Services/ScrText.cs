namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
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
		/// <summary>The instruction that calls another script by its number (the first operand).</summary>
		private const String CallScript = "SAT_CALL_SCRIPT";

		/// <summary>Reads a script and writes it.</summary>
		/// <param name="data">The file.</param>
		/// <param name="scriptName">Gives the name of the script of a number (see <see cref="ScrScriptNames"/>), to write it after a call.</param>
		/// <exception cref="ScrFormatException">The data is not a script.</exception>
		public static String Render(Byte[] data, Func<Int32, String?>? scriptName = null)
		{
			return Render(ScrReader.Read(data), scriptName);
		}

		public static String Render(ScrFile script, Func<Int32, String?>? scriptName = null)
		{
			StringBuilder text = new();
			text.Append("// ").AppendLine(script.Description.Length > 0 ? script.Description : "(no description)");
			text.AppendLine(String.Create(CultureInfo.InvariantCulture, $"// {script.LineCount} lines, capacity {script.Capacity}, flags {script.Flags}"));

			// The first eight bytes of the file are the local flags and the counters the script starts with: said when there is something.
			if (script.HeaderFlags != 0 || script.Counters != 0)
			{
				List<String> state = new();
				if (script.HeaderFlags != 0)
				{
					state.Add(String.Create(CultureInfo.InvariantCulture, $"local flags 0x{script.HeaderFlags:X8}"));
				}

				if (script.Counters != 0)
				{
					state.Add("counters " + String.Join(", ", Enumerable.Range(0, 4).Select(index => script.GetCounter(index).ToString(CultureInfo.InvariantCulture))));
				}

				text.Append("// ").AppendLine(String.Join("; ", state));
			}

			foreach (ScrLine line in script.Lines)
			{
				text.AppendLine();
				text.AppendLine(String.Create(CultureInfo.InvariantCulture, $"// line {line.Index}"));
				text.Append("if ").AppendLine(Describe(line.Condition));
				text.Append("then ").AppendLine(Describe(line.Then) + CalledScript(line.Then, scriptName));

				// An else that does nothing is not written.
				if (line.Else.Type != ScriptActionType.DoNothing)
				{
					text.Append("else ").AppendLine(Describe(line.Else) + CalledScript(line.Else, scriptName));
				}
			}

			return text.ToString();
		}

		/// <summary>
		/// After a call of a script, a comment with the name of the script that is called ("  // 01013dante.scr"), when the number of
		/// the script is a number of the file and the script is known; nothing for the other instructions.
		/// </summary>
		private static String CalledScript(ScrAction action, Func<Int32, String?>? scriptName)
		{
			if (scriptName == null || !action.IsKnown || action.NativeName != CallScript || action.Operands[0].TypeTag != (Byte)ScriptValueType.Number)
			{
				return String.Empty;
			}

			return scriptName(action.Operands[0].Value) is { } name ? "  // " + name : String.Empty;
		}

		/// <summary>
		/// The name of the instruction of the engine and its operands: only the ones the game reads (the others are what was left in
		/// the memory when the file was saved). <c>SAT_GOTO(Number:5)</c>, <c>SCT_HAS_GOLD(Triggerer, Number:100)</c>: an operand that is an
		/// object has the name of the object, a value has its kind. A name that has no count has its unused operands at the end left out.
		/// </summary>
		private static String Describe(ScrInstruction instruction)
		{
			Int32 used = instruction.Operands.Count;
			if (instruction.IsKnown && ScrOperandCounts.TryGet(instruction.NativeName, out Int32 count))
			{
				used = Math.Min(count, used);
			}
			else
			{
				while (used > 0 && instruction.Operands[used - 1] is { TypeTag: 0, Value: 0 })
				{
					used--;
				}
			}

			String name = instruction.IsKnown ? instruction.NativeName : String.Create(CultureInfo.InvariantCulture, $"UNKNOWN_{instruction.RawOpcode}");
			String domains = instruction.IsKnown && ScrOperandDomains.TryGet(instruction.NativeName, out String known) ? known : String.Empty;
			return name + "(" + String.Join(", ", instruction.Operands.Take(used).Select((operand, index) => index < domains.Length && domains[index] == 'O' ? DescribeObject(operand) : DescribeValue(operand))) + ")";
		}

		/// <summary>
		/// An operand that is an object: the type is the one that tells which (<c>Attachee</c>, <c>Player</c>...). The number only means
		/// something for a local object (which of them); for the others it is what was in the memory when the file was saved.
		/// </summary>
		private static String DescribeObject(ScrOperand operand)
		{
			return DescribeObject(operand.TypeTag, operand.Value);
		}

		private static String DescribeObject(Int32 type, Int32 value)
		{
			if (!Enum.IsDefined((ScriptFocusObject)type))
			{
				return String.Create(CultureInfo.InvariantCulture, $"Object{type}:{value}");
			}

			ScriptFocusObject focus = (ScriptFocusObject)type;
			return focus == ScriptFocusObject.LocalObject ? String.Create(CultureInfo.InvariantCulture, $"{focus}:{value}") : focus.ToString();
		}

		/// <summary>An operand that is a value: its kind and its number; a variable or a flag of the player says which object holds it.</summary>
		private static String DescribeValue(ScrOperand operand)
		{
			if (!Enum.IsDefined((ScriptValueType)operand.TypeTag))
			{
				return String.Create(CultureInfo.InvariantCulture, $"Tag{operand.TypeTag}:{operand.Value}");
			}

			ScriptValueType type = (ScriptValueType)operand.TypeTag;

			// The number holds the object in its high half and the number of the variable or of the flag in the other.
			if (type is ScriptValueType.PcVar or ScriptValueType.PcFlag)
			{
				return String.Create(CultureInfo.InvariantCulture, $"{type}:{DescribeObject((operand.Value >> 16) & 0xFFFF, 0)}.{operand.Value & 0xFFFF}");
			}

			return String.Create(CultureInfo.InvariantCulture, $"{type}:{operand.Value}");
		}
	}
}