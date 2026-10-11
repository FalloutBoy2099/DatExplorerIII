namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;
	using System.IO;
	using Models;

	/// <summary>
	/// Reads the prototype files (.pro) of Fallout: the description of an object of the game (an item, a creature, a piece
	/// of scenery, a wall, a tile or a miscellaneous object). All numbers are big endian 32 bit, except a few letters.
	/// </summary>
	/// <remarks>
	/// Every file starts with the identifier of the object, whose high byte is its kind, the number of its name, the
	/// identifier of its picture, its light and its flags. The rest depends on the kind: an item has its own kind (armor,
	/// container, drug, weapon, ammunition, miscellaneous item, key) with data for each, a creature has the statistics of a
	/// character file, scenery has a kind of its own (door, stairs...), and walls, tiles and miscellaneous objects have little.
	/// </remarks>
	public static class ProReader
	{
		private const Int32 ResistanceKinds = 7;

		/// <summary>
		/// Reads a prototype.
		/// </summary>
		/// <exception cref="InvalidDataException">The data is too short for its kind, or its kind is unknown.</exception>
		public static ProtoFile Read(Byte[] data)
		{
			Cursor cursor = new(data);
			if (data.Length < 24)
			{
				throw new InvalidDataException("A prototype needs at least 24 bytes.");
			}

			Int32 pid = BinaryPrimitives.ReadInt32BigEndian(data);
			ProtoType type = (ProtoType)(pid >>> 24);
			if (!Enum.IsDefined(type))
			{
				throw new InvalidDataException("The kind of object is unknown.");
			}

			return type switch
			{
				ProtoType.Item => ReadItem(cursor),
				ProtoType.Critter => ReadCritter(cursor),
				ProtoType.Scenery => ReadScenery(cursor),
				ProtoType.Wall => ReadWall(cursor),
				ProtoType.Tile => ReadTile(cursor),
				_ => ReadMisc(cursor),
			};
		}

		private static ItemProto ReadItem(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			Int32 extended = c.ReadInt();
			Int32 script = c.ReadInt();
			Int32 kind = c.ReadInt();
			if (kind < 0 || kind > (Int32)ItemType.Key)
			{
				throw new InvalidDataException("The kind of item is unknown.");
			}

			Int32 material = c.ReadInt();
			Int32 size = c.ReadInt();
			Int32 weight = c.ReadInt();
			Int32 cost = c.ReadInt();
			Int32 inventory = c.ReadInt();
			Char sound = (Char)c.ReadByte();
			ItemType itemType = (ItemType)kind;

			return new ItemProto
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				ExtendedFlags = extended,
				ScriptId = script,
				ItemType = itemType,
				Material = material,
				Size = size,
				Weight = weight,
				Cost = cost,
				InventoryFid = inventory,
				SoundId = sound,
				Armor = itemType == ItemType.Armor ? new ArmorData(c.ReadInt(), c.ReadInts(ResistanceKinds), c.ReadInts(ResistanceKinds), c.ReadInt(), c.ReadInt(), c.ReadInt()) : null,
				Container = itemType == ItemType.Container ? new ContainerData(c.ReadInt(), c.ReadInt()) : null,
				Drug = itemType == ItemType.Drug ? ReadDrug(c) : null,
				Weapon = itemType == ItemType.Weapon ? ReadWeapon(c) : null,
				Ammo = itemType == ItemType.Ammo ? new AmmoData(c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt()) : null,
				MiscData = itemType == ItemType.Misc ? new MiscItemData(c.ReadInt(), c.ReadInt(), c.ReadInt()) : null,
				Key = itemType == ItemType.Key ? new KeyData(c.ReadInt()) : null,
			};
		}

		private static DrugData ReadDrug(Cursor c)
		{
			Int32[] stat = c.ReadInts(3);
			Int32[] amount = c.ReadInts(3);
			Int32 duration1 = c.ReadInt();
			Int32[] amount1 = c.ReadInts(3);
			Int32 duration2 = c.ReadInt();
			Int32[] amount2 = c.ReadInts(3);
			return new DrugData(stat, amount, duration1, amount1, duration2, amount2, c.ReadInt(), c.ReadInt(), c.ReadInt());
		}

		private static WeaponData ReadWeapon(Cursor c)
		{
			Int32 animation = c.ReadInt();
			Int32 minDamage = c.ReadInt();
			Int32 maxDamage = c.ReadInt();
			Int32 damageType = c.ReadInt();
			Int32 range1 = c.ReadInt();
			Int32 range2 = c.ReadInt();
			Int32 projectile = c.ReadInt();
			Int32 strength = c.ReadInt();
			Int32 points1 = c.ReadInt();
			Int32 points2 = c.ReadInt();
			Int32 failure = c.ReadInt();
			Int32 perk = c.ReadInt();
			Int32 rounds = c.ReadInt();
			Int32 caliber = c.ReadInt();
			Int32 ammo = c.ReadInt();
			Int32 capacity = c.ReadInt();
			return new WeaponData(animation, minDamage, maxDamage, damageType, range1, range2, projectile, strength, points1, points2, failure, perk, rounds, caliber, ammo, capacity, (Char)c.ReadByte());
		}

		private static CritterProto ReadCritter(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			Int32 extended = c.ReadInt();
			Int32 script = c.ReadInt();
			Int32 head = c.ReadInt();
			Int32 ai = c.ReadInt();
			Int32 team = c.ReadInt();
			Int32 critterFlags = c.ReadInt();
			Int32[] baseStats = c.ReadInts(GcdCharacter.StatCount);
			Int32[] bonusStats = c.ReadInts(GcdCharacter.StatCount);
			Int32[] skills = c.ReadInts(GcdCharacter.SkillCount);
			Int32 body = c.ReadInt();
			Int32 experience = c.ReadInt();
			Int32 killType = c.ReadInt();

			// A few files stop before the last number (the kind of damage).
			Int32 damageType = c.Remaining >= 4 ? c.ReadInt() : 0;

			return new CritterProto(baseStats, bonusStats, skills)
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				ExtendedFlags = extended,
				ScriptId = script,
				HeadFid = head,
				AiPacket = ai,
				Team = team,
				CritterFlags = critterFlags,
				BodyType = body,
				Experience = experience,
				KillType = killType,
				DamageType = damageType,
			};
		}

		private static SceneryProto ReadScenery(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			Int32 extended = c.ReadInt();
			Int32 script = c.ReadInt();
			Int32 kind = c.ReadInt();
			if (kind < 0 || kind > (Int32)SceneryType.Generic)
			{
				throw new InvalidDataException("The kind of scenery is unknown.");
			}

			Int32 material = c.ReadInt();
			Char sound = (Char)c.ReadByte();
			SceneryType sceneryType = (SceneryType)kind;

			return new SceneryProto
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				ExtendedFlags = extended,
				ScriptId = script,
				SceneryType = sceneryType,
				Material = material,
				SoundId = sound,
				Door = sceneryType == SceneryType.Door ? new DoorData(c.ReadInt(), c.ReadInt()) : null,
				Stairs = sceneryType == SceneryType.Stairs ? new StairsData(c.ReadInt(), c.ReadInt()) : null,
				Elevator = sceneryType == SceneryType.Elevator ? new ElevatorData(c.ReadInt(), c.ReadInt()) : null,
				LadderDestination = sceneryType is SceneryType.LadderBottom or SceneryType.LadderTop ? c.ReadInt() : null,
			};
		}

		private static WallProto ReadWall(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			return new WallProto
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				ExtendedFlags = c.ReadInt(),
				ScriptId = c.ReadInt(),
				Material = c.ReadInt(),
			};
		}

		private static TileProto ReadTile(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			return new TileProto
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				Material = c.ReadInt(),
			};
		}

		private static MiscProto ReadMisc(Cursor c)
		{
			(Int32 pid, Int32 message, Int32 fid, Int32 distance, Int32 intensity, Int32 flags) = ReadCommon(c);
			return new MiscProto
			{
				Pid = pid,
				MessageId = message,
				Fid = fid,
				LightDistance = distance,
				LightIntensity = intensity,
				Flags = flags,
				ExtendedFlags = c.ReadInt(),
			};
		}

		/// <summary>The numbers every kind of object starts with.</summary>
		private static (Int32 Pid, Int32 Message, Int32 Fid, Int32 Distance, Int32 Intensity, Int32 Flags) ReadCommon(Cursor c)
		{
			return (c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt(), c.ReadInt());
		}

		/// <summary>Reads the numbers of a file one after the other, refusing to go past its end.</summary>
		private sealed class Cursor
		{
			private readonly Byte[] _data;
			private Int32 _position;

			public Cursor(Byte[] data)
			{
				_data = data;
			}

			public Int32 Remaining => _data.Length - _position;

			public Int32 ReadInt()
			{
				if (Remaining < 4)
				{
					throw new InvalidDataException("The prototype is shorter than its kind of object needs.");
				}

				Int32 value = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(_position));
				_position += 4;
				return value;
			}

			public Int32[] ReadInts(Int32 count)
			{
				Int32[] values = new Int32[count];
				for (Int32 i = 0; i < count; i++)
				{
					values[i] = ReadInt();
				}

				return values;
			}

			public Byte ReadByte()
			{
				if (Remaining < 1)
				{
					throw new InvalidDataException("The prototype is shorter than its kind of object needs.");
				}

				return _data[_position++];
			}
		}
	}
}