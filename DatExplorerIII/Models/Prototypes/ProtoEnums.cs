namespace DatExplorerIII.Models.Prototypes
{
	/// <summary>
	/// The kind of object a prototype file (.pro) describes. It is the high byte of the identifier of the object.
	/// </summary>
	public enum ProtoType
	{
		Item,
		Critter,
		Scenery,
		Wall,
		Tile,
		Misc,
	}

	/// <summary>
	/// What an item is.
	/// </summary>
	public enum ItemType
	{
		Armor,
		Container,
		Drug,
		Weapon,
		Ammo,
		Misc,
		Key,
	}

	/// <summary>
	/// What a piece of scenery is.
	/// </summary>
	public enum SceneryType
	{
		Door,
		Stairs,
		Elevator,
		LadderBottom,
		LadderTop,
		Generic,
	}

	/// <summary>
	/// The kind of object a prototype file (.pro) of Arcanum describes.
	/// </summary>
	public enum ArcanumObjectType
	{
		Wall = 0,
		Portal = 1,
		Container = 2,
		Scenery = 3,
		Projectile = 4,
		Weapon = 5,
		Ammo = 6,
		Armor = 7,
		Gold = 8,
		Food = 9,
		Scroll = 10,
		Key = 11,
		KeyRing = 12,
		Written = 13,
		Generic = 14,
		Pc = 15,
		Npc = 16,
		Trap = 17,
	}

	/// <summary>
	/// What an object is made of (it gives the sound it makes when it is hit).
	/// </summary>
	public enum MaterialType
	{
		Glass,
		Metal,
		Plastic,
		Wood,
		Dirt,
		Stone,
		Cement,
		Leather,
	}

	/// <summary>
	/// The statistics of a character, in the order they are stored in a character file (.gcd).
	/// </summary>
	public enum CharacterStat
	{
		Strength,
		Perception,
		Endurance,
		Charisma,
		Intelligence,
		Agility,
		Luck,
		HitPoints,
		ActionPoints,
		ArmorClass,
		UnarmedDamage,
		MeleeDamage,
		CarryWeight,
		Sequence,
		HealingRate,
		CriticalChance,
		BetterCriticals,
		ThresholdNormal,
		ThresholdLaser,
		ThresholdFire,
		ThresholdPlasma,
		ThresholdElectrical,
		ThresholdEmp,
		ThresholdExplosive,
		ResistanceNormal,
		ResistanceLaser,
		ResistanceFire,
		ResistancePlasma,
		ResistanceElectrical,
		ResistanceEmp,
		ResistanceExplosive,
		RadiationResistance,
		PoisonResistance,
		Age,
		Gender,
	}
}