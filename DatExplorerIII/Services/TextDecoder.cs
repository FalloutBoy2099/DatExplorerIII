namespace DatExplorerIII.Services
{
	using System;
	using System.Text;
	using Models;

	/// <summary>
	/// Reads the text of a file whose encoding is not known: Fallout files are single byte texts of the language of the
	/// game (Windows-1252 for Western Europe, Windows-1251 for Russian, Windows-1250 for Polish and Czech...),
	/// but files made with today's tools are often UTF-8 or UTF-16.
	/// </summary>
	public static class TextDecoder
	{
		private const Int32 SampleLength = 8192;

		static TextDecoder()
		{
			// Gives access to the old code pages (437, 850, 866, 1250, 1251, 1252...).
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		/// <summary>
		/// Decodes a text.
		/// </summary>
		/// <param name="data">The bytes of the file.</param>
		/// <param name="codePage">The code page to read the text with, or <see cref="TextEncodingOption.Automatic"/> to detect it.</param>
		public static DecodedText Decode(Byte[] data, Int32 codePage = TextEncodingOption.Automatic)
		{
			if (codePage == TextEncodingOption.Automatic)
			{
				return Detect(data);
			}

			return DecodeWith(data, codePage, false);
		}

		/// <summary>The name shown for a code page.</summary>
		public static String NameOf(Int32 codePage)
		{
			return codePage switch
			{
				65001 => "UTF-8",
				1200 => "UTF-16 LE",
				1201 => "UTF-16 BE",
				12000 => "UTF-32 LE",
				12001 => "UTF-32 BE",
				20127 => "ASCII",
				1250 or 1251 or 1252 => "Windows-" + codePage,
				_ => "CP" + codePage,
			};
		}

		private static DecodedText Detect(Byte[] data)
		{
			// A byte order mark settles it.
			if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
			{
				return DecodeWith(data, 65001, true);
			}

			if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE && data[2] == 0 && data[3] == 0)
			{
				return DecodeWith(data, 12000, true);
			}

			if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF)
			{
				return DecodeWith(data, 12001, true);
			}

			if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
			{
				return DecodeWith(data, 1200, true);
			}

			if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
			{
				return DecodeWith(data, 1201, true);
			}

			// UTF-16 without mark: every other byte of a western text is zero.
			Int32 sample = Math.Min(data.Length, SampleLength) & ~1;
			if (sample >= 4)
			{
				Int32 evenZeros = 0;
				Int32 oddZeros = 0;
				for (Int32 i = 0; i < sample; i += 2)
				{
					evenZeros += data[i] == 0 ? 1 : 0;
					oddZeros += data[i + 1] == 0 ? 1 : 0;
				}

				Int32 pairs = sample / 2;
				if (oddZeros * 10 >= pairs * 3 && evenZeros * 50 <= pairs)
				{
					return DecodeWith(data, 1200, true);
				}

				if (evenZeros * 10 >= pairs * 3 && oddZeros * 50 <= pairs)
				{
					return DecodeWith(data, 1201, true);
				}
			}

			Int32 high = 0;
			Int32 letters = 0;
			Int32 polish = 0;
			foreach (Byte value in data.AsSpan(0, Math.Min(data.Length, SampleLength * 4)))
			{
				if (value >= 0x80)
				{
					high++;

					// ł, ą, ś, ź and Ł, Ą, Ś, Ź, Ż of Windows-1250 are the symbols ³ ¹ œ Ÿ £ ¥ Œ ¯ of Windows-1252, which
					// a western text hardly ever contains. (ż is left out: it is the ¿ of Spanish.)
					if (value is 0xB3 or 0xB9 or 0x9C or 0x9F or 0xA3 or 0xA5 or 0x8C or 0x8F or 0xAF)
					{
						polish++;
					}
				}
				else if ((value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z'))
				{
					letters++;
				}
			}

			if (high == 0)
			{
				return DecodeWith(data, 20127, true);
			}

			// Text that is valid UTF-8 and is not plain ASCII is UTF-8: by chance, text in another encoding is not.
			if (IsValidUtf8(data))
			{
				return DecodeWith(data, 65001, true);
			}

			if (polish >= 3 && polish * 100 >= high * 8)
			{
				return DecodeWith(data, 1250, true);
			}

			// Russian text is made of high bytes, western text of letters with a few accents.
			Boolean cyrillic = high >= 4 && high * 100 > (high + letters) * 35;
			return DecodeWith(data, cyrillic ? 1251 : 1252, true);
		}

		private static Boolean IsValidUtf8(Byte[] data)
		{
			try
			{
				new UTF8Encoding(false, true).GetCharCount(data);
				return true;
			}
			catch (DecoderFallbackException)
			{
				return false;
			}
		}

		private static DecodedText DecodeWith(Byte[] data, Int32 codePage, Boolean detected)
		{
			Encoding encoding = Encoding.GetEncoding(codePage);
			Int32 skip = ByteOrderMarkLength(data, codePage);
			String text = encoding.GetString(data, skip, data.Length - skip);

			return new DecodedText(MakeControlCharactersVisible(text, out Boolean looksBinary), NameOf(codePage), detected, looksBinary);
		}

		private static Int32 ByteOrderMarkLength(Byte[] data, Int32 codePage)
		{
			return codePage switch
			{
				65001 when data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF => 3,
				1200 when data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE => 2,
				1201 when data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF => 2,
				12000 when data.Length >= 4 && data[0] == 0xFF && data[1] == 0xFE && data[2] == 0 && data[3] == 0 => 4,
				12001 when data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0xFE && data[3] == 0xFF => 4,
				_ => 0,
			};
		}

		/// <summary>
		/// Shows the control characters as the symbols of the "control pictures" block (a text box would draw nothing),
		/// and tells whether there are so many of them that this is probably not a text.
		/// </summary>
		private static String MakeControlCharactersVisible(String text, out Boolean looksBinary)
		{
			Int32 controls = 0;
			Boolean found = false;
			foreach (Char character in text)
			{
				if (character < ' ' && character is not ('\t' or '\r' or '\n' or '\f'))
				{
					controls++;
					found = true;
				}
			}

			looksBinary = text.Length > 0 && controls * 10 > text.Length;
			if (!found)
			{
				return text;
			}

			StringBuilder builder = new(text.Length);
			foreach (Char character in text)
			{
				builder.Append(character < ' ' && character is not ('\t' or '\r' or '\n' or '\f') ? (Char)(0x2400 + character) : character);
			}

			return builder.ToString();
		}
	}
}