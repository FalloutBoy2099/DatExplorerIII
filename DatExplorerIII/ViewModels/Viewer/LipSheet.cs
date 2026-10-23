namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Formats.Audio;
	using Models.Audio;
	using Services.Interface;

	/// <summary>
	/// The sheet of a lip synchronization file (.lip) shown in the viewer: what the file says of itself, then its phonemes and
	/// its markers.
	/// </summary>
	internal static class LipSheet
	{
		public static IReadOnlyList<CharacterSection> Build(LipFile file)
		{
			List<CharacterRow> info =
			[
				new(Loc.Get("Str.LipVersion"), file.Version.ToString(CultureInfo.CurrentCulture)),
				new(Loc.Get("Str.LipAcm"), file.AcmName),
				new(Loc.Get("Str.LipLength"), CharacterViewerViewModel.Number(file.UnpackedLength)),
				new(Loc.Get("Str.LipPhonemeCount"), CharacterViewerViewModel.Number(file.Phonemes.Count)),
				new(Loc.Get("Str.LipMarkerCount"), CharacterViewerViewModel.Number(file.Markers.Count)),
			];

			List<CharacterRow> phonemes = [with(file.Phonemes.Count)];
			for (Int32 index = 0; index < file.Phonemes.Count; index++)
			{
				Byte code = file.Phonemes[index];
				(String ipa, Int32 frame) = code < LipReader.Phonemes.Count ? LipReader.Phonemes[code] : ("?", 0);
				phonemes.Add(new CharacterRow((index + 1).ToString(CultureInfo.CurrentCulture), Loc.Format("Str.LipPhonemeValue", ipa, frame, code.ToString("X2", CultureInfo.InvariantCulture))));
			}

			List<CharacterRow> markers = [with(file.Markers.Count)];
			for (Int32 index = 0; index < file.Markers.Count; index++)
			{
				LipMarker marker = file.Markers[index];
				markers.Add(new CharacterRow(index.ToString(CultureInfo.CurrentCulture), Loc.Format("Str.LipMarkerValue", marker.Type, CharacterViewerViewModel.Number(marker.Sample))));
			}

			return
			[
				new CharacterSection(Loc.Get("Str.LipSectionFile"), info),
				new CharacterSection(Loc.Get("Str.LipSectionPhonemes"), phonemes),
				new CharacterSection(Loc.Get("Str.LipSectionMarkers"), markers),
			];
		}
	}
}