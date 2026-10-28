namespace DatExplorerIII.Models.GameFiles
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A property of an entity of Fallout Tactics: a name and a value shown as text, or a group of properties (a nested entity or a table).
	/// </summary>
	/// <param name="Name">The name of the property.</param>
	/// <param name="Value">The value as text (the class of a nested entity, for a group).</param>
	/// <param name="Children">The properties of a group (empty for a plain value).</param>
	public sealed record EntNode(String Name, String Value, IReadOnlyList<EntNode> Children);

	/// <summary>
	/// An entity of Fallout Tactics (.ent): a class (Actor, Item...) and its properties, some of which hold nested entities.
	/// </summary>
	public sealed record EntFile(String ClassName, IReadOnlyList<EntNode> Properties);
}
