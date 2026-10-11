namespace DatExplorerIII.Models
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// One field of an object of Arcanum.
	/// </summary>
	/// <param name="Bit">The number of the field: its place in the bitmap of the file.</param>
	/// <param name="Name">The name of the field (the name the game gives it, in words).</param>
	/// <param name="Value">The value, written as text: a number, a text, a color, a list of numbers.</param>
	public sealed record ArcanumProperty(Int32 Bit, String Name, String Value);

	/// <summary>
	/// The description of an object of Arcanum read from a prototype file (.pro): what kind of object it is and the values of
	/// its fields (art, weight, hit points, damage...).
	/// </summary>
	/// <param name="Version">The version of the format of the file (0x77, or 0x08 for the first version of the game).</param>
	/// <param name="Number">The number of the prototype (the number in the name of its file in the data of the game).</param>
	/// <param name="Type">The kind of object.</param>
	/// <param name="Properties">The fields, in the order of the file.</param>
	/// <param name="Note">Null when the whole file was read; else why the reading stopped (a field of a kind that is not known, a file that ends early).</param>
	public sealed record ArcanumProto(Int32 Version, Int32 Number, ArcanumObjectType Type, IReadOnlyList<ArcanumProperty> Properties, String? Note);
}