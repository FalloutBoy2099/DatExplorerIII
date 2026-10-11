namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using Models;

	/// <summary>How the value of a field is stored in a file of an object.</summary>
	internal enum ArcanumWireType
	{
		/// <summary>32 bit integer.</summary>
		Int32,

		/// <summary>One byte that tells if the value is there, then a 64 bit integer.</summary>
		Int64,

		/// <summary>32 bit floating point number.</summary>
		Float,

		/// <summary>One byte that tells if the text is there, then its length (32 bit), then its characters and a zero.</summary>
		String,

		/// <summary>An array of 32 bit integers.</summary>
		Int32Array,

		/// <summary>An array of 32 bit unsigned integers.</summary>
		UInt32Array,

		/// <summary>An array of 64 bit integers.</summary>
		Int64Array,

		/// <summary>An array of identifiers of objects (24 bytes each).</summary>
		HandleArray,

		/// <summary>An array of scripts (12 bytes each).</summary>
		ScriptArray,

		/// <summary>An array of quests (the size of an element is in the array).</summary>
		QuestArray,

		/// <summary>Three bytes: red, green and blue.</summary>
		Rgb24,

		/// <summary>An identifier of an object (24 bytes).</summary>
		ObjectId,
	}

	/// <summary>
	/// The fields of the objects of Arcanum: their names, and how each is stored. Every field has a number (its bit in the bitmap
	/// at the start of the file). The first 36 are common to all objects; the next ones depend on the kind of object (the numbers
	/// from 36 to 63 are not stored in a prototype: they belong to the objects of a map). A prototype holds every field of its kind
	/// of object, whether or not its bit is set in the bitmap.
	/// </summary>
	internal static class ArcanumFields
	{
		private static readonly String[] CommonNames =
		[
			"Current Aid", "Location", "Offset X", "Offset Y", "Shadow", "Overlay Fore", "Overlay Back", "Underlay", "Blit Flags", "Blit Color", "Blit Alpha",
			"Blit Scale", "Light Flags", "Light Aid", "Light Color", "Overlay Light Flags", "Overlay Light Aid", "Overlay Light Color", "Object Flags", "Spell Flags",
			"Blocking Mask", "Name", "Description", "Aid", "Destroyed Aid", "Armor Class", "Hit Points", "Hit Points Adjustment", "Hit Points Damage", "Material",
			"Resistances", "Scripts", "Sound Effect", "Category", "Pad Ias 1", "Pad I64 As 1",
		];

		private static readonly String[] ItemNames =
		[
			"Item Flags", "Item Parent", "Item Weight", "Item Magic Weight Adjustment", "Item Worth", "Item Mana Store", "Item Inventory Aid", "Item Inventory Location",
			"Item Use Aid Fragment", "Item Magic Tech Complexity", "Item Discipline", "Item Description Unknown", "Item Description Effects", "Item Spell 1", "Item Spell 2",
			"Item Spell 3", "Item Spell 4", "Item Spell 5", "Item Spell Mana Store", "Item Ai Action", "Item Pad I1", "Item Pad Ias 1", "Item Pad I64 As 1",
		];

		private static readonly Dictionary<ArcanumObjectType, (Int32 Start, String[] Names)[]> Names = new()
		{
			[ArcanumObjectType.Wall] = [(64, new[] { "Wall Flags", "Wall Pad I1", "Wall Pad I2", "Wall Pad Ias 1", "Wall Pad I64 As 1" })],
			[ArcanumObjectType.Portal] = [(64, new[] { "Portal Flags", "Portal Lock Difficulty", "Portal Key Id", "Portal Notify Npc", "Portal Pad I1", "Portal Pad I2", "Portal Pad Ias 1", "Portal Pad I64 As 1" })],
			[ArcanumObjectType.Container] = [(64, new[] { "Container Flags", "Container Lock Difficulty", "Container Key Id", "Container Inventory Count", "Container Inventory List", "Container Inventory Source", "Container Notify Npc", "Container Pad I1", "Container Pad I2", "Container Pad Ias 1", "Container Pad I64 As 1" })],
			[ArcanumObjectType.Scenery] = [(64, new[] { "Scenery Flags", "Scenery Who Is In Me", "Scenery Respawn Delay", "Scenery Pad I2", "Scenery Pad Ias 1", "Scenery Pad I64 As 1" })],
			[ArcanumObjectType.Projectile] = [(64, new[] { "Projectile Combat Flags", "Projectile Combat Damage Flags", "Projectile Hit Location", "Projectile Parent Weapon", "Projectile Pad I1", "Projectile Pad I2", "Projectile Pad Ias 1", "Projectile Pad I64 As 1" })],
			[ArcanumObjectType.Trap] = [(64, new[] { "Trap Flags", "Trap Difficulty", "Trap Pad I2", "Trap Pad Ias 1", "Trap Pad I64 As 1" })],
			[ArcanumObjectType.Weapon] = [(64, ItemNames), (96, ["Weapon Flags", "Weapon Paper Doll Aid", "Weapon Bonus To Hit", "Weapon Magic Hit Adjustment", "Weapon Damage Lower", "Weapon Damage Upper", "Weapon Magic Damage Adjustment", "Weapon Speed Factor", "Weapon Magic Speed Adjustment", "Weapon Range", "Weapon Magic Range Adjustment", "Weapon Minimum Strength", "Weapon Magic Minimum Strength Adjustment", "Weapon Ammo Type", "Weapon Ammo Consumption", "Weapon Missile Aid", "Weapon Visual Effect Aid", "Weapon Critical Hit Chart", "Weapon Magic Critical Hit Chance", "Weapon Magic Critical Hit Effect", "Weapon Critical Miss Chart", "Weapon Magic Critical Miss Chance", "Weapon Magic Critical Miss Effect", "Weapon Pad I1", "Weapon Pad I2", "Weapon Pad Ias 1", "Weapon Pad I64 As 1"])],
			[ArcanumObjectType.Ammo] = [(64, ItemNames), (96, ["Ammo Flags", "Ammo Quantity", "Ammo Type", "Ammo Pad I1", "Ammo Pad I2", "Ammo Pad Ias 1", "Ammo Pad I64 As 1"])],
			[ArcanumObjectType.Armor] = [(64, ItemNames), (96, ["Armor Flags", "Armor Paper Doll Aid", "Armor Class Adjustment", "Armor Magic Class Adjustment", "Armor Resistance Adjustment", "Armor Magic Resistance Adjustment", "Armor Silent Move Adjustment", "Armor Magic Silent Move Adjustment", "Armor Unarmed Bonus Damage", "Armor Pad I2", "Armor Pad Ias 1", "Armor Pad I64 As 1"])],
			[ArcanumObjectType.Gold] = [(64, ItemNames), (96, ["Gold Flags", "Gold Quantity", "Gold Pad I1", "Gold Pad I2", "Gold Pad Ias 1", "Gold Pad I64 As 1"])],
			[ArcanumObjectType.Food] = [(64, ItemNames), (96, ["Food Flags", "Food Pad I1", "Food Pad I2", "Food Pad Ias 1", "Food Pad I64 As 1"])],
			[ArcanumObjectType.Scroll] = [(64, ItemNames), (96, ["Scroll Flags", "Scroll Pad I1", "Scroll Pad I2", "Scroll Pad Ias 1", "Scroll Pad I64 As 1"])],
			[ArcanumObjectType.Key] = [(64, ItemNames), (96, ["Key Id", "Key Pad I1", "Key Pad I2", "Key Pad Ias 1", "Key Pad I64 As 1"])],
			[ArcanumObjectType.KeyRing] = [(64, ItemNames), (96, ["Key Ring Flags", "Key Ring List", "Key Ring Pad I1", "Key Ring Pad I2", "Key Ring Pad Ias 1", "Key Ring Pad I64 As 1"])],
			[ArcanumObjectType.Written] = [(64, ItemNames), (96, ["Written Flags", "Written Subtype", "Written Text Start Line", "Written Text End Line", "Written Pad I1", "Written Pad I2", "Written Pad Ias 1", "Written Pad I64 As 1"])],
			[ArcanumObjectType.Generic] = [(64, ItemNames), (96, ["Generic Flags", "Generic Usage Bonus", "Generic Usage Count Remaining", "Generic Pad Ias 1", "Generic Pad I64 As 1"])],
			[ArcanumObjectType.Pc] = [(64, CritterNames()), (128, ["Pc Flags", "Pc Fate Flags", "Pc Reputations", "Pc Reputation Times", "Pc Background", "Pc Background Text", "Pc Quests", "Pc Blessings", "Pc Blessing Times", "Pc Curses", "Pc Curse Times", "Pc Party Id", "Pc Rumors", "Pc Pad Ias 2", "Pc Schematics Found", "Pc Logbook Ego", "Pc Fog Mask", "Pc Player Name", "Pc Bank Money", "Pc Global Flags", "Pc Global Variables", "Pc Pad I1", "Pc Pad I2", "Pc Pad Ias 1", "Pc Pad I64 As 1"])],
			[ArcanumObjectType.Npc] = [(64, CritterNames()), (128, ["Npc Flags", "Npc Leader", "Npc Ai Data", "Npc Combat Focus", "Npc Who Hit Me Last", "Npc Experience Worth", "Npc Experience Pool", "Npc Waypoints", "Npc Waypoint Current", "Npc Standpoint Day", "Npc Standpoint Night", "Npc Origin", "Npc Faction", "Npc Retail Price Multiplier", "Npc Substitute Inventory", "Npc Reaction Base", "Npc Social Class", "Npc Reaction Pc", "Npc Reaction Level", "Npc Reaction Time", "Npc Wait", "Npc Generator Data", "Npc Pad I1", "Npc Damage", "Npc Hostile List"])],
		};

		private static String[] CritterNames() =>
		[
			"Critter Flags", "Critter Flags 2", "Critter Base Stats", "Critter Basic Skills", "Critter Tech Skills", "Critter Spell And Tech Disciplines", "Critter Fatigue Points",
			"Critter Fatigue Adjustment", "Critter Fatigue Damage", "Critter Critical Hit Chart", "Critter Effects", "Critter Effect Causes", "Critter Fleeing From", "Critter Portrait",
			"Critter Gold", "Critter Arrows", "Critter Bullets", "Critter Power Cells", "Critter Fuel", "Critter Inventory Count", "Critter Inventory List", "Critter Inventory Source",
			"Critter Description Unknown", "Critter Followers", "Critter Teleport Destination", "Critter Teleport Map", "Critter Death Time", "Critter Auto Level Scheme",
			"Critter Pad I1", "Critter Pad I2", "Critter Pad I3", "Critter Pad Ias 1", "Critter Pad I64 As 1",
		];

		/// <summary>The name of a field of a kind of object.</summary>
		public static String NameOf(ArcanumObjectType type, Int32 bit)
		{
			if (bit < CommonNames.Length)
			{
				return CommonNames[bit];
			}

			if (Names.TryGetValue(type, out (Int32 Start, String[] Names)[]? groups))
			{
				foreach ((Int32 start, String[] names) in groups)
				{
					if (bit >= start && bit < start + names.Length)
					{
						return names[bit - start];
					}
				}
			}

			return "Field " + bit;
		}

		/// <summary>How a field of a kind of object is stored; false when it is not known (the size of the value is then unknown).</summary>
		public static Boolean TryGetWireType(ArcanumObjectType type, Int32 bit, out ArcanumWireType wireType)
		{
			if (Common.TryGetValue(bit, out wireType))
			{
				return true;
			}

			return ByType.TryGetValue(type, out Dictionary<Int32, ArcanumWireType>? specific) && specific.TryGetValue(bit, out wireType);
		}

		/// <summary>The number of bytes of the bitmap that says which fields the file holds.</summary>
		public static Int32 BitmapSize(ArcanumObjectType type)
		{
			return type switch
			{
				ArcanumObjectType.Pc or ArcanumObjectType.Npc => 20,
				>= ArcanumObjectType.Weapon and <= ArcanumObjectType.Generic => 16,
				_ => 12,
			};
		}

		private static readonly Dictionary<Int32, ArcanumWireType> Common = BuildCommon();

		private static readonly Dictionary<ArcanumObjectType, Dictionary<Int32, ArcanumWireType>> ByType = BuildByType();

		private static Dictionary<Int32, ArcanumWireType> BuildCommon()
		{
			Dictionary<Int32, ArcanumWireType> map = new();
			Int32[] int32 = [0, 4, 8, 9, 10, 11, 12, 13, 14, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 32, 33];
			foreach (Int32 bit in int32)
			{
				map[bit] = ArcanumWireType.Int32;
			}

			map[1] = ArcanumWireType.Int64;
			map[2] = ArcanumWireType.Float;
			map[3] = ArcanumWireType.Float;

			foreach (Int32 bit in new[] { 5, 6, 7, 30, 34 })
			{
				map[bit] = ArcanumWireType.Int32Array;
			}

			foreach (Int32 bit in new[] { 15, 16, 17 })
			{
				map[bit] = ArcanumWireType.UInt32Array;
			}

			map[31] = ArcanumWireType.ScriptArray;
			map[35] = ArcanumWireType.Int64Array;
			return map;
		}

		private static Dictionary<ArcanumObjectType, Dictionary<Int32, ArcanumWireType>> BuildByType()
		{
			Dictionary<Int32, ArcanumWireType> itemBits = new() { [64] = ArcanumWireType.Int32, [65] = ArcanumWireType.Int64, [85] = ArcanumWireType.Int32Array, [86] = ArcanumWireType.Int64Array };
			Range(itemBits, 66, 84, ArcanumWireType.Int32);

			Dictionary<Int32, ArcanumWireType> critterBits = new();
			Set(critterBits, ArcanumWireType.Int32, 64, 65, 70, 71, 72, 73, 77, 83, 85, 86, 89, 91, 92, 93, 94);
			Range(critterBits, 66, 69, ArcanumWireType.Int32Array);
			Set(critterBits, ArcanumWireType.Int32Array, 74, 75, 95);
			critterBits[76] = ArcanumWireType.ObjectId;
			Range(critterBits, 78, 82, ArcanumWireType.ObjectId);
			critterBits[84] = ArcanumWireType.HandleArray;
			critterBits[87] = ArcanumWireType.HandleArray;
			critterBits[88] = ArcanumWireType.Int64;
			critterBits[90] = ArcanumWireType.Int64;
			critterBits[96] = ArcanumWireType.Int64Array;

			Dictionary<Int32, ArcanumWireType> pc = new();

			// 130 is a 32 bit number in the files read (the reputations are in 131); 141 (pad Ias 2) and 151 (pad Ias 1) are arrays.
			Int32[] pcTypes = [0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 2, 0, 1, 1, 0, 0, 1, 3];
			for (Int32 i = 0; i < pcTypes.Length; i++)
			{
				pc[128 + i] = pcTypes[i] switch
				{
					0 => ArcanumWireType.Int32,
					1 => ArcanumWireType.Int32Array,
					2 => ArcanumWireType.String,
					_ => ArcanumWireType.Int64Array,
				};
			}

			Dictionary<Int32, ArcanumWireType> npc = new()
			{
				[128] = ArcanumWireType.Int32,
				[129] = ArcanumWireType.ObjectId,
				[130] = ArcanumWireType.Int32,
				[131] = ArcanumWireType.ObjectId,
				[132] = ArcanumWireType.ObjectId,
				[133] = ArcanumWireType.Int32,
				[134] = ArcanumWireType.Int32,
				[135] = ArcanumWireType.Int64Array,
				[136] = ArcanumWireType.Int32,
				[137] = ArcanumWireType.Int64,
				[138] = ArcanumWireType.Int64,
				[139] = ArcanumWireType.Int32,
				[140] = ArcanumWireType.Int32,
				[141] = ArcanumWireType.Int32,
				[142] = ArcanumWireType.ObjectId,
				[143] = ArcanumWireType.Int32,
				[144] = ArcanumWireType.Int32,
				[145] = ArcanumWireType.Int32Array,
				[146] = ArcanumWireType.Int32Array,
				[147] = ArcanumWireType.Int32Array,
				[148] = ArcanumWireType.Int32,
				[149] = ArcanumWireType.Int32,
				[150] = ArcanumWireType.Int32,
				[151] = ArcanumWireType.Int32Array,
				[152] = ArcanumWireType.Int32Array,
			};

			return new Dictionary<ArcanumObjectType, Dictionary<Int32, ArcanumWireType>>
			{
				[ArcanumObjectType.Wall] = Plain(Layout((64, 66), 67, 68, (69, 93), 94, 95)),
				[ArcanumObjectType.Portal] = Plain(Layout((64, 69), 70, 71, (72, 93), 94, 95)),
				[ArcanumObjectType.Container] = ContainerBits(),
				[ArcanumObjectType.Scenery] = Plain(Layout((64, 67), 68, 69)),
				[ArcanumObjectType.Trap] = Plain(Layout((64, 66), 67, 68)),
				[ArcanumObjectType.Projectile] = ProjectileBits(),
				[ArcanumObjectType.Weapon] = ItemDerived(itemBits, 120, 121, 122),
				[ArcanumObjectType.Ammo] = ItemDerived(itemBits, 100, 101, 102),
				[ArcanumObjectType.Armor] = ItemDerived(itemBits, 105, 106, 107),
				[ArcanumObjectType.Gold] = ItemDerived(itemBits, 99, 100, 101),
				[ArcanumObjectType.Food] = ItemDerived(itemBits, 98, 99, 100),
				[ArcanumObjectType.Scroll] = ItemDerived(itemBits, 98, 99, 100),
				[ArcanumObjectType.Key] = ItemDerived(itemBits, 98, 99, 100),
				[ArcanumObjectType.KeyRing] = ItemDerived(itemBits, 99, 100, 101),
				[ArcanumObjectType.Written] = ItemDerived(itemBits, 101, 102, 103),
				[ArcanumObjectType.Generic] = ItemDerived(itemBits, 98, 99, 100),
				[ArcanumObjectType.Pc] = Merge(critterBits, pc),
				[ArcanumObjectType.Npc] = Merge(critterBits, npc),
			};
		}

		/// <summary>A layout: integer fields from a bit to a bit, then an array of integers, then an array of 64 bit integers.</summary>
		private static (Int32 Start, Int32 End, ArcanumWireType Type)[] Layout((Int32 Start, Int32 End) integers, Int32 intArray, Int32 longArray, (Int32 Start, Int32 End)? moreIntegers = null, Int32 moreIntArray = -1, Int32 moreLongArray = -1)
		{
			List<(Int32, Int32, ArcanumWireType)> parts = new()
			{
				(integers.Start, integers.End, ArcanumWireType.Int32),
				(intArray, intArray, ArcanumWireType.Int32Array),
				(longArray, longArray, ArcanumWireType.Int64Array),
			};

			if (moreIntegers is { } more)
			{
				parts.Add((more.Start, more.End, ArcanumWireType.Int32));
				parts.Add((moreIntArray, moreIntArray, ArcanumWireType.Int32Array));
				parts.Add((moreLongArray, moreLongArray, ArcanumWireType.Int64Array));
			}

			return parts.ToArray();
		}

		private static Dictionary<Int32, ArcanumWireType> Plain((Int32 Start, Int32 End, ArcanumWireType Type)[] layout)
		{
			Dictionary<Int32, ArcanumWireType> map = new();
			foreach ((Int32 start, Int32 end, ArcanumWireType type) in layout)
			{
				Range(map, start, end, type);
			}

			return map;
		}

		private static Dictionary<Int32, ArcanumWireType> ContainerBits()
		{
			Dictionary<Int32, ArcanumWireType> map = new();
			Range(map, 64, 67, ArcanumWireType.Int32);
			map[68] = ArcanumWireType.HandleArray;
			Range(map, 69, 72, ArcanumWireType.Int32);
			map[73] = ArcanumWireType.Int32Array;
			map[74] = ArcanumWireType.Int64Array;
			return map;
		}

		private static Dictionary<Int32, ArcanumWireType> ProjectileBits()
		{
			Dictionary<Int32, ArcanumWireType> map = new();
			Range(map, 64, 66, ArcanumWireType.Int32);
			map[67] = ArcanumWireType.Int64;
			map[68] = ArcanumWireType.Int32;
			map[69] = ArcanumWireType.Int32;
			map[70] = ArcanumWireType.Int32Array;
			map[71] = ArcanumWireType.Int64Array;
			return map;
		}

		private static Dictionary<Int32, ArcanumWireType> ItemDerived(Dictionary<Int32, ArcanumWireType> itemBits, Int32 lastInteger, Int32 intArray, Int32 longArray)
		{
			Dictionary<Int32, ArcanumWireType> map = new(itemBits);
			Range(map, 96, lastInteger, ArcanumWireType.Int32);
			map[intArray] = ArcanumWireType.Int32Array;
			map[longArray] = ArcanumWireType.Int64Array;
			return map;
		}

		private static Dictionary<Int32, ArcanumWireType> Merge(Dictionary<Int32, ArcanumWireType> baseBits, Dictionary<Int32, ArcanumWireType> specific)
		{
			Dictionary<Int32, ArcanumWireType> map = new(baseBits);
			foreach (KeyValuePair<Int32, ArcanumWireType> pair in specific)
			{
				map[pair.Key] = pair.Value;
			}

			return map;
		}

		private static void Set(Dictionary<Int32, ArcanumWireType> map, ArcanumWireType type, params Int32[] bits)
		{
			foreach (Int32 bit in bits)
			{
				map[bit] = type;
			}
		}

		private static void Range(Dictionary<Int32, ArcanumWireType> map, Int32 start, Int32 end, ArcanumWireType type)
		{
			for (Int32 bit = start; bit <= end; bit++)
			{
				map[bit] = type;
			}
		}
	}
}