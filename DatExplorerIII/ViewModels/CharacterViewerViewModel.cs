namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Infrastructure;
	using Models;
	using Services;

	/// <summary>A line of the sheet of a character: what it is, and its value.</summary>
	public sealed record CharacterRow(String Label, String Value);

	/// <summary>A group of lines of the sheet of a character.</summary>
	public sealed record CharacterSection(String Title, IReadOnlyList<CharacterRow> Rows);

	/// <summary>
	/// The sheet of a character (.gcd) shown in the viewer: its statistics, resistances, tagged skills and traits.
	/// </summary>
	public sealed class CharacterViewerViewModel : ObservableObject
	{
		private const Int32 TraitCount = 16;

		private Func<IReadOnlyList<CharacterSection>>? _build;
		private IReadOnlyList<CharacterSection> _sections = Array.Empty<CharacterSection>();

		public IReadOnlyList<CharacterSection> Sections
		{
			get => _sections;
			private set => SetProperty(ref _sections, value);
		}

		public void Show(GcdCharacter character)
		{
			ShowSheet(() => Build(character));
		}

		/// <summary>Shows a sheet; <paramref name="build"/> writes it, and writes it again when the language of the interface changes.</summary>
		public void ShowSheet(Func<IReadOnlyList<CharacterSection>> build)
		{
			_build = build;
			Sections = build();
		}

		public void Clear()
		{
			_build = null;
			Sections = Array.Empty<CharacterSection>();
		}

		/// <summary>Names the lines again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			if (_build != null)
			{
				Sections = _build();
			}
		}

		internal static String Number(Int32 value) => value.ToString("N0", CultureInfo.CurrentCulture);

		/// <summary>The primary statistics, with their names.</summary>
		internal static IReadOnlyList<CharacterRow> SpecialRows(Func<Int32, Int32> stat)
		{
			return Enumerable.Range((Int32)CharacterStat.Strength, 7).Select(index => new CharacterRow(Loc.Get("Str.Stat" + index), Number(stat(index)))).ToList();
		}

		/// <summary>The secondary statistics (hit points, action points...), with their names.</summary>
		internal static IReadOnlyList<CharacterRow> DerivedRows(Func<Int32, Int32> stat)
		{
			return Enumerable.Range((Int32)CharacterStat.HitPoints, (Int32)CharacterStat.BetterCriticals - (Int32)CharacterStat.HitPoints + 1)
				.Select(index => new CharacterRow(Loc.Get("Str.Stat" + index), Number(stat(index)))).ToList();
		}

		/// <summary>The resistances: a line for each kind of damage, then radiation and poison.</summary>
		internal static IReadOnlyList<CharacterRow> ResistanceRows(Func<Int32, Int32> stat)
		{
			List<CharacterRow> resistances = new();
			for (Int32 type = 0; type < 7; type++)
			{
				resistances.Add(new CharacterRow(Loc.Get("Str.Damage" + type), ResistanceText(stat((Int32)CharacterStat.ThresholdNormal + type), stat((Int32)CharacterStat.ResistanceNormal + type))));
			}

			resistances.Add(new CharacterRow(Loc.Get("Str.Stat31"), Number(stat((Int32)CharacterStat.RadiationResistance)) + " %"));
			resistances.Add(new CharacterRow(Loc.Get("Str.Stat32"), Number(stat((Int32)CharacterStat.PoisonResistance)) + " %"));
			return resistances;
		}

		/// <summary>"Threshold 4 - Resistance 30 %".</summary>
		internal static String ResistanceText(Int32 threshold, Int32 resistance)
		{
			return Loc.Format("Str.GcdResistanceFormat", Number(threshold), Number(resistance));
		}

		internal static IReadOnlyList<CharacterSection> Build(GcdCharacter character)
		{
			Int32 Stat(Int32 index) => character.GetStat((CharacterStat)index);

			List<CharacterSection> sections = new();

			Boolean female = Stat((Int32)CharacterStat.Gender) == 1;
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionCharacter"),
			[
				new CharacterRow(Loc.Get("Str.GcdName"), character.Name),
				new CharacterRow(Loc.Get("Str.GcdAge"), Number(Stat((Int32)CharacterStat.Age))),
				new CharacterRow(Loc.Get("Str.GcdGender"), Loc.Get(female ? "Str.GcdFemale" : "Str.GcdMale")),
				new CharacterRow(Loc.Get("Str.GcdExperience"), Number(character.Experience)),
			]));

			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionSpecial"), SpecialRows(Stat)));
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionDerived"), DerivedRows(Stat)));
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionResistances"), ResistanceRows(Stat)));

			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionTagged"), NamedRows(character.TaggedSkills, GcdCharacter.SkillCount, "Str.Skill")));
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionTraits"), NamedRows(character.Traits, TraitCount, "Str.Trait")));
			return sections;
		}

		/// <summary>One line for each choice that was made (a number of -1 is no choice), or a line saying there is none.</summary>
		private static IReadOnlyList<CharacterRow> NamedRows(Int32[] choices, Int32 count, String keyPrefix)
		{
			List<CharacterRow> rows = choices
				.Where(choice => choice >= 0 && choice < count)
				.Select(choice => new CharacterRow(Loc.Get(keyPrefix + choice), String.Empty))
				.ToList();

			if (rows.Count == 0)
			{
				rows.Add(new CharacterRow(Loc.Get("Str.GcdNone"), String.Empty));
			}

			return rows;
		}
	}
}