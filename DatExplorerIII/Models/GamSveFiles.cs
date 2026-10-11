namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>A variable of a .gam file.</summary>
	/// <param name="Index">Its place in the list, from 0: what the scripts call it by.</param>
	/// <param name="Name">Its name (GVAR_..., MVAR_...).</param>
	/// <param name="Value">Its initial value, as written.</param>
	/// <param name="Comment">The comment that comes after it, without the number of its place.</param>
	public sealed record GamVariable(Int32 Index, String Name, String Value, String Comment);

	/// <summary>
	/// A .gam file of Fallout: the global variables of the game (GAME_GLOBAL_VARS) or of a map (MAP_GLOBAL_VARS), with their
	/// initial value.
	/// </summary>
	/// <param name="Header">The line that starts the list (GAME_GLOBAL_VARS or MAP_GLOBAL_VARS); empty when there is none.</param>
	/// <param name="Variables">The variables, in the order of the file.</param>
	/// <param name="SkippedCount">How many lines were neither a variable, a comment nor the header.</param>
	/// <param name="EncodingName">The encoding the file was read with.</param>
	public sealed record GamFile(String Header, IReadOnlyList<GamVariable> Variables, Int32 SkippedCount, String EncodingName);

	/// <summary>A subtitle of a .sve file: the text that shows from a picture of the movie on.</summary>
	/// <param name="Frame">The number of the picture of the movie where the text appears.</param>
	/// <param name="Text">The text.</param>
	public sealed record SveEntry(Int32 Frame, String Text);

	/// <summary>
	/// A .sve file of Fallout: the subtitles of a movie (the .mve file of the same name), by the number of the picture they
	/// appear on, in order.
	/// </summary>
	/// <param name="Entries">The subtitles, ordered by picture.</param>
	/// <param name="SkippedCount">How many lines were not subtitles.</param>
	/// <param name="EncodingName">The encoding the file was read with.</param>
	public sealed record SveFile(IReadOnlyList<SveEntry> Entries, Int32 SkippedCount, String EncodingName);
}