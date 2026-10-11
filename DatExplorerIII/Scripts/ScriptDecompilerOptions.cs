namespace DatExplorerIII.Scripts
{
	using System;

	/// <summary>Which game a script was made for: the opcodes of the two games are not quite the same.</summary>
	public enum ScriptGame
	{
		/// <summary>Fallout 1.</summary>
		Fallout1,

		/// <summary>Fallout 2, and what the Fallout 2 engine mods (sfall) add.</summary>
		Fallout2,
	}

	/// <summary>How a script is decompiled.</summary>
	public sealed class ScriptDecompilerOptions
	{
		/// <summary>The game the script is for (the opcodes that only one game has are named accordingly).</summary>
		public ScriptGame Game { get; set; } = ScriptGame.Fallout2;

		/// <summary>
		/// Goes on when a call has fewer arguments than its function takes (some compilers leave out the last ones) instead of
		/// failing.
		/// </summary>
		public Boolean IgnoreWrongArgumentCount { get; set; }

		/// <summary>When arguments are missing, supposes they are the ones before the call, not the ones after.</summary>
		public Boolean InsertOmittedArgumentsBackward { get; set; }

		/// <summary>Writes <c>and</c> and <c>or</c> as the operators that always evaluate both sides, as the old compilers did.</summary>
		public Boolean UseOldShortCircuitKeywords { get; set; }

		/// <summary>The longest a decompilation may take before it is given up; zero or less for no limit.</summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

		/// <summary>What one level of indentation is made of.</summary>
		public String Indent { get; set; } = "\t";

		internal ScriptDecompilerOptions Clone() => (ScriptDecompilerOptions)MemberwiseClone();
	}
}