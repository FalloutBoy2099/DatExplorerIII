namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// What a map of Fallout (.map) holds, without its tiles and objects one by one: the header, the number of variables, what each
	/// elevation contains, the scripts and the places that lead to another map.
	/// </summary>
	/// <param name="Header">The fixed part at the start of the file.</param>
	/// <param name="GlobalVariableCount">Variables of the map that the scripts share.</param>
	/// <param name="LocalVariableCount">Variables of the map for its scripts.</param>
	/// <param name="Elevations">The elevations the map has (up to three).</param>
	/// <param name="Scripts">The scripts of the map, by type.</param>
	/// <param name="Transitions">The exit grids, stairs and ladders that lead to another map.</param>
	/// <param name="TotalObjects">The number of objects the file announces (inventories not included).</param>
	/// <param name="Note">Set when the file could not be read to the end: why, in English.</param>
	public sealed record MapFile(
		MapHeader Header,
		Int32 GlobalVariableCount,
		Int32 LocalVariableCount,
		IReadOnlyList<MapElevation> Elevations,
		IReadOnlyList<MapScriptEntry> Scripts,
		IReadOnlyList<MapTransition> Transitions,
		Int32 TotalObjects,
		String? Note);

	/// <summary>The start of a map file.</summary>
	/// <param name="Version">19 for Fallout 1, 20 for Fallout 2.</param>
	/// <param name="Name">The name of the map file.</param>
	/// <param name="PlayerTile">Where the player appears (a hexagon: row * 200 + column).</param>
	/// <param name="PlayerElevation">The elevation where the player appears.</param>
	/// <param name="PlayerOrientation">The direction the player faces (0 to 5, clockwise from the north east).</param>
	/// <param name="ScriptId">The identifier of the script of the map (0 or -1 for none; the place of its program in the list of scripts is one less).</param>
	/// <param name="Flags">Bit 0: the map is part of a saved game. Bits 1 to 3: the elevation 0, 1 or 2 does not exist.</param>
	/// <param name="Darkness">The ambient light.</param>
	/// <param name="MapNumber">The number of the map in the list of maps of the game.</param>
	/// <param name="DisplayName">The name of the map in the list of maps of the game, when that list was found.</param>
	/// <param name="ScriptName">The program of the script of the map, when the list of scripts was found.</param>
	public sealed record MapHeader(
		Int32 Version,
		String Name,
		Int32 PlayerTile,
		Int32 PlayerElevation,
		Int32 PlayerOrientation,
		Int32 ScriptId,
		Int32 Flags,
		Int32 Darkness,
		Int32 MapNumber,
		String? DisplayName = null,
		String? ScriptName = null);

	/// <summary>What one elevation of a map holds.</summary>
	/// <param name="Index">0, 1 or 2.</param>
	/// <param name="FloorTiles">How many of its 10 000 floor squares are not empty.</param>
	/// <param name="RoofTiles">How many of its 10 000 roof squares are not empty.</param>
	/// <param name="ObjectsByType">The number of objects of each kind (items, critters, scenery, walls, tiles, others); null when the objects could not be counted.</param>
	/// <param name="CarriedItems">The objects carried by the others (in inventories and containers).</param>
	public sealed record MapElevation(Int32 Index, Int32 FloorTiles, Int32 RoofTiles, IReadOnlyList<Int32>? ObjectsByType, Int32 CarriedItems);

	/// <summary>A script of a map.</summary>
	/// <param name="Type">0 system, 1 spatial, 2 timer, 3 item, 4 critter.</param>
	/// <param name="ScriptId">The place of the script program in the list of scripts of the game.</param>
	/// <param name="Tile">For a spatial script, the hexagon that triggers it; otherwise -1.</param>
	/// <param name="Elevation">For a spatial script, its elevation; otherwise -1.</param>
	/// <param name="Radius">For a spatial script, how far from the hexagon it triggers; otherwise 0.</param>
	/// <param name="Name">The program (a .int file) when the list of scripts was found.</param>
	public sealed record MapScriptEntry(Int32 Type, Int32 ScriptId, Int32 Tile, Int32 Elevation, Int32 Radius, String? Name);

	/// <summary>What leads to another map.</summary>
	public enum MapTransitionKind
	{
		ExitGrid,
		Stairs,
		Ladder,
	}

	/// <summary>A place of a map that leads to another map (or to another place of the same map).</summary>
	/// <param name="Kind">An exit grid, stairs or a ladder.</param>
	/// <param name="Elevation">Where it is.</param>
	/// <param name="Tile">Where it is (a hexagon).</param>
	/// <param name="DestinationMap">The number of the map it leads to (-1 for the same map).</param>
	/// <param name="DestinationTile">The hexagon where the player arrives.</param>
	/// <param name="DestinationElevation">The elevation where the player arrives.</param>
	/// <param name="DestinationMapName">The name of the map it leads to when the list of maps was found.</param>
	public sealed record MapTransition(
		MapTransitionKind Kind,
		Int32 Elevation,
		Int32 Tile,
		Int32 DestinationMap,
		Int32 DestinationTile,
		Int32 DestinationElevation,
		String? DestinationMapName);
}