namespace DatExplorerIII.Formats
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Models;

	/// <summary>
	/// The files of the game a map needs to be read: a map does not say what its items and its scenery are, the prototype of each
	/// object does. It also gives the names of the scripts and of the maps.
	/// </summary>
	public sealed class MapResources
	{
		private readonly Func<String, Byte[]?> _read;
		private readonly Dictionary<String, IReadOnlyList<String>> _lists = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<Int32, Int32?> _subtypes = new();
		private IReadOnlyDictionary<Int32, String>? _maps;

		/// <param name="read">Gives the content of a file of the game from its path ("proto\items\items.lst"), or null when it is not there.</param>
		public MapResources(Func<String, Byte[]?> read)
		{
			_read = read;
		}

		/// <summary>
		/// What an item (<see cref="ItemType"/>) or a piece of scenery (<see cref="SceneryType"/>) is, from the identifier of its
		/// prototype. Null when the prototype cannot be found.
		/// </summary>
		public Int32? SubtypeOf(UInt32 pid)
		{
			Int32 key = (Int32)pid;
			if (_subtypes.TryGetValue(key, out Int32? known))
			{
				return known;
			}

			Int32? subtype = FindSubtype(pid);
			_subtypes[key] = subtype;
			return subtype;
		}

		/// <summary>The program of a script (a .int file), from its place in the list of scripts.</summary>
		public String? ScriptName(Int32 index)
		{
			IReadOnlyList<String> list = ReadList(@"scripts\scripts.lst");
			return index >= 0 && index < list.Count ? list[index] : null;
		}

		/// <summary>The name of a map, from its number in the list of maps of the game (data\maps.txt).</summary>
		public String? MapName(Int32 number)
		{
			_maps ??= ReadMaps();
			return _maps.TryGetValue(number, out String? name) ? name : null;
		}

		private Int32? FindSubtype(UInt32 pid)
		{
			String? folder = (pid >> 24) switch
			{
				0 => "items",
				2 => "scenery",
				_ => null,
			};

			Int32 index = (Int32)(pid & 0x00FFFFFF);
			if (folder == null || index < 1)
			{
				return null;
			}

			IReadOnlyList<String> list = ReadList($@"proto\{folder}\{folder}.lst");
			if (index > list.Count)
			{
				return null;
			}

			Byte[]? data = _read($@"proto\{folder}\{list[index - 1]}");
			if (data == null)
			{
				return null;
			}

			try
			{
				return ProReader.Read(data) switch
				{
					ItemProto item => (Int32)item.ItemType,
					SceneryProto scenery => (Int32)scenery.SceneryType,
					_ => null,
				};
			}
			catch (InvalidDataException)
			{
				return null;
			}
		}

		/// <summary>A list file (.lst): one name per line, followed by an optional comment.</summary>
		private IReadOnlyList<String> ReadList(String path)
		{
			if (_lists.TryGetValue(path, out IReadOnlyList<String>? list))
			{
				return list;
			}

			List<String> names = new();
			Byte[]? data = _read(path);
			if (data != null)
			{
				foreach (String line in Encoding.Latin1.GetString(data).Split('\n'))
				{
					String text = line.Trim();
					Int32 end = text.IndexOfAny([' ', '\t', ';']);
					names.Add(end < 0 ? text : text[..end]);
				}

				// A file that ends with a line break has no last name.
				if (names.Count > 0 && names[^1].Length == 0)
				{
					names.RemoveAt(names.Count - 1);
				}
			}

			_lists[path] = names;
			return names;
		}

		/// <summary>The sections "[Map 012]" of maps.txt: the name shown to the player, then the name of the file.</summary>
		private IReadOnlyDictionary<Int32, String> ReadMaps()
		{
			Dictionary<Int32, String> maps = new();
			Byte[]? data = _read(@"data\maps.txt");
			if (data == null)
			{
				return maps;
			}

			Int32 number = -1;
			String? display = null;
			String? file = null;

			void Close()
			{
				String? name = display != null && file != null ? $"{display} ({file})" : display ?? file;
				if (number >= 0 && name != null)
				{
					maps[number] = name;
				}
			}

			foreach (String line in Encoding.Latin1.GetString(data).Split('\n'))
			{
				String text = line.Trim();
				if (text.StartsWith("[Map ", StringComparison.OrdinalIgnoreCase) && text.EndsWith(']'))
				{
					Close();
					number = Int32.TryParse(text[5..^1].Trim(), out Int32 parsed) ? parsed : -1;
					display = null;
					file = null;
				}
				else if (text.StartsWith("lookup_name", StringComparison.OrdinalIgnoreCase))
				{
					display = ValueOf(text);
				}
				else if (text.StartsWith("map_name", StringComparison.OrdinalIgnoreCase))
				{
					file = ValueOf(text);
				}
			}

			Close();
			return maps;
		}

		private static String? ValueOf(String line)
		{
			Int32 equals = line.IndexOf('=');
			if (equals < 0)
			{
				return null;
			}

			String value = line[(equals + 1)..];
			Int32 comment = value.IndexOf(';');
			value = (comment < 0 ? value : value[..comment]).Trim();
			return value.Length == 0 ? null : value;
		}
	}
}