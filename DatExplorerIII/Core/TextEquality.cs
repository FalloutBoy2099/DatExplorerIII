namespace DatExplorerIII.Core
{
	using System;
	using Models.Archives;

	/// <summary>A version of a file that is being compared: its name, the format of the archive it is in, and its content.</summary>
	public sealed record ComparedFile(String Name, DatFormat Format, Byte[] Data);

	/// <summary>
	/// How the files that are compared as text (the texts, and the compiled scripts as the source they decompile to) are compared in
	/// the comparison of two archives, when it is not byte for byte: which files they are, and whether the content of two of them is
	/// the same for what the user chose to ignore.
	/// </summary>
	/// <param name="AppliesTo">Whether a file (by its name) is compared as a text.</param>
	/// <param name="AreEqual">Whether the content of two versions of a file is the same.</param>
	public sealed record TextEquality(Func<String, Boolean> AppliesTo, Func<ComparedFile, ComparedFile, Boolean> AreEqual);
}