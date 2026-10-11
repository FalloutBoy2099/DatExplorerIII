namespace DatExplorerIII.Scripts
{
	using System;
	using System.Collections.Generic;

	/// <summary>The source of a decompiled script, and what the decompiler had to say about it.</summary>
	/// <param name="Text">The source (<c>.ssl</c>).</param>
	/// <param name="Messages">The warnings: things in the compiled script that did not look as the compilers write them.</param>
	public sealed record DecompiledScript(String Text, IReadOnlyList<String> Messages);
}