namespace DatExplorerIII.Formats
{
	using System;
	using System.IO;
	using Models;

	/// <summary>
	/// Reads the Interplay ACM sound files (.acm) of Fallout and decodes them to a playable WAV.
	/// </summary>
	/// <remarks>
	/// This is a port to C# of the ACM decoder of libacm (https://github.com/markokr/libacm).
	/// Copyright (c) 2004-2010, Marko Kreen. Permission to use, copy, modify, and/or distribute this software for any
	/// purpose with or without fee is hereby granted, provided that the above copyright notice and this permission
	/// notice appear in all copies. The software is provided "as is" and the author disclaims all warranties.
	/// </remarks>
	public static class AcmReader
	{
		private const Int32 EnvelopeLength = 1000;

		/// <summary>Decoded sound larger than this is cut (a 20 minute stereo sound at 22 kHz is about 105 MB).</summary>
		private const Int64 MaxPcmBytes = 256L * 1024 * 1024;

		/// <summary>True when the data starts like an ACM file (plain, or wrapped as a "WAVC" file).</summary>
		public static Boolean IsAcm(ReadOnlySpan<Byte> data)
		{
			return data.Length >= 14 && ((data[0] == 0x97 && data[1] == 0x28 && data[2] == 0x03) || (data[0] == (Byte)'W' && data[1] == (Byte)'A' && data[2] == (Byte)'V' && data[3] == (Byte)'C'));
		}

		/// <summary>
		/// Decodes a sound.
		/// </summary>
		/// <param name="data">The content of the .acm file.</param>
		/// <param name="forcedChannels">0 to find the number of channels (the header says stereo for most of the speech and
		/// the sound effects, which are mono, so the decoded sound is examined), 1 or 2 to choose it.</param>
		/// <exception cref="InvalidDataException">The data is not an ACM sound, or is damaged from its first block.</exception>
		public static AudioClip Read(Byte[] data, Int32 forcedChannels = 0)
		{
			AcmDecoder decoder = new(data, forcedChannels);
			Byte[] pcm = decoder.DecodeAll(MaxPcmBytes, out Boolean incomplete);
			if (pcm.Length == 0)
			{
				throw new InvalidDataException("The sound contains no data.");
			}

			Int32 channels = decoder.Channels;
			if (forcedChannels == 0 && channels == 2 && LooksMono(pcm))
			{
				// The samples are one stream: the left and right channels would play it twice as fast.
				channels = 1;
			}

			Int32 rate = decoder.SampleRate;
			TimeSpan duration = TimeSpan.FromSeconds(pcm.Length / 2.0 / channels / rate);
			return new AudioClip(rate, channels, duration, WavWriter.Create(pcm, rate, channels), WavWriter.CreateEnvelope(pcm, EnvelopeLength), incomplete);
		}

		/// <summary>
		/// Tells whether a sound decoded as stereo is really a mono sound. Read as one stream, a mono sound is smooth: a
		/// sample is closer to the previous one than to the one before it. In a stereo sound the neighbours are the other
		/// channel, which is not (a ratio of 4 to 12 for the music of Fallout 2, 0.5 to 1.2 for the speech). A stereo sound
		/// with the same signal on both sides is smooth too, but its two channels are identical, which a mono one never is.
		/// </summary>
		private static Boolean LooksMono(Byte[] pcm)
		{
			Int32 count = Math.Min(pcm.Length / 2, 4 * 1024 * 1024);
			Double near = 0;
			Double far = 0;
			Int32 pairs = 0;
			Int32 sameChannels = 0;
			Int32 previous = 0;
			Int32 beforePrevious = 0;

			for (Int32 i = 0; i < count; i++)
			{
				Int32 sample = (Int16)(pcm[i * 2] | (pcm[(i * 2) + 1] << 8));
				near += Math.Abs(sample - previous);
				far += Math.Abs(sample - beforePrevious);

				if ((i & 1) == 1 && (sample != 0 || previous != 0))
				{
					pairs++;
					sameChannels += sample == previous ? 1 : 0;
				}

				beforePrevious = previous;
				previous = sample;
			}

			if (far == 0 || (pairs > 0 && sameChannels * 2 > pairs))
			{
				return false;
			}

			return near < far * 2;
		}

		/// <summary>
		/// The ACM decoder: bits are read least significant first, a block holds <c>rows * cols</c> values.
		/// </summary>
		private sealed class AcmDecoder
		{
			private const Int32 AcmId = 0x032897;
			private const Int32 WavcId = 0x564157;
			private const Int32 MaxBlockLength = 1024 * 1024;
			private const Int32 MaxValues = 1 << 30;
			private const Int32 MidOffset = 0x8000;

			private static readonly Int32[] Map1Bit = [-1, +1];
			private static readonly Int32[] Map2BitNear = [-2, -1, +1, +2];
			private static readonly Int32[] Map2BitFar = [-3, -2, +2, +3];
			private static readonly Int32[] Map3Bit = [-4, -3, -2, -1, +1, +2, +3, +4];

			private readonly Byte[] _data;
			private readonly Int32 _level;
			private readonly Int32 _rows;
			private readonly Int32 _cols;
			private readonly Int32 _totalValues;
			private readonly Int32[] _block;
			private readonly Int32[] _wrap;
			private readonly Int32[] _amp = new Int32[0x10000];
			private readonly Action<Int32, Int32>[] _fillers;

			private UInt64 _buffer;
			private Int32 _bits;
			private Int32 _next;

			public AcmDecoder(Byte[] data, Int32 forcedChannels)
			{
				// Like libacm, the stream is followed by one zero byte.
				_data = new Byte[data.Length + 1];
				data.CopyTo(_data, 0);

				_fillers = BuildFillers();

				try
				{
					Int32 id = GetBits(24);
					if (id == WavcId)
					{
						if (GetBits(8) != 'C')
						{
							throw NotAcm();
						}

						ReadWavcHeader();
						id = GetBits(24);
					}

					if (id != AcmId || GetBits(8) != 1)
					{
						throw NotAcm();
					}

					_totalValues = GetBits(16);
					_totalValues += GetBits(16) << 16;
					if (_totalValues == 0 || _totalValues > MaxValues)
					{
						throw NotAcm();
					}

					Int32 channels = GetBits(16);
					if (channels < 1 || channels > 2)
					{
						throw NotAcm();
					}

					SampleRate = GetBits(16);
					if (SampleRate < 4096)
					{
						throw NotAcm();
					}

					_level = GetBits(4);
					_rows = GetBits(12);
					if (_rows == 0 || _level == 0)
					{
						throw NotAcm();
					}

					// The header of many game sounds says stereo for a mono sound. An odd number of values cannot be
					// stereo, so that case is certain; the other wrong headers cannot be told from the data.
					Channels = forcedChannels is 1 or 2 ? forcedChannels : channels == 2 && (_totalValues & 1) == 1 ? 1 : channels;
				}
				catch (EndOfStreamException)
				{
					throw NotAcm();
				}

				_cols = 1 << _level;
				Int32 blockLength = _rows * _cols;
				if (blockLength > MaxBlockLength)
				{
					throw new InvalidDataException("The blocks of this sound are too large.");
				}

				_block = new Int32[blockLength];
				_wrap = new Int32[(2 * _cols) - 2];
			}

			public Int32 Channels { get; }

			public Int32 SampleRate { get; }

			private static InvalidDataException NotAcm() => new("This is not an ACM sound.");

			/// <summary>Decodes the whole sound to 16 bit little endian samples.</summary>
			/// <param name="maxBytes">The decoding stops when this much sound was produced.</param>
			/// <param name="incomplete">True when the data ended or was damaged before the announced length.</param>
			public Byte[] DecodeAll(Int64 maxBytes, out Boolean incomplete)
			{
				using MemoryStream pcm = new();
				Int32 position = 0;
				incomplete = false;

				try
				{
					while (position < _totalValues)
					{
						if (!DecodeBlock())
						{
							break;
						}

						Int32 count = Math.Min(_block.Length, _totalValues - position);
						if (Channels > 1)
						{
							count -= count % Channels;
						}

						for (Int32 i = 0; i < count; i++)
						{
							Int32 value = _block[i] >> _level;
							pcm.WriteByte((Byte)(value & 0xFF));
							pcm.WriteByte((Byte)((value >> 8) & 0xFF));
						}

						position += count;
						if (pcm.Length >= maxBytes)
						{
							break;
						}
					}
				}
				catch (InvalidDataException)
				{
					// A damaged block ends the sound; what was decoded before it can still be heard.
					incomplete = true;
				}

				incomplete |= position < _totalValues;
				return pcm.ToArray();
			}

			private void ReadWavcHeader()
			{
				// 'WAVC' + 'V1.00' + sizes + 12 bytes: twelve 16 bit words, the magic number 28 is the seventh.
				Int32[] words = new Int32[12];
				for (Int32 i = 0; i < words.Length; i++)
				{
					words[i] = GetBits(16);
				}

				if (words[0] != 0x3156 || words[1] != 0x302E || words[6] != 28)
				{
					throw NotAcm();
				}
			}

			/// <summary>Reads up to 31 bits.</summary>
			private Int32 GetBits(Int32 count)
			{
				while (_bits < count)
				{
					if (_next >= _data.Length)
					{
						throw new EndOfStreamException();
					}

					_buffer |= (UInt64)_data[_next++] << _bits;
					_bits += 8;
				}

				Int32 value = (Int32)(_buffer & ((1UL << count) - 1));
				_buffer >>= count;
				_bits -= count;
				return value;
			}

			/// <summary>Decodes the next block. Returns false at the end of the data.</summary>
			private Boolean DecodeBlock()
			{
				Int32 power;
				Int32 value;
				try
				{
					power = GetBits(4);
					value = GetBits(16);
				}
				catch (EndOfStreamException)
				{
					return false;
				}

				// The table of amplitudes, indexed from -count to count - 1.
				Int32 count = 1 << power;
				Int32 x = 0;
				for (Int32 i = 0; i < count; i++)
				{
					_amp[MidOffset + i] = x;
					x += value;
				}

				x = -value;
				for (Int32 i = 1; i <= count; i++)
				{
					_amp[MidOffset - i] = x;
					x -= value;
				}

				for (Int32 column = 0; column < _cols; column++)
				{
					Int32 kind;
					try
					{
						kind = GetBits(5);
					}
					catch (EndOfStreamException)
					{
						// The data may end between two columns: that is the end of the sound.
						return false;
					}

					try
					{
						_fillers[kind](kind, column);
					}
					catch (EndOfStreamException exception)
					{
						throw new InvalidDataException("The sound is truncated.", exception);
					}
				}

				JuggleBlock();
				return true;
			}

			private void SetPosition(Int32 row, Int32 column, Int32 index)
			{
				_block[(row << _level) + column] = _amp[MidOffset + index];
			}

			#region Column decoders

			private Action<Int32, Int32>[] BuildFillers()
			{
				Action<Int32, Int32> bad = (_, _) => throw new InvalidDataException("The sound is damaged.");
				Action<Int32, Int32> linear = FillLinear;
				return
				[
					FillZero, bad, bad, linear,
					linear, linear, linear, linear,
					linear, linear, linear, linear,
					linear, linear, linear, linear,
					linear, FillK13, FillK12, FillT15,
					FillK24, FillK23, FillT27, FillK35,
					FillK34, bad, FillK45, FillK44,
					bad, FillT37, bad, bad,
				];
			}

			private void FillZero(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					SetPosition(i, column, 0);
				}
			}

			private void FillLinear(Int32 kind, Int32 column)
			{
				Int32 middle = 1 << (kind - 1);
				for (Int32 i = 0; i < _rows; i++)
				{
					SetPosition(i, column, GetBits(kind) - middle);
				}
			}

			private void FillK13(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						// 0: two zeros
						SetPosition(i++, column, 0);
						if (i >= _rows)
						{
							break;
						}

						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						// 1, 0: one zero
						SetPosition(i, column, 0);
						continue;
					}

					// 1, 1, ?
					SetPosition(i, column, Map1Bit[GetBits(1)]);
				}
			}

			private void FillK12(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					// 1, ?
					SetPosition(i, column, Map1Bit[GetBits(1)]);
				}
			}

			private void FillK24(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i++, column, 0);
						if (i >= _rows)
						{
							break;
						}

						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					// 1, 1, ?, ?
					SetPosition(i, column, Map2BitNear[GetBits(2)]);
				}
			}

			private void FillK23(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					// 1, ?, ?
					SetPosition(i, column, Map2BitNear[GetBits(2)]);
				}
			}

			private void FillK35(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i++, column, 0);
						if (i >= _rows)
						{
							break;
						}

						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						// 1, 1, 0, ?
						SetPosition(i, column, Map1Bit[GetBits(1)]);
						continue;
					}

					// 1, 1, 1, ?, ?
					SetPosition(i, column, Map2BitFar[GetBits(2)]);
				}
			}

			private void FillK34(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						// 1, 0, ?
						SetPosition(i, column, Map1Bit[GetBits(1)]);
						continue;
					}

					// 1, 1, ?, ?
					SetPosition(i, column, Map2BitFar[GetBits(2)]);
				}
			}

			private void FillK45(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						i++;
						if (i >= _rows)
						{
							break;
						}

						SetPosition(i, column, 0);
						continue;
					}

					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					// 1, 1, ?, ?, ?
					SetPosition(i, column, Map3Bit[GetBits(3)]);
				}
			}

			private void FillK44(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					if (GetBits(1) == 0)
					{
						SetPosition(i, column, 0);
						continue;
					}

					// 1, ?, ?, ?
					SetPosition(i, column, Map3Bit[GetBits(3)]);
				}
			}

			private void FillT15(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					// Three values of -1..1 packed in 5 bits: b = x1 + (x2 * 3) + (x3 * 9)
					Int32 b = GetBits(5);
					if (b >= 3 * 3 * 3)
					{
						throw new InvalidDataException("The sound is damaged.");
					}

					Int32 n1 = (b % 3) - 1;
					Int32 rest = b / 3;
					Int32 n2 = (rest % 3) - 1;
					Int32 n3 = (rest / 3) - 1;

					SetPosition(i++, column, n1);
					if (i >= _rows)
					{
						break;
					}

					SetPosition(i++, column, n2);
					if (i >= _rows)
					{
						break;
					}

					SetPosition(i, column, n3);
				}
			}

			private void FillT27(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					// Three values of -2..2 packed in 7 bits: b = x1 + (x2 * 5) + (x3 * 25)
					Int32 b = GetBits(7);
					if (b >= 5 * 5 * 5)
					{
						throw new InvalidDataException("The sound is damaged.");
					}

					Int32 n1 = (b % 5) - 2;
					Int32 rest = b / 5;
					Int32 n2 = (rest % 5) - 2;
					Int32 n3 = (rest / 5) - 2;

					SetPosition(i++, column, n1);
					if (i >= _rows)
					{
						break;
					}

					SetPosition(i++, column, n2);
					if (i >= _rows)
					{
						break;
					}

					SetPosition(i, column, n3);
				}
			}

			private void FillT37(Int32 kind, Int32 column)
			{
				for (Int32 i = 0; i < _rows; i++)
				{
					// Two values of -5..5 packed in 7 bits: b = x1 + (x2 * 11)
					Int32 b = GetBits(7);
					if (b >= 11 * 11)
					{
						throw new InvalidDataException("The sound is damaged.");
					}

					Int32 n1 = (b % 11) - 5;
					Int32 n2 = (b / 11) - 5;

					SetPosition(i++, column, n1);
					if (i >= _rows)
					{
						break;
					}

					SetPosition(i, column, n2);
				}
			}

			#endregion

			#region Synthesis

			private void JuggleBlock()
			{
				// Only a block with more than one column is juggled.
				if (_level == 0)
				{
					return;
				}

				Int32 stepSubCount = _level > 9 ? 1 : (2048 >> _level) - 2;

				// Applies the juggling from (stepSubCount * 2) x (cols / 2) to (stepSubCount * cols) x 1.
				Int32 todoCount = _rows;
				Int32 blockPosition = 0;
				while (true)
				{
					Int32 wrapPosition = 0;
					Int32 subCount = stepSubCount;
					if (subCount > todoCount)
					{
						subCount = todoCount;
					}

					Int32 subLength = _cols / 2;
					subCount *= 2;

					Juggle(wrapPosition, blockPosition, subLength, subCount);
					wrapPosition += subLength * 2;

					Int32 p = blockPosition;
					for (Int32 i = 0; i < subCount; i++)
					{
						_block[p]++;
						p += subLength;
					}

					while (subLength > 1)
					{
						subLength /= 2;
						subCount *= 2;
						Juggle(wrapPosition, blockPosition, subLength, subCount);
						wrapPosition += subLength * 2;
					}

					if (todoCount <= stepSubCount)
					{
						break;
					}

					todoCount -= stepSubCount;
					blockPosition += stepSubCount << _level;
				}
			}

			private void Juggle(Int32 wrapPosition, Int32 blockPosition, Int32 subLength, Int32 subCount)
			{
				for (Int32 i = 0; i < subLength; i++)
				{
					Int32 p = blockPosition;
					UInt32 r0 = (UInt32)_wrap[wrapPosition];
					UInt32 r1 = (UInt32)_wrap[wrapPosition + 1];

					for (Int32 j = 0; j < subCount / 2; j++)
					{
						UInt32 r2 = (UInt32)_block[p];
						_block[p] = (Int32)((r1 * 2) + (r0 + r2));
						p += subLength;

						UInt32 r3 = (UInt32)_block[p];
						_block[p] = (Int32)((r2 * 2) - (r1 + r3));
						p += subLength;

						r0 = r2;
						r1 = r3;
					}

					_wrap[wrapPosition++] = (Int32)r0;
					_wrap[wrapPosition++] = (Int32)r1;
					blockPosition++;
				}
			}

			#endregion
		}
	}
}