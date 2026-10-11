namespace DatExplorerIII.Models
{
	using System;

	/// <summary>
	/// A creature (a person, an animal, a robot...): its statistics are those of a character file (see <see cref="GcdCharacter"/>).
	/// </summary>
	public sealed class CritterProto : ProtoFile
	{
		public CritterProto(Int32[] baseStats, Int32[] bonusStats, Int32[] skills)
		{
			BaseStats = baseStats;
			BonusStats = bonusStats;
			Skills = skills;
		}

		public override ProtoType Type => ProtoType.Critter;

		public Int32 ExtendedFlags { get; init; }

		/// <summary>The script of the creature, or -1.</summary>
		public Int32 ScriptId { get; init; }

		/// <summary>The picture of the head shown when it talks, or -1.</summary>
		public Int32 HeadFid { get; init; }

		/// <summary>The number of the behavior of the creature in combat.</summary>
		public Int32 AiPacket { get; init; }

		/// <summary>The team: creatures of a team do not fight each other.</summary>
		public Int32 Team { get; init; }

		/// <summary>The flags of the creature itself (as opposed to those of the object).</summary>
		public Int32 CritterFlags { get; init; }

		public Int32[] BaseStats { get; }

		public Int32[] BonusStats { get; }

		/// <summary>The points of each skill.</summary>
		public Int32[] Skills { get; }

		/// <summary>0 biped, 1 quadruped, 2 robotic.</summary>
		public Int32 BodyType { get; init; }

		/// <summary>The experience it gives when it is killed.</summary>
		public Int32 Experience { get; init; }

		/// <summary>The kind of creature it counts as for the kill counts.</summary>
		public Int32 KillType { get; init; }

		/// <summary>The kind of damage of its attacks.</summary>
		public Int32 DamageType { get; init; }

		/// <summary>The value of a statistic: its base plus its bonus.</summary>
		public Int32 GetStat(CharacterStat stat)
		{
			return BaseStats[(Int32)stat] + BonusStats[(Int32)stat];
		}
	}
}