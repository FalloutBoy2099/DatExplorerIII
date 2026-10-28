namespace DatExplorerIII.Formats.Graphics
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models.Graphics;

	/// <summary>
	/// Reads the pictures of the "Quite OK Image" format (.qoi): the signature "qoif", the width and the height (32 bits, big endian), the
	/// count of channels (3 or 4) and the color space, then chunks (a full pixel, a difference with the previous pixel, an index in the
	/// 64 pixels seen last, or a run of the same pixel), and an end marker of 7 zeros and a one.
	/// </summary>
	public static class QoiReader
	{
		/// <summary>The pictures larger than this (in pixels) are not decoded: they would take too much memory.</summary>
		private const Int64 MaxPixels = 100L * 1024 * 1024;

		private const Int32 HeaderSize = 14;
		private const Int32 EndMarkerSize = 8;

		/// <summary>True when the data starts like a QOI file.</summary>
		public static Boolean IsQoi(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize && data.StartsWith("qoif"u8);
		}

		/// <exception cref="InvalidDataException">The data is not a valid QOI file.</exception>
		public static PixelImage Read(Byte[] data)
		{
			if (!IsQoi(data) || data.Length < HeaderSize + EndMarkerSize)
				throw new InvalidDataException("This is not a QOI picture.");

			UInt32 width = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
			UInt32 height = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8));
			if (width == 0 || height == 0 || (Int64)width * height > MaxPixels)
				throw new InvalidDataException("The size of the picture is impossible.");

			if (data[12] is not (3 or 4))
				throw new InvalidDataException("The count of channels is impossible.");

			Int32 total = (Int32)(width * height);
			Byte[] pixels = new Byte[total * 4];
			Byte[] seen = new Byte[64 * 4];
			Byte red = 0, green = 0, blue = 0, alpha = 255;
			Int32 position = HeaderSize;
			Int32 limit = data.Length - EndMarkerSize;
			Int32 run = 0;

			for (Int32 i = 0; i < total; i++)
			{
				if (run > 0)
				{
					run--;
				}
				else
				{
					if (position >= limit)
						throw new InvalidDataException("The picture is incomplete.");

					Byte tag = data[position++];
					if (tag == 0xFE)
					{
						if (limit - position < 3)
							throw new InvalidDataException("The picture is incomplete.");

						red = data[position++];
						green = data[position++];
						blue = data[position++];
					}
					else if (tag == 0xFF)
					{
						if (limit - position < 4)
							throw new InvalidDataException("The picture is incomplete.");

						red = data[position++];
						green = data[position++];
						blue = data[position++];
						alpha = data[position++];
					}
					else
					{
						switch (tag >> 6)
						{
							case 0:
								// An index in the pixels seen last.
								Int32 index = (tag & 0x3F) * 4;
								red = seen[index];
								green = seen[index + 1];
								blue = seen[index + 2];
								alpha = seen[index + 3];
								break;
							case 1:
								red = (Byte)(red + ((tag >> 4) & 3) - 2);
								green = (Byte)(green + ((tag >> 2) & 3) - 2);
								blue = (Byte)(blue + (tag & 3) - 2);
								break;
							case 2:
								if (position >= limit)
									throw new InvalidDataException("The picture is incomplete.");

								Int32 diffGreen = (tag & 0x3F) - 32;
								Byte second = data[position++];
								red = (Byte)(red + diffGreen - 8 + (second >> 4));
								green = (Byte)(green + diffGreen);
								blue = (Byte)(blue + diffGreen - 8 + (second & 0x0F));
								break;
							default:
								// The pixel is repeated, this time and the next ones.
								run = tag & 0x3F;
								break;
						}
					}

					Int32 hash = ((red * 3) + (green * 5) + (blue * 7) + (alpha * 11)) % 64 * 4;
					seen[hash] = red;
					seen[hash + 1] = green;
					seen[hash + 2] = blue;
					seen[hash + 3] = alpha;
				}

				Int32 offset = i * 4;
				pixels[offset] = blue;
				pixels[offset + 1] = green;
				pixels[offset + 2] = red;
				pixels[offset + 3] = alpha;
			}

			return new PixelImage((Int32)width, (Int32)height, pixels);
		}
	}
}
