namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;

	/// <summary>
	/// Decodes the pictures of a movie one after the other. A picture is made of 8x8 blocks; for each block, a code tells
	/// how it is made: copied from one of the two previous pictures (with a movement), or drawn with one to four colors,
	/// or given pixel by pixel. The pictures of the two previous frames are kept to copy from.
	/// </summary>
	public sealed class MveFrameReader
	{
		private const Int32 BlockSize = 8;

		private readonly MveMovie _movie;
		private readonly Int32 _width;
		private readonly Int32 _height;
		private readonly UInt32[] _palette = new UInt32[256];

		private Byte[] _current;
		private Byte[] _last;
		private Byte[] _secondLast;
		private Int32 _frame;
		private Int32 _index;
		private Int32 _mapOffset;
		private Int32 _mapLength;
		private Int32 _videoOffset = -1;
		private Int32 _videoLength;

		// The state of the decoding of one picture.
		private Int32 _position;
		private Int32 _end;

		internal MveFrameReader(MveMovie movie)
		{
			_movie = movie;
			_width = movie.Width;
			_height = movie.Height;
			_current = new Byte[_width * _height];
			_last = new Byte[_width * _height];
			_secondLast = new Byte[_width * _height];
			Rewind();
		}

		/// <summary>The number of bytes of a picture as <see cref="ReadFrame"/> gives it.</summary>
		public Int32 FrameBytes => _width * _height * 4;

		/// <summary>The number of the picture the next call of <see cref="ReadFrame"/> gives, from 0.</summary>
		public Int32 FramePosition => _frame;

		/// <summary>Goes back to before the first picture.</summary>
		public void Rewind()
		{
			_frame = 0;
			_index = 0;
			_mapOffset = 0;
			_mapLength = 0;
			_videoOffset = -1;
			Array.Clear(_current);
			Array.Clear(_last);
			Array.Clear(_secondLast);
			Array.Clear(_palette);
		}

		/// <summary>
		/// Moves to a picture, so that the next call of <see cref="ReadFrame"/> gives it. A picture depends on the two
		/// before it, so the decoding restarts from the last saved state before the picture (one is saved every
		/// <see cref="MveMovie.CheckpointInterval"/> pictures as they are decoded) and goes forward from there.
		/// </summary>
		public void SeekTo(Int32 frame)
		{
			frame = Math.Clamp(frame, 0, Math.Max(0, _movie.FrameCount - 1));
			if (frame != _frame)
			{
				MveCheckpoint? checkpoint = _movie.Checkpoints.FindAtOrBefore(frame);
				if (frame < _frame || (checkpoint != null && checkpoint.Frame > _frame))
				{
					if (checkpoint != null)
					{
						Restore(checkpoint);
					}
					else
					{
						Rewind();
					}
				}
			}

			while (_frame < frame && ReadFrame(null))
			{
			}
		}

		/// <summary>
		/// Decodes the next picture.
		/// </summary>
		/// <param name="bgra">Receives the picture as 32 bit pixels (blue, green, red, alpha), row after row; null to decode
		/// without making the picture (to go forward quickly).</param>
		/// <returns>False when there is no more picture.</returns>
		public Boolean ReadFrame(Byte[]? bgra)
		{
			Byte[] data = _movie.Data;
			while (_index < _movie.Segments.Count)
			{
				MveSegment segment = _movie.Segments[_index++];
				switch (segment.Opcode)
				{
					case 0x0C when segment.Length >= 4:
						SetPalette(data.AsSpan(segment.Offset, segment.Length));
						break;

					case 0x0F:
						_mapOffset = segment.Offset;
						_mapLength = segment.Length;
						break;

					case 0x11:
						_videoOffset = segment.Offset;
						_videoLength = segment.Length;
						break;

					case 0x07:
						DecodePicture();
						if (bgra != null)
						{
							Present(bgra);
						}

						Advance();
						_frame++;
						if (_frame % MveMovie.CheckpointInterval == 0 && !_movie.Checkpoints.Contains(_frame))
						{
							_movie.Checkpoints.Add(Capture());
						}

						return true;
				}
			}

			return false;
		}

		private MveCheckpoint Capture()
		{
			return new MveCheckpoint(_frame, _index, _mapOffset, _mapLength, (UInt32[])_palette.Clone(), (Byte[])_current.Clone(), (Byte[])_last.Clone(), (Byte[])_secondLast.Clone());
		}

		private void Restore(MveCheckpoint checkpoint)
		{
			_frame = checkpoint.Frame;
			_index = checkpoint.SegmentIndex;
			_mapOffset = checkpoint.MapOffset;
			_mapLength = checkpoint.MapLength;
			_videoOffset = -1;
			Array.Copy(checkpoint.Palette, _palette, _palette.Length);
			Array.Copy(checkpoint.Current, _current, _current.Length);
			Array.Copy(checkpoint.Last, _last, _last.Length);
			Array.Copy(checkpoint.SecondLast, _secondLast, _secondLast.Length);
		}

		private void SetPalette(ReadOnlySpan<Byte> body)
		{
			Int32 first = BinaryPrimitives.ReadUInt16LittleEndian(body);
			Int32 count = BinaryPrimitives.ReadUInt16LittleEndian(body[2..]);
			for (Int32 i = 0; i < count && first + i < 256 && 4 + (i * 3) + 2 < body.Length; i++)
			{
				// The colors are 6 bits, shifted to 8 bits.
				UInt32 red = (UInt32)((body[4 + (i * 3)] << 2) & 0xFF);
				UInt32 green = (UInt32)((body[5 + (i * 3)] << 2) & 0xFF);
				UInt32 blue = (UInt32)((body[6 + (i * 3)] << 2) & 0xFF);
				_palette[first + i] = 0xFF000000 | (red << 16) | (green << 8) | blue;
			}
		}

		private void Present(Byte[] bgra)
		{
			Int32 length = Math.Min(_current.Length, bgra.Length / 4);
			for (Int32 i = 0; i < length; i++)
			{
				UInt32 color = _palette[_current[i]];
				bgra[i * 4] = (Byte)color;
				bgra[(i * 4) + 1] = (Byte)(color >> 8);
				bgra[(i * 4) + 2] = (Byte)(color >> 16);
				bgra[(i * 4) + 3] = 0xFF;
			}
		}

		/// <summary>
		/// The picture just shown becomes the previous one. The next picture is drawn over the one from two frames ago, as
		/// the movie was made for two screens used in turn.
		/// </summary>
		private void Advance()
		{
			Byte[] recycled = _secondLast;
			_secondLast = _last;
			_last = _current;
			_current = recycled;
			Array.Copy(_secondLast, _current, _current.Length);
			_videoOffset = -1;
		}

		private void DecodePicture()
		{
			if (_videoOffset < 0)
			{
				return;
			}

			// The data of the picture starts after a header of 14 bytes.
			_position = _videoOffset + 14;
			_end = _videoOffset + _videoLength;
			Byte[] data = _movie.Data;

			Int32 block = 0;
			for (Int32 y = 0; y < _height; y += BlockSize)
			{
				for (Int32 x = 0; x < _width; x += BlockSize)
				{
					Int32 mapIndex = block >> 1;
					Int32 packed = mapIndex < _mapLength ? data[_mapOffset + mapIndex] : 0;
					Int32 opcode = (block & 1) == 0 ? packed & 0x0F : packed >> 4;
					block++;
					DecodeBlock(opcode, x, y);
				}
			}
		}

		private Int32 ReadByte()
		{
			// A picture that is cut short reads as zeros.
			return _position < _end ? _movie.Data[_position++] : 0;
		}

		private Int32 ReadUInt16()
		{
			Int32 low = ReadByte();
			return low | (ReadByte() << 8);
		}

		private Int64 ReadBits(Int32 byteCount)
		{
			Int64 value = 0;
			for (Int32 i = 0; i < byteCount; i++)
			{
				value |= (Int64)ReadByte() << (8 * i);
			}

			return value;
		}

		private void Set(Int32 blockX, Int32 blockY, Int32 x, Int32 y, Int32 color)
		{
			_current[((blockY + y) * _width) + blockX + x] = (Byte)color;
		}

		private void Fill(Int32 blockX, Int32 blockY, Int32 x, Int32 y, Int32 width, Int32 height, Int32 color)
		{
			for (Int32 row = 0; row < height; row++)
			{
				Array.Fill(_current, (Byte)color, ((blockY + y + row) * _width) + blockX + x, width);
			}
		}

		/// <summary>Copies a block of another picture, the movement being added to the position of the block.</summary>
		private void CopyBlock(Byte[] source, Int32 blockX, Int32 blockY, Int32 movementX, Int32 movementY)
		{
			Int32 x = blockX + movementX;
			Int32 y = blockY + movementY;
			if (x >= _width)
			{
				x -= _width;
				y++;
			}

			if (x < 0)
			{
				x += _width;
				y--;
			}

			Int32 offset = (y * _width) + x;
			Int32 limit = ((_height - BlockSize) * _width) + (_width - BlockSize);
			if (offset < 0 || offset > limit)
			{
				return;
			}

			for (Int32 row = 0; row < BlockSize; row++)
			{
				Array.Copy(source, offset + (row * _width), _current, ((blockY + row) * _width) + blockX, BlockSize);
			}
		}

		private void DecodeBlock(Int32 opcode, Int32 bx, Int32 by)
		{
			switch (opcode)
			{
				case 0x0:
					CopyBlock(_last, bx, by, 0, 0);
					break;

				case 0x1:
					CopyBlock(_secondLast, bx, by, 0, 0);
					break;

				case 0x2:
					{
						Int32 b = ReadByte();
						(Int32 x, Int32 y) = b < 56 ? (8 + (b % 7), b / 7) : (-14 + ((b - 56) % 29), 8 + ((b - 56) / 29));
						CopyBlock(_secondLast, bx, by, x, y);
						break;
					}

				case 0x3:
					{
						Int32 b = ReadByte();
						(Int32 x, Int32 y) = b < 56 ? (-(8 + (b % 7)), -(b / 7)) : (-(-14 + ((b - 56) % 29)), -(8 + ((b - 56) / 29)));
						CopyBlock(_current, bx, by, x, y);
						break;
					}

				case 0x4:
					{
						Int32 b = ReadByte();
						CopyBlock(_last, bx, by, (b & 0x0F) - 8, (b >> 4) - 8);
						break;
					}

				case 0x5:
					{
						Int32 x = (SByte)ReadByte();
						Int32 y = (SByte)ReadByte();
						CopyBlock(_last, bx, by, x, y);
						break;
					}

				case 0x6:
					// Not used by any movie: the block is left as it is.
					break;

				case 0x7:
					TwoColors(bx, by);
					break;

				case 0x8:
					TwoColorsPerPart(bx, by);
					break;

				case 0x9:
					FourColors(bx, by);
					break;

				case 0xA:
					FourColorsPerPart(bx, by);
					break;

				case 0xB:
					for (Int32 y = 0; y < BlockSize; y++)
					{
						for (Int32 x = 0; x < BlockSize; x++)
						{
							Set(bx, by, x, y, ReadByte());
						}
					}

					break;

				case 0xC:
					for (Int32 k = 0; k < 16; k++)
					{
						Fill(bx, by, (k % 4) * 2, (k / 4) * 2, 2, 2, ReadByte());
					}

					break;

				case 0xD:
					for (Int32 half = 0; half < 2; half++)
					{
						Fill(bx, by, 0, half * 4, 4, 4, ReadByte());
						Fill(bx, by, 4, half * 4, 4, 4, ReadByte());
					}

					break;

				case 0xE:
					Fill(bx, by, 0, 0, BlockSize, BlockSize, ReadByte());
					break;

				case 0xF:
					{
						Int32 first = ReadByte();
						Int32 second = ReadByte();
						for (Int32 y = 0; y < BlockSize; y++)
						{
							for (Int32 x = 0; x < BlockSize; x++)
							{
								Set(bx, by, x, y, ((x + y) & 1) == 0 ? first : second);
							}
						}

						break;
					}
			}
		}

		/// <summary>Opcode 7: two colors, one bit per pixel (8 bytes) or one bit per 2x2 pixels (2 bytes).</summary>
		private void TwoColors(Int32 bx, Int32 by)
		{
			Int32 p0 = ReadByte();
			Int32 p1 = ReadByte();
			if (p0 <= p1)
			{
				for (Int32 y = 0; y < BlockSize; y++)
				{
					Int32 flags = ReadByte();
					for (Int32 x = 0; x < BlockSize; x++)
					{
						Set(bx, by, x, y, ((flags >> x) & 1) == 0 ? p0 : p1);
					}
				}
			}
			else
			{
				Int32 flags = ReadUInt16();
				for (Int32 k = 0; k < 16; k++)
				{
					Fill(bx, by, (k % 4) * 2, (k / 4) * 2, 2, 2, ((flags >> k) & 1) == 0 ? p0 : p1);
				}
			}
		}

		/// <summary>Draws a part of a block (a quadrant, a half) with two colors, one bit per pixel.</summary>
		private void TwoColorPart(Int32 bx, Int32 by, Int32 left, Int32 top, Int32 width, Int32 height, Int32 p0, Int32 p1, Int64 flags)
		{
			Int32 bit = 0;
			for (Int32 y = 0; y < height; y++)
			{
				for (Int32 x = 0; x < width; x++)
				{
					Set(bx, by, left + x, top + y, ((flags >> bit++) & 1) == 0 ? p0 : p1);
				}
			}
		}

		/// <summary>Opcode 8: two colors for each quadrant, or for each half of the block.</summary>
		private void TwoColorsPerPart(Int32 bx, Int32 by)
		{
			Int32 p0 = ReadByte();
			Int32 p1 = ReadByte();
			if (p0 <= p1)
			{
				// Four quadrants, each with its own two colors: upper left, lower left, upper right, lower right.
				(Int32 Left, Int32 Top)[] quadrants = [(0, 0), (0, 4), (4, 0), (4, 4)];
				for (Int32 q = 0; q < 4; q++)
				{
					if (q > 0)
					{
						p0 = ReadByte();
						p1 = ReadByte();
					}

					TwoColorPart(bx, by, quadrants[q].Left, quadrants[q].Top, 4, 4, p0, p1, ReadUInt16());
				}

				return;
			}

			Int64 flags1 = ReadBits(4);
			Int32 p2 = ReadByte();
			Int32 p3 = ReadByte();
			if (p2 <= p3)
			{
				TwoColorPart(bx, by, 0, 0, 4, 8, p0, p1, flags1);
				TwoColorPart(bx, by, 4, 0, 4, 8, p2, p3, ReadBits(4));
			}
			else
			{
				TwoColorPart(bx, by, 0, 0, 8, 4, p0, p1, flags1);
				TwoColorPart(bx, by, 0, 4, 8, 4, p2, p3, ReadBits(4));
			}
		}

		/// <summary>Opcode 9: four colors, per pixel, per 2x2 pixels, per horizontal pair or per vertical pair.</summary>
		private void FourColors(Int32 bx, Int32 by)
		{
			Int32[] p = [ReadByte(), ReadByte(), ReadByte(), ReadByte()];
			if (p[0] <= p[1])
			{
				if (p[2] <= p[3])
				{
					for (Int32 y = 0; y < BlockSize; y++)
					{
						Int32 flags = ReadUInt16();
						for (Int32 x = 0; x < BlockSize; x++)
						{
							Set(bx, by, x, y, p[(flags >> (2 * x)) & 3]);
						}
					}
				}
				else
				{
					Int64 flags = ReadBits(4);
					for (Int32 k = 0; k < 16; k++)
					{
						Fill(bx, by, (k % 4) * 2, (k / 4) * 2, 2, 2, p[(Int32)((flags >> (2 * k)) & 3)]);
					}
				}

				return;
			}

			Int64 all = ReadBits(8);
			if (p[2] <= p[3])
			{
				for (Int32 y = 0; y < BlockSize; y++)
				{
					for (Int32 pair = 0; pair < 4; pair++)
					{
						Fill(bx, by, pair * 2, y, 2, 1, p[(Int32)((all >> (2 * ((y * 4) + pair))) & 3)]);
					}
				}
			}
			else
			{
				for (Int32 pair = 0; pair < 4; pair++)
				{
					for (Int32 x = 0; x < BlockSize; x++)
					{
						Fill(bx, by, x, pair * 2, 1, 2, p[(Int32)((all >> (2 * ((pair * 8) + x))) & 3)]);
					}
				}
			}
		}

		/// <summary>Draws a part of a block with four colors, two bits per pixel.</summary>
		private void FourColorPart(Int32 bx, Int32 by, Int32 left, Int32 top, Int32 width, Int32 height, Int32[] palette, Int64 flags)
		{
			Int32 shift = 0;
			for (Int32 y = 0; y < height; y++)
			{
				for (Int32 x = 0; x < width; x++)
				{
					Set(bx, by, left + x, top + y, palette[(Int32)((flags >> shift) & 3)]);
					shift += 2;
				}
			}
		}

		/// <summary>Opcode 10: four colors for each quadrant, or for each half of the block.</summary>
		private void FourColorsPerPart(Int32 bx, Int32 by)
		{
			Int32[] p = [ReadByte(), ReadByte(), ReadByte(), ReadByte()];
			if (p[0] <= p[1])
			{
				(Int32 Left, Int32 Top)[] quadrants = [(0, 0), (0, 4), (4, 0), (4, 4)];
				for (Int32 q = 0; q < 4; q++)
				{
					if (q > 0)
					{
						p = [ReadByte(), ReadByte(), ReadByte(), ReadByte()];
					}

					FourColorPart(bx, by, quadrants[q].Left, quadrants[q].Top, 4, 4, p, ReadBits(4));
				}

				return;
			}

			Int64 flags1 = ReadBits(8);
			Int32[] second = [ReadByte(), ReadByte(), ReadByte(), ReadByte()];
			if (second[0] <= second[1])
			{
				FourColorPart(bx, by, 0, 0, 4, 8, p, flags1);
				FourColorPart(bx, by, 4, 0, 4, 8, second, ReadBits(8));
			}
			else
			{
				FourColorPart(bx, by, 0, 0, 8, 4, p, flags1);
				FourColorPart(bx, by, 0, 4, 8, 4, second, ReadBits(8));
			}
		}
	}
}