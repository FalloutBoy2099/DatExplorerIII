namespace DatExplorerIII.Models.Audio
{
	using System;
	using System.Collections.Generic;

	/// <summary>A marker of a lip synchronization file: where a phoneme starts in the sound.</summary>
	/// <param name="Type">1 for a silence or the start of a word, 0 inside a word.</param>
	/// <param name="Sample">The position in the unpacked sound.</param>
	public sealed record LipMarker(Int32 Type, Int32 Sample);

	/// <summary>
	/// A lip synchronization file (.lip) of Fallout 2 (also used by Fallout): the phonemes of a speech, which drive the mouth of
	/// the talking head, and where they start in the sound.
	/// </summary>
	/// <param name="Version">The version of the file (2 in the games).</param>
	/// <param name="UnpackedLength">The length of the unpacked sound that the file holds, as it says.</param>
	/// <param name="AcmName">The name of the sound file (without extension).</param>
	/// <param name="Phonemes">The phoneme codes, in order (0 to 0x29).</param>
	/// <param name="Markers">The markers: one more than there are phonemes, the first being the start.</param>
	public sealed record LipFile(Int32 Version, Int32 UnpackedLength, String AcmName, IReadOnlyList<Byte> Phonemes, IReadOnlyList<LipMarker> Markers);
}