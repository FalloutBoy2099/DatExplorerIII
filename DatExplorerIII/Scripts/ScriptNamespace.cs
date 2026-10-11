namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	/// <summary>
	/// A table of strings of the compiled script: the namespace holds the names, the stringspace the texts. A string is found by its
	/// offset in the table.
	/// </summary>
	internal sealed class ScriptNamespace
	{
		private readonly Dictionary<UInt32, String> _strings = [];
		private readonly List<UInt32> _order = [];

		public Int32 Count => _order.Count;
		public String this[UInt32 offset] => _strings.TryGetValue(offset, out String? value) ? value : throw new ScriptFormatException(String.Format(CultureInfo.InvariantCulture, "Error: No string at offset 0x{0:x8}", offset));

		public void Read(BinaryScriptReader reader)
		{
			_strings.Clear();
			_order.Clear();
			UInt32 length = reader.ReadUInt32();
			if (length == 0xFFFFFFFF)
			{
				return;
			}

			UInt32 totalRead = 0;
			while (totalRead < length)
			{
				UInt16 stringLength = reader.ReadUInt16();
				if (stringLength < 2 || (stringLength & 1) != 0)
				{
					throw new ScriptFormatException("Error: Invalid length of string");
				}

				Byte[] bytes = reader.ReadBytes(stringLength);
				if (bytes[stringLength - 1] != 0 && bytes[stringLength - 2] != 0)
				{
					throw new ScriptFormatException("Error: Invalid end of string in namespace");
				}

				Int32 zeroIndex = Array.IndexOf(bytes, (Byte)0);
				if (zeroIndex < 0)
				{
					zeroIndex = bytes.Length;
				}

				String value = Encoding.Latin1.GetString(bytes, 0, zeroIndex);
				value = EscapeNamespaceString(value);
				UInt32 offset = totalRead + 6;
				_strings[offset] = value;
				_order.Add(offset);
				totalRead += (UInt32)(2 + stringLength);
			}

			UInt32 terminator = reader.ReadUInt32();
			if (terminator != 0xFFFFFFFF)
			{
				throw new ScriptFormatException("Error: Invalid terminator of namespace");
			}
		}

		public String GetStringByIndex(Int32 index)
		{
			return this[_order[index]];
		}

		public UInt32 GetOffsetByIndex(Int32 index)
		{
			return _order[index];
		}

		private static String EscapeNamespaceString(String value)
		{
			return value
				.Replace("\\", "\\\\", StringComparison.Ordinal)
				.Replace("\a", "\\a", StringComparison.Ordinal)
				.Replace("\b", "\\b", StringComparison.Ordinal)
				.Replace("\f", "\\f", StringComparison.Ordinal)
				.Replace("\n", "\\n", StringComparison.Ordinal)
				.Replace("\r", "\\r", StringComparison.Ordinal)
				.Replace("\t", "\\t", StringComparison.Ordinal)
				.Replace("\"", "\\\"", StringComparison.Ordinal);
		}
	}
}