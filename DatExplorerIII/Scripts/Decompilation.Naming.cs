namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	internal sealed partial class Decompilation
	{
		private String CreateVariableDefinition(String prefix, ScriptOpcode value, Boolean appendSemicolon)
		{
			String result = prefix;
			if (value.Operator == Opcode.O_STRINGOP)
			{
				result += " := \"" + Escape(_strings[value.Argument]) + "\"";
			}
			else if (value.Operator == Opcode.O_FLOATOP)
			{
				result += " := " + BitConverter.Int32BitsToSingle(unchecked((Int32)value.Argument)).ToString("0.00000", CultureInfo.InvariantCulture);
			}
			else if (value.Operator == Opcode.O_INTOP && value.Argument != 0)
			{
				result += " := " + unchecked((Int32)value.Argument).ToString(CultureInfo.InvariantCulture);
			}

			if (appendSemicolon)
			{
				result += ";";
			}

			return result;
		}

		private static String CreateProcedureArguments(UInt32 argumentCount)
		{
			if (argumentCount == 0)
			{
				return String.Empty;
			}

			List<String> arguments = [];
			for (UInt32 index = 0; index < argumentCount; index++)
			{
				arguments.Add(String.Format(CultureInfo.InvariantCulture, "variable arg{0}", index));
			}

			return "(" + String.Join(", ", arguments) + ")";
		}

		private String SafeName(UInt32 offset)
		{
			try
			{
				return _namespace[offset];
			}
			catch (ScriptFormatException)
			{
				_messages.Add("Warning: Restoration after exception. Object name replaced with '/* Fake object name */'");
				return "/* Fake object name */";
			}
		}

		private String GetVariableName(UInt32 index, UInt32 argumentCount)
		{
			return index < argumentCount
				? String.Format(CultureInfo.InvariantCulture, "arg{0}", index)
				: String.Format(CultureInfo.InvariantCulture, "LVar{0}", index);
		}

		private String GetIndentString(Int32 level)
		{
			StringBuilder builder = new();
			for (Int32 index = 0; index < level; index++)
			{
				builder.Append(_options.Indent);
			}

			return builder.ToString();
		}

		private static String PadCommentLine(String value)
		{
			String result = value;
			while (result.Length < 55)
			{
				result += " ";
			}

			return result + "*";
		}

		private static String Escape(String value)
		{
			return value.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);
		}
	}
}