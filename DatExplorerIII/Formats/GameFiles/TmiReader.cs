namespace DatExplorerIII.Formats.GameFiles
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models.GameFiles;

	/// <summary>
	/// Reads the information of a town map of Arcanum (.tmi, 48 bytes of 32 bit numbers): the version (2), a zero, the number of the
	/// map, the width and the height of the picture in pixels, the width and the height of the map in tiles (the size of the .tma),
	/// a zero or -1, two numbers that place the map, the scale of the picture (0.25) and a number that is not understood.
	/// </summary>
	public static class TmiReader
	{
		public const Int32 FileSize = 48;

		/// <exception cref="InvalidDataException">The data is not the information of a town map.</exception>
		public static TmiFile Read(Byte[] data)
		{
			if (data.Length != FileSize || BinaryPrimitives.ReadUInt32LittleEndian(data) != 2)
				throw new InvalidDataException("This is not the information of a town map.");

			Int32 At(Int32 index) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(index * 4));

			return new TmiFile(
				At(0),
				At(2),
				At(3),
				At(4),
				At(5),
				At(6),
				At(7),
				At(8),
				At(9),
				BitConverter.Int32BitsToSingle(At(10)),
				At(11),
				At(1));
		}
	}
}
