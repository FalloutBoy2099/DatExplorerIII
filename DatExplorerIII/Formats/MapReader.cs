namespace DatExplorerIII.Formats
{
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Models;

	/// <summary>
	/// Reads the maps of Fallout 1 and 2 (.map). All numbers are big endian 32 bit words, except the tiles (16 bit).
	/// </summary>
	/// <remarks>
	/// <para>The file is: a header of 236 bytes; the global then the local variables of the map; for each elevation the map has
	/// (flags bits 1 to 3 tell which are missing) 10 000 squares of a roof and a floor tile; the scripts, in five lists (system,
	/// spatial, timer, item, critter), each stored in groups of 16 slots; then the objects, in three lists of one per elevation.</para>
	/// <para>An object is 22 words, then words that depend on what it is (that is told by its prototype, which is why the
	/// prototypes of the game are needed: see <see cref="MapResources"/>), then the objects it carries.</para>
	/// <para>What cannot be read stops the reading there: the map returned holds what was read before, with a note.</para>
	/// </remarks>
	public static class MapReader
	{
		private const Int32 HeaderSize = 236;
		private const Int32 NameLength = 16;
		private const Int32 TilesPerElevation = 10000;
		private const Int32 ElevationCount = 3;
		private const Int32 EmptyTile = 1;
		private const Int32 CommonObjectWords = 22;
		private const Int32 ScriptTypes = 5;
		private const Int32 ScriptSlotsPerGroup = 16;
		private const Int32 ScriptTrailerWords = 14;
		private const Int32 MaxInventoryDepth = 32;
		private const Int32 FalloutOneVersion = 19;
		private const Int32 FirstExitGrid = 16;
		private const Int32 LastExitGrid = 23;

		private const Int32 ItemAmmo = (Int32)ItemType.Ammo;
		private const Int32 ItemMisc = (Int32)ItemType.Misc;
		private const Int32 ItemKey = (Int32)ItemType.Key;
		private const Int32 ItemWeapon = (Int32)ItemType.Weapon;

		/// <summary>True when the data starts like a map: a version, 19 (Fallout 1) or 20 (Fallout 2), and a whole header.</summary>
		public static Boolean IsMap(ReadOnlySpan<Byte> data)
		{
			return data.Length >= HeaderSize && BinaryPrimitives.ReadInt32BigEndian(data) is 19 or 20;
		}

		/// <exception cref="InvalidDataException">The data is not a map (no header, unknown version).</exception>
		public static MapFile Read(Byte[] data, MapResources? resources = null)
		{
			if (!IsMap(data))
			{
				throw new InvalidDataException("This is not a map of Fallout.");
			}

			Cursor cursor = new(data);
			Int32 version = cursor.ReadInt32();
			String name = ReadName(data.AsSpan(4, NameLength));
			cursor.Skip(NameLength);

			Int32 playerTile = cursor.ReadInt32();
			Int32 playerElevation = cursor.ReadInt32();
			Int32 playerOrientation = cursor.ReadInt32();
			Int32 localVariables = cursor.ReadInt32();
			Int32 scriptId = cursor.ReadInt32();
			Int32 flags = cursor.ReadInt32();
			Int32 darkness = cursor.ReadInt32();
			Int32 globalVariables = cursor.ReadInt32();
			Int32 mapNumber = cursor.ReadInt32();
			cursor.Skip(HeaderSize - cursor.Position);

			MapHeader header = new(
				version,
				name,
				playerTile,
				playerElevation,
				playerOrientation,
				scriptId,
				flags,
				darkness,
				mapNumber,
				resources?.MapName(mapNumber),
				scriptId > 0 ? resources?.ScriptName(scriptId - 1) : null);

			List<MapElevation> elevations = new();
			List<MapScriptEntry> scripts = new();
			List<MapTransition> transitions = new();
			Int32 totalObjects = 0;
			String? note = null;

			try
			{
				if (globalVariables < 0 || localVariables < 0 || (Int64)(globalVariables + localVariables) * 4 > cursor.Remaining)
				{
					throw new InvalidDataException("The number of variables is not valid.");
				}

				cursor.Skip(((Int64)globalVariables + localVariables) * 4);

				ReadTiles(cursor, flags, elevations);
				ReadScripts(cursor, resources, scripts);
				ReadObjects(cursor, version, resources, elevations, transitions, out totalObjects);

				if (cursor.Remaining > 0)
				{
					throw new InvalidDataException($"{cursor.Remaining} bytes follow the last object.");
				}
			}
			catch (InvalidDataException exception)
			{
				note = exception.Message;
			}

			elevations.Sort((a, b) => a.Index.CompareTo(b.Index));
			return new MapFile(header, Math.Max(globalVariables, 0), Math.Max(localVariables, 0), elevations, scripts, transitions, totalObjects, note);
		}

		private static String ReadName(ReadOnlySpan<Byte> bytes)
		{
			Int32 end = bytes.IndexOf((Byte)0);
			return Encoding.Latin1.GetString(end < 0 ? bytes : bytes[..end]);
		}

		private static Boolean HasElevation(Int32 flags, Int32 elevation) => (flags & (0x2 << elevation)) == 0;

		private static void ReadTiles(Cursor cursor, Int32 flags, List<MapElevation> elevations)
		{
			for (Int32 elevation = 0; elevation < ElevationCount; elevation++)
			{
				if (!HasElevation(flags, elevation))
				{
					continue;
				}

				ReadOnlySpan<Byte> tiles = cursor.Bytes(TilesPerElevation * 4);
				Int32 roofs = 0;
				Int32 floors = 0;
				for (Int32 index = 0; index < TilesPerElevation; index++)
				{
					if (BinaryPrimitives.ReadUInt16BigEndian(tiles[(index * 4)..]) != EmptyTile)
					{
						roofs++;
					}

					if (BinaryPrimitives.ReadUInt16BigEndian(tiles[(index * 4 + 2)..]) != EmptyTile)
					{
						floors++;
					}
				}

				elevations.Add(new MapElevation(elevation, floors, roofs, null, 0));
			}
		}

		/// <summary>
		/// The five lists of scripts. Each is a count, then the slots in groups of 16 (the last group is padded with leftovers),
		/// each group followed by the number of slots used in it and a word that is not used.
		/// </summary>
		private static void ReadScripts(Cursor cursor, MapResources? resources, List<MapScriptEntry> scripts)
		{
			for (Int32 type = 0; type < ScriptTypes; type++)
			{
				Int64 count = cursor.ReadUInt32();
				if (count == 0)
				{
					continue;
				}

				Int64 slots = (count + ScriptSlotsPerGroup - 1) / ScriptSlotsPerGroup * ScriptSlotsPerGroup;
				if (slots * 4 * (2 + ScriptTrailerWords) > cursor.Remaining)
				{
					throw new InvalidDataException("The number of scripts is not valid.");
				}

				Int64 counted = 0;
				for (Int64 slot = 0; slot < slots; slot++)
				{
					UInt32 pid = cursor.ReadUInt32();
					cursor.ReadUInt32();

					Int32 timer = 0;
					Int32 radius = 0;
					switch (pid >> 24)
					{
						case 1:
							timer = cursor.ReadInt32();
							radius = cursor.ReadInt32();
							break;

						case 2:
							cursor.ReadUInt32();
							break;
					}

					// flags, then the place of the program in the list of scripts, then the rest.
					cursor.ReadUInt32();
					Int32 scriptId = cursor.ReadInt32();
					cursor.Skip((ScriptTrailerWords - 2) * 4);

					if (slot < count)
					{
						Boolean spatial = type == 1;
						scripts.Add(new MapScriptEntry(
							type,
							scriptId,
							spatial ? timer & 0x03FFFFFF : -1,
							spatial ? (Int32)((UInt32)timer >> 29) : -1,
							spatial ? radius : 0,
							resources?.ScriptName(scriptId)));
					}

					if (slot % ScriptSlotsPerGroup == ScriptSlotsPerGroup - 1)
					{
						counted += cursor.ReadUInt32();
						cursor.ReadUInt32();
					}
				}

				if (counted != count)
				{
					throw new InvalidDataException("The scripts of the map are not stored as expected.");
				}
			}
		}

		/// <summary>
		/// The objects: the total, then for each of the three elevations (whether the map has it or not) a count and the objects.
		/// </summary>
		private static void ReadObjects(Cursor cursor, Int32 version, MapResources? resources, List<MapElevation> elevations, List<MapTransition> transitions, out Int32 total)
		{
			total = cursor.ReadInt32();

			for (Int32 elevation = 0; elevation < ElevationCount; elevation++)
			{
				Int64 count = cursor.ReadUInt32();
				if (count * CommonObjectWords * 4 > cursor.Remaining)
				{
					throw new InvalidDataException("The number of objects is not valid.");
				}

				Int32[] byType = new Int32[6];
				ObjectCounter counter = new();
				for (Int64 index = 0; index < count; index++)
				{
					byType[ReadObject(cursor, version, resources, 0, elevation, counter, transitions)]++;
				}

				// Fold the counts into the elevation, when the map has tiles for it.
				Int32 position = elevations.FindIndex(existing => existing.Index == elevation);
				if (position >= 0)
				{
					elevations[position] = elevations[position] with { ObjectsByType = byType, CarriedItems = counter.Carried };
				}
				else if (count > 0)
				{
					elevations.Add(new MapElevation(elevation, 0, 0, byType, counter.Carried));
				}
			}
		}

		/// <summary>Counts the objects that are carried by others.</summary>
		private sealed class ObjectCounter
		{
			public Int32 Carried { get; set; }

			/// <summary>The object read before, for the message when the next one makes no sense.</summary>
			public String Previous { get; set; } = "none";
		}

		/// <summary>Reads an object and what it carries; returns what the object is (0 item to 5 other).</summary>
		private static Int32 ReadObject(Cursor cursor, Int32 version, MapResources? resources, Int32 depth, Int32 elevation, ObjectCounter counter, List<MapTransition> transitions)
		{
			if (depth > MaxInventoryDepth)
			{
				throw new InvalidDataException("The objects carry each other too deep.");
			}

			cursor.Need(CommonObjectWords * 4);
			Int32 start = cursor.Position;
			Int32 position = cursor.Peek(1);
			UInt32 pid = (UInt32)cursor.Peek(11);
			Int32 carried = cursor.Peek(18);
			cursor.Skip(CommonObjectWords * 4);

			Int32 type = (Int32)(pid >> 24);
			Int32 index = (Int32)(pid & 0x00FFFFFF);
			if (type > 5)
			{
				throw new InvalidDataException($"An object has an unknown type ({pid:X8} at byte {start}; the object before was {counter.Previous}).");
			}

			counter.Previous = $"{pid:X8} at byte {start}";

			switch (type)
			{
				case 0:
					Int32 itemType = resources?.SubtypeOf(pid) ?? throw new InvalidDataException($"The prototype of an item ({pid:X8}) was not found: the objects cannot be read.");
					switch (itemType)
					{
						case ItemAmmo:
						case ItemMisc:
						case ItemKey:
							cursor.Skip(4);
							break;

						case ItemWeapon:
							cursor.Skip(8);
							break;
					}

					break;

				case 1:
					cursor.Skip(10 * 4);
					break;

				case 2:
					Int32 sceneryType = resources?.SubtypeOf(pid) ?? throw new InvalidDataException($"The prototype of a piece of scenery ({pid:X8}) was not found: the objects cannot be read.");
					ReadScenery(cursor, version, sceneryType, elevation, position, transitions);
					break;

				case 5 when index is >= FirstExitGrid and <= LastExitGrid:
					Int32 map = cursor.ReadInt32();
					Int32 tile = cursor.ReadInt32();
					Int32 destinationElevation = cursor.ReadInt32();
					cursor.ReadInt32();
					transitions.Add(new MapTransition(MapTransitionKind.ExitGrid, elevation, position, map, tile, destinationElevation, resources?.MapName(map)));
					break;
			}

			if (carried < 0 || (Int64)carried * (4 + CommonObjectWords * 4) > cursor.Remaining)
			{
				throw new InvalidDataException("The number of carried objects is not valid.");
			}

			for (Int32 item = 0; item < carried; item++)
			{
				cursor.ReadUInt32();
				ReadObject(cursor, version, resources, depth + 1, elevation, counter, transitions);
				counter.Carried++;
			}

			return type;
		}

		/// <summary>The words after the common ones for a piece of scenery; stairs and ladders lead to another map.</summary>
		private static void ReadScenery(Cursor cursor, Int32 version, Int32 sceneryType, Int32 elevation, Int32 position, List<MapTransition> transitions)
		{
			switch ((SceneryType)sceneryType)
			{
				case SceneryType.Door:
					cursor.Skip(4);
					break;

				case SceneryType.Stairs:
					AddBuiltTransition(cursor.ReadUInt32(), cursor.ReadInt32(), MapTransitionKind.Stairs);
					break;

				case SceneryType.LadderBottom:
				case SceneryType.LadderTop:
					if (version == FalloutOneVersion)
					{
						// A ladder of Fallout 1 only holds where it leads on the same map.
						UInt32 destination = cursor.ReadUInt32();
						if (destination is not (0 or UInt32.MaxValue))
						{
							AddBuiltTransition(destination, -1, MapTransitionKind.Ladder);
						}

						break;
					}

					Int32 map = cursor.ReadInt32();
					AddBuiltTransition(cursor.ReadUInt32(), map, MapTransitionKind.Ladder);
					break;

				case SceneryType.Elevator:
					cursor.Skip(8);
					break;

				case SceneryType.Generic:
					break;

				default:
					throw new InvalidDataException("A piece of scenery has an unknown type.");
			}

			void AddBuiltTransition(UInt32 builtTile, Int32 map, MapTransitionKind kind)
			{
				transitions.Add(new MapTransition(kind, elevation, position, map, (Int32)(builtTile & 0x03FFFFFF), (Int32)(builtTile >> 29), null));
			}
		}

		/// <summary>Reads big endian words, and says "invalid data" instead of running past the end.</summary>
		private sealed class Cursor
		{
			private readonly Byte[] _data;

			public Cursor(Byte[] data)
			{
				_data = data;
			}

			public Int32 Position { get; private set; }

			public Int64 Remaining => _data.Length - Position;

			public void Need(Int64 count)
			{
				if (count < 0 || count > Remaining)
				{
					throw new InvalidDataException("The file ends too early.");
				}
			}

			public void Skip(Int64 count)
			{
				Need(count);
				Position += (Int32)count;
			}

			public ReadOnlySpan<Byte> Bytes(Int32 count)
			{
				Need(count);
				ReadOnlySpan<Byte> bytes = _data.AsSpan(Position, count);
				Position += count;
				return bytes;
			}

			public Int32 ReadInt32()
			{
				Need(4);
				Int32 value = BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(Position));
				Position += 4;
				return value;
			}

			public UInt32 ReadUInt32()
			{
				Need(4);
				UInt32 value = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(Position));
				Position += 4;
				return value;
			}

			/// <summary>The word at a distance (in words) from the position, without moving.</summary>
			public Int32 Peek(Int32 word)
			{
				Need((word + 1) * 4L);
				return BinaryPrimitives.ReadInt32BigEndian(_data.AsSpan(Position + word * 4));
			}
		}
	}
}