namespace DatExplorerIII.Models.Prototypes
{
	using System;

	/// <summary>The numbers of armor.</summary>
	/// <param name="ArmorClass">The armor class it gives.</param>
	/// <param name="DamageResistance">The percentage of each kind of damage it stops (normal, laser, fire, plasma, electrical, EMP, explosive).</param>
	/// <param name="DamageThreshold">The damage of each kind it stops entirely.</param>
	/// <param name="Perk">The perk it gives, or -1.</param>
	/// <param name="MaleFid">The picture of a man wearing it.</param>
	/// <param name="FemaleFid">The picture of a woman wearing it.</param>
	public sealed record ArmorData(Int32 ArmorClass, Int32[] DamageResistance, Int32[] DamageThreshold, Int32 Perk, Int32 MaleFid, Int32 FemaleFid);

	/// <summary>The numbers of a container.</summary>
	/// <param name="MaxSize">How much it holds.</param>
	/// <param name="OpenFlags">How it opens.</param>
	public sealed record ContainerData(Int32 MaxSize, Int32 OpenFlags);

	/// <summary>The numbers of a drug.</summary>
	/// <param name="Stat">The three statistics it changes, -1 for none.</param>
	/// <param name="Amount">The change of each at once.</param>
	/// <param name="Duration1">The time until the second change.</param>
	/// <param name="Amount1">The second change of each.</param>
	/// <param name="Duration2">The time until the third change.</param>
	/// <param name="Amount2">The third change of each.</param>
	/// <param name="AddictionChance">The percentage of chance to become addicted.</param>
	/// <param name="WithdrawalEffect">The effect of the withdrawal.</param>
	/// <param name="WithdrawalOnset">The time until the withdrawal starts.</param>
	public sealed record DrugData(Int32[] Stat, Int32[] Amount, Int32 Duration1, Int32[] Amount1, Int32 Duration2, Int32[] Amount2, Int32 AddictionChance, Int32 WithdrawalEffect, Int32 WithdrawalOnset);

	/// <summary>The numbers of a weapon.</summary>
	/// <param name="AnimationCode">The code of the animation of the one who uses it.</param>
	/// <param name="MinDamage">The least damage of a hit.</param>
	/// <param name="MaxDamage">The most damage of a hit.</param>
	/// <param name="DamageType">The kind of damage (0 normal, 1 laser, 2 fire, 3 plasma, 4 electrical, 5 EMP, 6 explosive).</param>
	/// <param name="MaxRange1">The range of a shot.</param>
	/// <param name="MaxRange2">The range of the second way of using it (a burst).</param>
	/// <param name="ProjectilePid">The projectile it throws or shoots, or -1.</param>
	/// <param name="MinStrength">The strength needed to use it well.</param>
	/// <param name="ActionPointCost1">The action points a shot costs.</param>
	/// <param name="ActionPointCost2">The action points of the second way of using it.</param>
	/// <param name="CriticalFailureType">What happens on a critical failure.</param>
	/// <param name="Perk">The perk it needs or gives, or -1.</param>
	/// <param name="Rounds">The rounds of a burst.</param>
	/// <param name="Caliber">The caliber of the ammunition it takes.</param>
	/// <param name="AmmoPid">The ammunition it takes by default, or -1.</param>
	/// <param name="AmmoCapacity">How many rounds it holds.</param>
	/// <param name="SoundCode">The letter that makes the name of its sounds.</param>
	public sealed record WeaponData(
		Int32 AnimationCode,
		Int32 MinDamage,
		Int32 MaxDamage,
		Int32 DamageType,
		Int32 MaxRange1,
		Int32 MaxRange2,
		Int32 ProjectilePid,
		Int32 MinStrength,
		Int32 ActionPointCost1,
		Int32 ActionPointCost2,
		Int32 CriticalFailureType,
		Int32 Perk,
		Int32 Rounds,
		Int32 Caliber,
		Int32 AmmoPid,
		Int32 AmmoCapacity,
		Char SoundCode);

	/// <summary>The numbers of ammunition.</summary>
	/// <param name="Caliber">The caliber.</param>
	/// <param name="Quantity">The rounds in one.</param>
	/// <param name="ArmorClassModifier">The change of the armor class of the target.</param>
	/// <param name="DamageResistanceModifier">The change of the resistance of the target.</param>
	/// <param name="DamageMultiplier">The damage is multiplied by this.</param>
	/// <param name="DamageDivisor">The damage is divided by this.</param>
	public sealed record AmmoData(Int32 Caliber, Int32 Quantity, Int32 ArmorClassModifier, Int32 DamageResistanceModifier, Int32 DamageMultiplier, Int32 DamageDivisor);

	/// <summary>The numbers of a miscellaneous item.</summary>
	/// <param name="PowerPid">The item that powers it, or -1.</param>
	/// <param name="PowerType">The kind of power it needs.</param>
	/// <param name="Charges">How many times it can be used.</param>
	public sealed record MiscItemData(Int32 PowerPid, Int32 PowerType, Int32 Charges);

	/// <summary>The numbers of a key.</summary>
	/// <param name="KeyCode">The code of the locks it opens.</param>
	public sealed record KeyData(Int32 KeyCode);

	/// <summary>
	/// An item: armor, container, drug, weapon, ammunition, miscellaneous item or key. Only the data of its kind is set.
	/// </summary>
	public sealed class ItemProto : ProtoFile
	{
		public override ProtoType Type => ProtoType.Item;

		public Int32 ExtendedFlags { get; init; }

		/// <summary>The script of the item, or -1.</summary>
		public Int32 ScriptId { get; init; }

		public ItemType ItemType { get; init; }

		public Int32 Material { get; init; }

		public Int32 Size { get; init; }

		public Int32 Weight { get; init; }

		public Int32 Cost { get; init; }

		/// <summary>The picture of the item in the inventory.</summary>
		public Int32 InventoryFid { get; init; }

		/// <summary>The letter that makes the name of the sounds of the item.</summary>
		public Char SoundId { get; init; }

		public ArmorData? Armor { get; init; }

		public ContainerData? Container { get; init; }

		public DrugData? Drug { get; init; }

		public WeaponData? Weapon { get; init; }

		public AmmoData? Ammo { get; init; }

		public MiscItemData? MiscData { get; init; }

		public KeyData? Key { get; init; }
	}
}