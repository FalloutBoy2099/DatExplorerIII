namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Models.GameFiles;

	/// <summary>
	/// Reads the entities of Fallout Tactics (.ent, and the characters .chr, which have the tag "&lt;character&gt;", a version and a zero before it): the tag "&lt;entity&gt;" and a zero, a version as text with a zero, the class as a
	/// string (32 bit length, characters), then the tag "&lt;esh&gt;" and a zero, a version and a zero, the count of properties (32 bit) and the
	/// properties: a name (a string), a type, a size, and the value of this size. The types are 1 boolean, 2 decimal number, 3 whole
	/// number, 5, 6 and 13 lists of decimal numbers, 14 a rectangle, 4, 8, 9 and 26 strings (the string of type 4 can be a nested entity),
	/// and 11 a group of properties.
	/// </summary>
	public static class EntReader
	{
		private const Int32 CharacterHeaderLimit = 64;
		private const Int32 MaxDepth = 16;
		private const Int32 MaxProperties = 100000;
		private const Int32 MaxNameLength = 1024;

		private static ReadOnlySpan<Byte> EntityTag => "<entity>\0"u8;

		private static ReadOnlySpan<Byte> PropertiesTag => "<esh>\0"u8;

		private static ReadOnlySpan<Byte> CharacterTag => "<character>\0"u8;

		/// <summary>True when the data starts like an ENT file, or like a character (.chr), which is an entity after a short header.</summary>
		public static Boolean IsEnt(ReadOnlySpan<Byte> data)
		{
			return data.StartsWith(EntityTag) || data.StartsWith(CharacterTag);
		}

		/// <exception cref="InvalidDataException">The data is not a valid ENT file.</exception>
		public static EntFile Read(Byte[] data)
		{
			if (!IsEnt(data))
				throw new InvalidDataException("This is not an ENT entity.");

			// A character has its version after the tag, then the entity.
			Int32 at = 0;
			if (data.AsSpan().StartsWith(CharacterTag))
			{
				at = data.AsSpan(0, Math.Min(data.Length, CharacterHeaderLimit)).IndexOf(EntityTag);
				if (at < 0)
					throw new InvalidDataException("The character holds no entity.");
			}

			(String className, IReadOnlyList<EntNode> properties) = ReadEntity(data, ref at, data.Length, 0);
			return new EntFile(className, properties);
		}

		/// <summary>The entity as text: the class, then a line for each property, the groups indented.</summary>
		public static String ToText(EntFile entity)
		{
			StringBuilder text = new();
			text.Append(entity.ClassName).AppendLine();
			Append(text, entity.Properties, 1);
			return text.ToString();
		}

		private static void Append(StringBuilder text, IReadOnlyList<EntNode> nodes, Int32 level)
		{
			foreach (EntNode node in nodes)
			{
				text.Append(' ', level * 2).Append(node.Name);
				if (node.Value.Length > 0)
					text.Append(": ").Append(node.Value);

				text.AppendLine();
				Append(text, node.Children, level + 1);
			}
		}

		private static (String ClassName, IReadOnlyList<EntNode> Properties) ReadEntity(Byte[] data, ref Int32 at, Int32 end, Int32 depth)
		{
			if (depth > MaxDepth || !data.AsSpan(at, end - at).StartsWith(EntityTag))
				throw new InvalidDataException("The entity is damaged.");

			at += EntityTag.Length;
			SkipVersion(data, ref at, end);
			String className = ReadString(data, ref at, end);
			return (className, ReadProperties(data, ref at, end, depth));
		}

		private static IReadOnlyList<EntNode> ReadProperties(Byte[] data, ref Int32 at, Int32 end, Int32 depth)
		{
			if (depth > MaxDepth || !data.AsSpan(at, end - at).StartsWith(PropertiesTag))
				throw new InvalidDataException("The properties are damaged.");

			at += PropertiesTag.Length;
			SkipVersion(data, ref at, end);
			UInt32 count = ReadUInt32(data, ref at, end);
			if (count > MaxProperties)
				throw new InvalidDataException("The count of properties is impossible.");

			List<EntNode> nodes = [];
			for (Int32 i = 0; i < count; i++)
			{
				String name = ReadString(data, ref at, end);
				UInt32 type = ReadUInt32(data, ref at, end);
				UInt32 size = ReadUInt32(data, ref at, end);
				if (size > end - at)
					throw new InvalidDataException("A property is cut.");

				Int32 valueEnd = at + (Int32)size;
				nodes.Add(ReadValue(data, name, type, at, valueEnd, depth));
				at = valueEnd;
			}

			return nodes;
		}

		private static EntNode ReadValue(Byte[] data, String name, UInt32 type, Int32 start, Int32 end, Int32 depth)
		{
			ReadOnlySpan<Byte> value = data.AsSpan(start, end - start);
			switch (type)
			{
				case 1 when value.Length == 1:
					return Plain(name, value[0] != 0 ? "true" : "false");
				case 2 when value.Length == 4:
					return Plain(name, Float(value));
				case 3 when value.Length == 4:
					return Plain(name, BinaryPrimitives.ReadInt32LittleEndian(value).ToString(CultureInfo.InvariantCulture));
				case 5 or 6 or 13 when value.Length % 4 == 0:
					return Plain(name, String.Join(", ", Floats(value)));
				case 14 when value.Length % 4 == 0:
					return Plain(name, String.Join(", ", Ints(value)));
				case 4 or 8 or 9 or 26:
				{
					Int32 at = start;
					UInt32 length = ReadUInt32(data, ref at, end);
					if (length != end - at)
						return Plain(name, Hex(value));

					// A string of type 4 can hold an entity or a group of properties.
					ReadOnlySpan<Byte> inner = data.AsSpan(at, end - at);
					if (inner.StartsWith(EntityTag))
						return Nested(name, data, at, end, depth, true);

					if (inner.StartsWith(PropertiesTag))
						return Nested(name, data, at, end, depth, false);

					return Plain(name, Encoding.Latin1.GetString(inner));
				}

				case 11:
				{
					// The size of the group, then the group itself.
					Int32 at = start;
					UInt32 length = ReadUInt32(data, ref at, end);
					if (length != end - at)
						return Plain(name, Hex(value));

					// A group without properties is stored as a size of 0.
					if (length == 0)
						return Plain(name, String.Empty);

					return Nested(name, data, at, end, depth, false);
				}

				default:
					return Plain(name, Hex(value));
			}
		}

		/// <summary>
		/// A nested entity or group. Some groups hold another tagged structure that is not understood (such as "&lt;attribs&gt;"): they are
		/// shown as their tag and their size.
		/// </summary>
		private static EntNode Nested(String name, Byte[] data, Int32 start, Int32 end, Int32 depth, Boolean isEntity)
		{
			Int32 at = start;
			try
			{
				if (isEntity)
				{
					(String className, IReadOnlyList<EntNode> properties) = ReadEntity(data, ref at, end, depth + 1);
					return new EntNode(name, className, properties);
				}

				if (data.AsSpan(start, end - start).StartsWith(PropertiesTag))
					return new EntNode(name, String.Empty, ReadProperties(data, ref at, end, depth + 1));
			}
			catch (InvalidDataException)
			{
				// Shown as binary data below.
			}

			ReadOnlySpan<Byte> inner = data.AsSpan(start, end - start);
			Int32 tagEnd = inner.IndexOf((Byte)'>');
			String tag = inner.Length > 0 && inner[0] == '<' && tagEnd is > 0 and < 32 ? Encoding.Latin1.GetString(inner[..(tagEnd + 1)]) + " " : String.Empty;
			return Plain(name, $"{tag}({inner.Length} bytes)");
		}

		private static EntNode Plain(String name, String value)
		{
			return new EntNode(name, value, []);
		}

		private static String Float(ReadOnlySpan<Byte> value)
		{
			return BinaryPrimitives.ReadSingleLittleEndian(value).ToString("0.######", CultureInfo.InvariantCulture);
		}

		private static List<String> Floats(ReadOnlySpan<Byte> value)
		{
			List<String> numbers = [];
			for (Int32 i = 0; i < value.Length; i += 4)
			{
				numbers.Add(Float(value[i..]));
			}

			return numbers;
		}

		private static List<String> Ints(ReadOnlySpan<Byte> value)
		{
			List<String> numbers = [];
			for (Int32 i = 0; i < value.Length; i += 4)
			{
				numbers.Add(BinaryPrimitives.ReadInt32LittleEndian(value[i..]).ToString(CultureInfo.InvariantCulture));
			}

			return numbers;
		}

		private static String Hex(ReadOnlySpan<Byte> value)
		{
			return Convert.ToHexString(value);
		}

		private static void SkipVersion(Byte[] data, ref Int32 at, Int32 end)
		{
			Int32 zero = data.AsSpan(at, end - at).IndexOf((Byte)0);
			if (zero < 0)
				throw new InvalidDataException("The entity is cut.");

			at += zero + 1;
		}

		private static UInt32 ReadUInt32(Byte[] data, ref Int32 at, Int32 end)
		{
			if (end - at < 4)
				throw new InvalidDataException("The entity is cut.");

			UInt32 value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at));
			at += 4;
			return value;
		}

		private static String ReadString(Byte[] data, ref Int32 at, Int32 end)
		{
			UInt32 length = ReadUInt32(data, ref at, end);
			if (length > MaxNameLength || length > end - at)
				throw new InvalidDataException("A name is cut.");

			String text = Encoding.Latin1.GetString(data, at, (Int32)length);
			at += (Int32)length;
			return text;
		}
	}
}
