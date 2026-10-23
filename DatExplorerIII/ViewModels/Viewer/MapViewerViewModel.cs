namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Infrastructure;
	using Models;
	using Services;
	using static CharacterViewerViewModel;

	/// <summary>
	/// What a map of Fallout (.map) holds, shown in the viewer: its header, what each elevation contains, its scripts and the
	/// places that lead to another map.
	/// </summary>
	public sealed class MapViewerViewModel : ObservableObject
	{
		/// <summary>The squares of a row of the hexagonal grid of a map.</summary>
		private const Int32 HexagonsPerRow = 200;

		/// <summary>A map can hold hundreds of scripts: the list is cut after this many lines.</summary>
		private const Int32 MaxScriptRows = 300;

		private MapFile? _map;
		private IReadOnlyList<CharacterSection> _sections = Array.Empty<CharacterSection>();

		/// <summary>The groups of lines; they share the layout of the sheet of a character.</summary>
		public IReadOnlyList<CharacterSection> Sections
		{
			get => _sections;
			private set => SetProperty(ref _sections, value);
		}

		public void Show(MapFile map)
		{
			_map = map;
			Sections = Build(map);
		}

		public void Clear()
		{
			_map = null;
			Sections = Array.Empty<CharacterSection>();
		}

		/// <summary>Names the lines again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			if (_map != null)
			{
				Sections = Build(_map);
			}
		}

		/// <summary>The place of a hexagon on the map: its column and its row.</summary>
		internal static String Position(Int32 tile)
		{
			return tile < 0 ? "—" : $"({(tile % HexagonsPerRow).ToString(CultureInfo.InvariantCulture)}, {(tile / HexagonsPerRow).ToString(CultureInfo.InvariantCulture)})";
		}

		/// <summary>Elevations are numbered from 0 in the file; people count from 1.</summary>
		private static String ElevationNumber(Int32 elevation) => (elevation + 1).ToString(CultureInfo.InvariantCulture);

		internal static IReadOnlyList<CharacterSection> Build(MapFile map)
		{
			List<CharacterSection> sections = new() { BuildGeneral(map) };

			foreach (MapElevation elevation in map.Elevations)
			{
				sections.Add(BuildElevation(elevation));
			}

			if (map.Scripts.Count > 0)
			{
				sections.Add(BuildScripts(map.Scripts));
			}

			if (map.Transitions.Count > 0)
			{
				sections.Add(BuildTransitions(map.Transitions));
			}

			return sections;
		}

		private static CharacterSection BuildGeneral(MapFile map)
		{
			MapHeader header = map.Header;
			List<CharacterRow> rows = new()
			{
				Row("Str.MapName", header.Name),
				Row("Str.MapFormat", $"Fallout {(header.Version == 19 ? 1 : 2)} ({header.Version})"),
				Row("Str.MapNumber", header.DisplayName != null ? $"{header.MapNumber} - {header.DisplayName}" : header.MapNumber.ToString(CultureInfo.InvariantCulture)),
				Row("Str.MapEntry", Loc.Format("Str.MapEntryValue", Position(header.PlayerTile), ElevationNumber(header.PlayerElevation), header.PlayerOrientation)),
				Row("Str.MapDarkness", header.Darkness),
				Row("Str.MapScript", header.ScriptName ?? (header.ScriptId > 0 ? "#" + header.ScriptId.ToString(CultureInfo.InvariantCulture) : "—")),
				Row("Str.MapElevations", map.Elevations.Count),
				Row("Str.MapObjects", map.TotalObjects),
				Row("Str.MapGlobalVariables", map.GlobalVariableCount),
				Row("Str.MapLocalVariables", map.LocalVariableCount),
			};

			if (map.Note != null)
			{
				rows.Add(new CharacterRow(Loc.Format("Str.MapIncomplete", map.Note), String.Empty));
			}

			return new CharacterSection(Loc.Get("Str.ProSectionGeneral"), rows);
		}

		private static CharacterSection BuildElevation(MapElevation elevation)
		{
			List<CharacterRow> rows = new()
			{
				Row("Str.MapFloorTiles", elevation.FloorTiles),
				Row("Str.MapRoofTiles", elevation.RoofTiles),
			};

			if (elevation.ObjectsByType is { Count: 6 } counts)
			{
				rows.Add(Row("Str.MapItems", counts[0]));
				rows.Add(Row("Str.MapCritters", counts[1]));
				rows.Add(Row("Str.MapScenery", counts[2]));
				rows.Add(Row("Str.MapWalls", counts[3]));
				rows.Add(Row("Str.MapOtherObjects", counts[4] + counts[5]));
				rows.Add(Row("Str.MapCarried", elevation.CarriedItems));
			}

			return new CharacterSection(Loc.Format("Str.MapElevation", ElevationNumber(elevation.Index)), rows);
		}

		/// <summary>
		/// The scripts by type: those that wait at a place (spatial) one by one, the others grouped by program with how many
		/// objects use it.
		/// </summary>
		private static CharacterSection BuildScripts(IReadOnlyList<MapScriptEntry> scripts)
		{
			List<CharacterRow> rows = new();
			foreach (IGrouping<Int32, MapScriptEntry> type in scripts.GroupBy(script => script.Type).OrderBy(group => group.Key))
			{
				String typeName = Loc.Get("Str.MapScriptType" + type.Key);
				if (type.Key == 1)
				{
					foreach (MapScriptEntry script in type)
					{
						rows.Add(new CharacterRow(typeName + ": " + ScriptLabel(script), Loc.Format("Str.MapSpatialValue", Position(script.Tile), ElevationNumber(script.Elevation), script.Radius)));
					}
				}
				else
				{
					foreach (IGrouping<String, MapScriptEntry> program in type.GroupBy(ScriptLabel).OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
					{
						rows.Add(new CharacterRow(typeName + ": " + program.Key, "× " + program.Count().ToString(CultureInfo.InvariantCulture)));
					}
				}
			}

			if (rows.Count > MaxScriptRows)
			{
				rows.RemoveRange(MaxScriptRows, rows.Count - MaxScriptRows);
				rows.Add(new CharacterRow("…", String.Empty));
			}

			return new CharacterSection(Loc.Format("Str.MapScripts", Number(scripts.Count)), rows);
		}

		private static String ScriptLabel(MapScriptEntry script)
		{
			return script.Name ?? "#" + script.ScriptId.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>The places that lead somewhere else, grouped by what they lead to (an exit is often many squares wide).</summary>
		private static CharacterSection BuildTransitions(IReadOnlyList<MapTransition> transitions)
		{
			List<CharacterRow> rows = new();
			IEnumerable<IGrouping<(MapTransitionKind Kind, Int32 Map, Int32 Tile, Int32 Elevation, Int32 Source), MapTransition>> groups = transitions
				.GroupBy(transition => (transition.Kind, transition.DestinationMap, transition.DestinationTile, transition.DestinationElevation, transition.Elevation));

			foreach (IGrouping<(MapTransitionKind Kind, Int32 Map, Int32 Tile, Int32 Elevation, Int32 Source), MapTransition> group in groups)
			{
				MapTransition first = group.First();
				String destination = first.DestinationMap < 0
					? Loc.Get("Str.MapSameMap")
					: first.DestinationMapName != null ? first.DestinationMap.ToString(CultureInfo.InvariantCulture) + " - " + first.DestinationMapName : Loc.Format("Str.MapNumberedMap", first.DestinationMap);

				String label = Loc.Get("Str.MapExitKind" + (Int32)group.Key.Kind) + " → " + destination;
				String arrival = Loc.Format("Str.MapArrival", Position(first.DestinationTile), ElevationNumber(first.DestinationElevation));
				String from = Loc.Format("Str.MapExitFrom", ElevationNumber(group.Key.Source));
				rows.Add(new CharacterRow(label, group.Count() > 1 ? $"{from} × {group.Count()} · {arrival}" : $"{from} {Position(first.Tile)} · {arrival}"));
			}

			return new CharacterSection(Loc.Format("Str.MapExits", Number(transitions.Count)), rows);
		}

		private static CharacterRow Row(String key, String value) => new(Loc.Get(key), value);

		private static CharacterRow Row(String key, Int32 value) => new(Loc.Get(key), Number(value));
	}
}