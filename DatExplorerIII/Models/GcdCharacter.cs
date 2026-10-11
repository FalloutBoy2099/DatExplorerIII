namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A character of Fallout read from a character file (.gcd): the statistics, the skills the player chose to specialize
	/// in and the traits.
	/// </summary>
	public sealed class GcdCharacter
	{
		/// <summary>The number of statistics stored for a character (see <see cref="CharacterStat"/>).</summary>
		public const Int32 StatCount = 35;

		/// <summary>The number of skills.</summary>
		public const Int32 SkillCount = 18;

		/// <summary>The number of skills a character can tag.</summary>
		public const Int32 TaggedSkillCount = 4;

		/// <summary>The number of traits a character can have.</summary>
		public const Int32 TraitCount = 2;

		public GcdCharacter(String name, Int32[] baseStats, Int32[] bonusStats, Int32[] skills, Int32[] taggedSkills, Int32[] traits)
		{
			Name = name;
			BaseStats = baseStats;
			BonusStats = bonusStats;
			Skills = skills;
			TaggedSkills = taggedSkills;
			Traits = traits;
		}

		public String Name { get; }

		/// <summary>Flags of the character prototype.</summary>
		public Int32 Flags { get; init; }

		public Int32[] BaseStats { get; }

		/// <summary>What equipment and effects add to the statistics (nothing in a premade character).</summary>
		public Int32[] BonusStats { get; }

		/// <summary>The points of each skill.</summary>
		public Int32[] Skills { get; }

		/// <summary>The numbers of the tagged skills, -1 for a tag that is not used.</summary>
		public Int32[] TaggedSkills { get; }

		/// <summary>The numbers of the traits, -1 for a trait that is not chosen.</summary>
		public Int32[] Traits { get; }

		public Int32 BodyType { get; init; }

		public Int32 Experience { get; init; }

		public Int32 KillType { get; init; }

		public Int32 DamageType { get; init; }

		/// <summary>The value of a statistic: its base plus its bonus.</summary>
		public Int32 GetStat(CharacterStat stat)
		{
			return BaseStats[(Int32)stat] + BonusStats[(Int32)stat];
		}
	}
}