namespace DatExplorerIII.Formats.Prototypes
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Models.Prototypes;

	/// <summary>
	/// Reads the prototype files (.pro) of Arcanum: the description of an object of the game. All numbers are little endian.
	/// </summary>
	/// <remarks>
	/// <para>The file starts like the files of the objects of a map: the version (0x77, or 8), the identifier of the prototype
	/// (24 bytes; for a prototype its type is -1), the identifier of the object (24 bytes; its first number is the number of the
	/// prototype), the kind of object, then a bitmap of 12, 16 or 20 bytes (by kind of object). The values follow: those of every
	/// field of the kind of object (not only the ones whose bit is set in the bitmap), in the order of their numbers; see
	/// <see cref="ArcanumFields"/>. An array is a byte that tells if it is there, the size of an element, the number of elements, a
	/// number that is only a memory address, the elements, and a bitmap of those in use. An identifier of an object, a text and a
	/// 64 bit number are also preceded by a byte that tells if they are there.</para>
	/// <para>A file of Fallout starts with a big endian identifier whose first byte is 0 to 5, so a file of Arcanum is told from
	/// a file of Fallout by its first byte (7 or 0x77): see <see cref="IsArcanumPro"/>.</para>
	/// </remarks>
	public static class ArcanumProReader
	{
		private const Int32 IdentifierSize = 24;
		private const Int32 MaxListedElements = 8;

		/// <summary>
		/// True when the data is a prototype of Arcanum: it starts with the version of the format, then the identifier of a
		/// prototype (a type of -1). A prototype of Fallout starts with a number whose first byte is 0 to 5.
		/// </summary>
		public static Boolean IsArcanumPro(ReadOnlySpan<Byte> data)
		{
			return data.Length >= 4 + IdentifierSize + IdentifierSize + 4
				&& (data[0] == 0x77 || data[0] == 0x08)
				&& data[1] == 0 && data[2] == 0 && data[3] == 0
				&& BinaryPrimitives.ReadInt16LittleEndian(data[4..]) == -1;
		}

		/// <summary>
		/// Reads a prototype. A field of a kind that is not known stops the reading: what was read is returned with a note.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is not a prototype of Arcanum.</exception>
		public static ArcanumProto Read(Byte[] data)
		{
			if (!IsArcanumPro(data))
				throw new InvalidDataException("This is not a prototype of Arcanum.");

			Int32 version = BinaryPrimitives.ReadInt32LittleEndian(data);

			// The first number of the union that follows the 8 first bytes of the identifier of the object.
			Int32 number = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4 + IdentifierSize + 8));
			UInt32 rawType = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4 + IdentifierSize + IdentifierSize));
			if (rawType > (UInt32)ArcanumObjectType.Trap)
				throw new InvalidDataException("The kind of object is unknown.");

			ArcanumObjectType type = (ArcanumObjectType)rawType;
			Int32 position = 4 + IdentifierSize + IdentifierSize + 4;
			Int32 bitmapSize = ArcanumFields.BitmapSize(type);
			if (position + bitmapSize > data.Length)
				throw new InvalidDataException("The file is shorter than its header.");

			Byte[] bitmap = data.AsSpan(position, bitmapSize).ToArray();
			position += bitmapSize;

			List<ArcanumProperty> properties = [];
			String? note = null;

			// A prototype holds every field of its kind of object, in the order of their numbers; the bitmap only marks some of
			// them. The numbers that are not a field of the kind of object (the gaps between the fields of the kind of object
			// and the ones all objects have) hold nothing.
			for (Int32 bit = 0; bit < bitmapSize * 8 && note == null; bit++)
			{
				if (!ArcanumFields.TryGetWireType(type, bit, out ArcanumWireType wire))
				{
					if ((bitmap[bit / 8] & (1 << (bit % 8))) != 0)
						note = "Field " + bit + " of this kind of object is not known: the next fields cannot be read.";

					continue;
				}

				try
				{
					String value = ReadValue(data, ref position, wire);
					properties.Add(new ArcanumProperty(bit, ArcanumFields.NameOf(type, bit), value));
				}
				catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException or OverflowException)
				{
					note = "The field " + ArcanumFields.NameOf(type, bit) + " cannot be read: " + exception.Message;
				}
			}

			if (note == null && position < data.Length)
				note = (data.Length - position) + " bytes follow the last field.";

			return new ArcanumProto(version, number, type, properties, note);
		}

		private static String ReadValue(Byte[] data, ref Int32 position, ArcanumWireType wire)
		{
			switch (wire)
			{
				case ArcanumWireType.Int32:
					return ReadInt32(data, ref position).ToString(CultureInfo.InvariantCulture);

				case ArcanumWireType.Float:
					return BitConverter.Int32BitsToSingle(ReadInt32(data, ref position)).ToString("0.###", CultureInfo.InvariantCulture);

				case ArcanumWireType.Rgb24:
					Need(data, position, 3);
					String color = $"#{data[position]:X2}{data[position + 1]:X2}{data[position + 2]:X2}";
					position += 3;
					return color;

				case ArcanumWireType.ObjectId:
					if (!ReadPresence(data, ref position))
						return "\u2014";

					Need(data, position, IdentifierSize);
					String label = DescribeIdentifier(data.AsSpan(position, IdentifierSize));
					position += IdentifierSize;
					return label;

				case ArcanumWireType.Int64:
					if (!ReadPresence(data, ref position))
						return String.Empty;

					Need(data, position, 8);
					Int64 big = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(position));
					position += 8;
					return big.ToString(CultureInfo.InvariantCulture);

				case ArcanumWireType.String:
					return ReadString(data, ref position);

				default:
					return ReadArray(data, ref position, wire);
			}
		}

		private static Boolean ReadPresence(Byte[] data, ref Int32 position)
		{
			Need(data, position, 1);
			return data[position++] != 0;
		}

		private static Int32 ReadInt32(Byte[] data, ref Int32 position)
		{
			Need(data, position, 4);
			Int32 value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(position));
			position += 4;
			return value;
		}

		private static void Need(Byte[] data, Int32 position, Int64 count)
		{
			if (count < 0 || position + count > data.Length)
				throw new InvalidDataException("The file ends too early.");
		}

		/// <summary>A text: a byte (is it there), its length, then its characters and a zero.</summary>
		private static String ReadString(Byte[] data, ref Int32 position)
		{
			if (!ReadPresence(data, ref position))
				return String.Empty;

			Int32 length = ReadInt32(data, ref position);
			if (length < 0)
				throw new InvalidDataException("A text has a negative length.");

			Need(data, position, (Int64)length + 1);
			String text = Encoding.Latin1.GetString(data, position, length);
			position += length + 1;
			return text;
		}

		/// <summary>
		/// An array: a byte (is it there), the size of an element, the number of elements, a number that is only a memory
		/// address, the elements, then a bitmap of the elements in use.
		/// </summary>
		private static String ReadArray(Byte[] data, ref Int32 position, ArcanumWireType wire)
		{
			if (!ReadPresence(data, ref position))
				return "[]";

			Need(data, position, 12);
			UInt32 elementSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
			UInt32 count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 4));
			position += 12;

			Int64 length = (Int64)elementSize * count;
			Need(data, position, length + 4);
			Int32 start = position;
			position += (Int32)length;

			UInt32 bitmapWords = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position));
			position += 4;
			Need(data, position, (Int64)bitmapWords * 4);
			position += (Int32)(bitmapWords * 4);

			// An array that was never filled has no size and a count of -1.
			if (count == 0 || elementSize == 0)
				return "[]";

			List<String> shown = [];
			for (Int32 i = 0; i < Math.Min(count, MaxListedElements); i++)
			{
				ReadOnlySpan<Byte> element = data.AsSpan(start + (Int32)(i * elementSize), (Int32)elementSize);
				shown.Add(DescribeElement(element, wire));
			}

			String list = String.Join(", ", shown) + (count > MaxListedElements ? ", …" : String.Empty);
			return $"[{count}] {list}";
		}

		private static String DescribeElement(ReadOnlySpan<Byte> element, ArcanumWireType wire)
		{
			switch (wire)
			{
				case ArcanumWireType.Int32Array when element.Length >= 4:
					return BinaryPrimitives.ReadInt32LittleEndian(element).ToString(CultureInfo.InvariantCulture);

				case ArcanumWireType.UInt32Array when element.Length >= 4:
					return BinaryPrimitives.ReadUInt32LittleEndian(element).ToString(CultureInfo.InvariantCulture);

				case ArcanumWireType.Int64Array when element.Length >= 8:
					return BinaryPrimitives.ReadInt64LittleEndian(element).ToString(CultureInfo.InvariantCulture);

				case ArcanumWireType.HandleArray when element.Length >= IdentifierSize:
					return DescribeIdentifier(element);

				default:
					// A script, a quest: the numbers that make it.
					List<String> numbers = [];
					for (Int32 i = 0; i + 4 <= element.Length && i < 24; i += 4)
					{
						numbers.Add(BinaryPrimitives.ReadInt32LittleEndian(element[i..]).ToString(CultureInfo.InvariantCulture));
					}

					return "(" + String.Join(" ", numbers) + ")";
			}
		}

		/// <summary>An identifier of an object: none, the number of a prototype, or the GUID of an object.</summary>
		private static String DescribeIdentifier(ReadOnlySpan<Byte> id)
		{
			Int16 type = BinaryPrimitives.ReadInt16LittleEndian(id);
			return type switch
			{
				0 => "—",
				1 => "#" + BinaryPrimitives.ReadInt32LittleEndian(id[8..]).ToString(CultureInfo.InvariantCulture),
				2 => new Guid(id.Slice(8, 16)).ToString()[..8] + "…",
				-1 => "#" + BinaryPrimitives.ReadInt32LittleEndian(id[8..]).ToString(CultureInfo.InvariantCulture),
				_ => "?",
			};
		}
	}
}