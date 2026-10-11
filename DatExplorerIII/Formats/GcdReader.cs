namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using System.Text;
	using Models;

	/// <summary>
	/// Reads the character files of Fallout (.gcd), as the ones of the premade characters of the character selection.
	/// All numbers are big endian 32 bit: the flags, 35 statistics, 35 bonuses, 18 skills, four more numbers about the body
	/// (type, experience, kill type, damage type), the name (32 bytes), the four tagged skills and the two traits, then one
	/// more number that is not used.
	/// </summary>
	public static class GcdReader
	{
		/// <summary>The size of a character file.</summary>
		public const Int32 Length = 0x1B0;

		private const Int32 NameLength = 32;

		/// <exception cref="InvalidDataException">The data is shorter than a character file.</exception>
		public static GcdCharacter Read(Byte[] data)
		{
			if (data.Length < Length)
			{
				throw new InvalidDataException("A character file needs 432 bytes.");
			}

			Int32 position = 0;
			Int32 Next()
			{
				Int32 value = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(position));
				position += 4;
				return value;
			}

			Int32[] ReadArray(Int32 count)
			{
				Int32[] values = new Int32[count];
				for (Int32 i = 0; i < count; i++)
				{
					values[i] = Next();
				}

				return values;
			}

			Int32 flags = Next();
			Int32[] baseStats = ReadArray(GcdCharacter.StatCount);
			Int32[] bonusStats = ReadArray(GcdCharacter.StatCount);
			Int32[] skills = ReadArray(GcdCharacter.SkillCount);
			Int32 bodyType = Next();
			Int32 experience = Next();
			Int32 killType = Next();
			Int32 damageType = Next();

			Int32 end = Array.IndexOf(data, (Byte)0, position, NameLength);
			String name = Encoding.Latin1.GetString(data, position, (end < 0 ? position + NameLength : end) - position);
			position += NameLength;

			Int32[] tagged = ReadArray(GcdCharacter.TaggedSkillCount);
			Int32[] traits = ReadArray(GcdCharacter.TraitCount);

			return new GcdCharacter(name, baseStats, bonusStats, skills, tagged, traits)
			{
				Flags = flags,
				BodyType = bodyType,
				Experience = experience,
				KillType = killType,
				DamageType = damageType,
			};
		}
	}
}