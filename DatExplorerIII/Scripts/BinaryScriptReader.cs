namespace DatExplorerIII.Scripts
{
	using System;
	using System.Buffers.Binary;

	/// <summary>
	/// Reads the numbers of a compiled script (big endian) and fails with a <see cref="ScriptFormatException"/> at the end of the file.
	/// </summary>
	internal sealed class BinaryScriptReader
	{
		private readonly Byte[] _data;

		public BinaryScriptReader(Byte[] data)
		{
			_data = data;
		}

		public UInt32 Position { get; private set; }
		public UInt32 Length => (UInt32)_data.Length;

		public void Seek(UInt32 position)
		{
			if (position > _data.Length)
			{
				throw new ScriptFormatException("Error: Seek past end of input");
			}

			Position = position;
		}

		public UInt16 ReadUInt16()
		{
			Ensure(2);
			UInt16 value = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan((Int32)Position, 2));
			Position += 2;
			return value;
		}

		public UInt32 ReadUInt32()
		{
			Ensure(4);
			UInt32 value = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan((Int32)Position, 4));
			Position += 4;
			return value;
		}

		public Byte[] ReadBytes(Int32 length)
		{
			Ensure((UInt32)length);
			Byte[] result = new Byte[length];
			Array.Copy(_data, Position, result, 0, length);
			Position += (UInt32)length;
			return result;
		}

		private void Ensure(UInt32 count)
		{
			if (Position + count > _data.Length)
			{
				throw new ScriptFormatException("Error: Unexpected end of input");
			}
		}
	}
}