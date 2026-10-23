namespace DatExplorerIII.ViewModels.Viewer
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Infrastructure;
	using Models.Prototypes;
	using Services.Interface;
	using static CharacterViewerViewModel;

	/// <summary>
	/// The description of an object of the game (a prototype, .pro) shown in the viewer: what every object has, then what
	/// its kind of object has (the damage of a weapon, the resistances of armor, the statistics of a creature...).
	/// </summary>
	public sealed class PrototypeViewerViewModel : ObservableObject
	{
		private ProtoFile? _prototype;
		private ArcanumProto? _arcanum;
		private IReadOnlyList<CharacterSection> _sections = Array.Empty<CharacterSection>();

		/// <summary>The groups of lines of the description; they share the layout of the sheet of a character.</summary>
		public IReadOnlyList<CharacterSection> Sections
		{
			get => _sections;
			private set => SetProperty(ref _sections, value);
		}

		public void Show(ProtoFile prototype)
		{
			_prototype = prototype;
			_arcanum = null;
			Sections = Build(prototype);
		}

		/// <summary>Shows the description of an object of Arcanum.</summary>
		public void Show(ArcanumProto prototype)
		{
			_prototype = null;
			_arcanum = prototype;
			Sections = Build(prototype);
		}

		public void Clear()
		{
			_prototype = null;
			_arcanum = null;
			Sections = Array.Empty<CharacterSection>();
		}

		/// <summary>Names the lines again after the language of the interface changed.</summary>
		public void RefreshLanguage()
		{
			if (_prototype != null)
			{
				Sections = Build(_prototype);
			}
			else if (_arcanum != null)
			{
				Sections = Build(_arcanum);
			}
		}

		/// <summary>
		/// An object of Arcanum: its kind, its number, then its fields (the names of the fields are those the game gives them; the
		/// fields that hold nothing are left out).
		/// </summary>
		internal static IReadOnlyList<CharacterSection> Build(ArcanumProto prototype)
		{
			List<CharacterRow> general =
			[
				new CharacterRow(Loc.Get("Str.ProType"), Loc.Get("Str.ArcProType" + (Int32)prototype.Type)),
				Row("Str.ArcProNumber", prototype.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)),
				Row("Str.ArcProVersion", "0x" + prototype.Version.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)),
			];

			if (prototype.Note != null)
			{
				String last = prototype.Properties.Count > 0 ? prototype.Properties[^1].Name : String.Empty;
				general.Add(new CharacterRow(Loc.Format("Str.ArcProIncomplete", last), String.Empty));
			}

			List<CharacterRow> fields = prototype.Properties
				.Where(property => property.Value.Length > 0 && property.Value != "[]" && property.Value != "\u2014")
				.Select(property => new CharacterRow(property.Name, property.Value))
				.ToList();

			List<CharacterSection> sections = [new CharacterSection(Loc.Get("Str.ProSectionGeneral"), general)];
			if (fields.Count > 0)
				sections.Add(new CharacterSection(Loc.Get("Str.ArcProFields"), fields));

			return sections;
		}

		private static CharacterRow Row(String key, String value) => new(Loc.Get(key), value);

		private static CharacterRow Row(String key, Int32 value) => new(Loc.Get(key), Number(value));

		/// <summary>The value the tools of the game left in the numbers that were never set (an uninitialized memory pattern).</summary>
		private const Int32 Unset = unchecked((Int32)0xCCCCCCCC);

		/// <summary>An identifier (of a picture, a script...) as it is written in the files; -1 and an unset number are "none".</summary>
		private static String Identifier(Int32 value) => value is -1 or Unset ? Loc.Get("Str.GcdNone") : Number(value);

		private static String Hex(Int32 value) => "0x" + value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);

		/// <summary>The name of a value that is a position in a list of names; the number alone when it is outside.</summary>
		private static String Named(String keyPrefix, Int32 value, Int32 count)
		{
			return value >= 0 && value < count ? Loc.Get(keyPrefix + value) : Number(value);
		}

		internal static IReadOnlyList<CharacterSection> Build(ProtoFile prototype)
		{
			List<CharacterRow> general =
			[
				new CharacterRow(Loc.Get("Str.ProType"), Loc.Get("Str.ProType" + (Int32)prototype.Type)),
				Row("Str.ProId", prototype.Id),
				Row("Str.ProMessage", prototype.MessageId),
				Row("Str.ProPicture", Identifier(prototype.Fid)),
				Row("Str.ProLightDistance", prototype.LightDistance),
				Row("Str.ProLightIntensity", prototype.LightIntensity),
				Row("Str.ProFlags", Hex(prototype.Flags)),
			];

			List<CharacterSection> sections = [new CharacterSection(Loc.Get("Str.ProSectionGeneral"), general)];

			switch (prototype)
			{
				case ItemProto item:
					AddItem(sections, general, item);
					break;

				case CritterProto critter:
					AddCritter(sections, general, critter);
					break;

				case SceneryProto scenery:
					general.Add(Row("Str.ProExtendedFlags", Hex(scenery.ExtendedFlags)));
					general.Add(Row("Str.ProScript", Identifier(scenery.ScriptId)));
					sections.Add(new CharacterSection(Loc.Get("Str.ProScenery" + (Int32)scenery.SceneryType), SceneryRows(scenery)));
					break;

				case WallProto wall:
					general.Add(Row("Str.ProExtendedFlags", Hex(wall.ExtendedFlags)));
					general.Add(Row("Str.ProScript", Identifier(wall.ScriptId)));
					general.Add(new CharacterRow(Loc.Get("Str.ProMaterial"), Named("Str.ProMaterial", wall.Material, 8)));
					break;

				case TileProto tile:
					general.Add(new CharacterRow(Loc.Get("Str.ProMaterial"), Named("Str.ProMaterial", tile.Material, 8)));
					break;

				case MiscProto misc:
					general.Add(Row("Str.ProExtendedFlags", Hex(misc.ExtendedFlags)));
					break;
			}

			return sections;
		}

		private static IReadOnlyList<CharacterRow> SceneryRows(SceneryProto scenery)
		{
			List<CharacterRow> rows =
			[
				new CharacterRow(Loc.Get("Str.ProMaterial"), Named("Str.ProMaterial", scenery.Material, 8)),
				Row("Str.ProSoundCode", scenery.SoundId.ToString()),
			];

			if (scenery.Door is { } door)
			{
				rows.Add(Row("Str.ProOpenFlags", Hex(door.OpenFlags)));
				rows.Add(Row("Str.ProKeyCode", Identifier(door.KeyCode)));
			}

			if (scenery.Stairs is { } stairs)
			{
				rows.Add(Row("Str.ProDestinationTile", Hex(stairs.DestinationTile)));
				rows.Add(Row("Str.ProDestinationMap", Identifier(stairs.DestinationMap)));
			}

			if (scenery.Elevator is { } elevator)
			{
				rows.Add(Row("Str.ProElevatorType", Identifier(elevator.ElevatorType)));
				rows.Add(Row("Str.ProElevatorLevel", Identifier(elevator.Level)));
			}

			if (scenery.LadderDestination is { } ladder)
				rows.Add(Row("Str.ProDestinationTile", Hex(ladder)));

			return rows;
		}

		private static void AddItem(List<CharacterSection> sections, List<CharacterRow> general, ItemProto item)
		{
			general.Add(Row("Str.ProExtendedFlags", Hex(item.ExtendedFlags)));
			general.Add(Row("Str.ProScript", Identifier(item.ScriptId)));
			general.Add(new CharacterRow(Loc.Get("Str.ProSubtype"), Loc.Get("Str.ProItem" + (Int32)item.ItemType)));
			general.Add(new CharacterRow(Loc.Get("Str.ProMaterial"), Named("Str.ProMaterial", item.Material, 8)));
			general.Add(Row("Str.ProSize", item.Size));
			general.Add(Row("Str.ProWeight", item.Weight));
			general.Add(Row("Str.ProCost", item.Cost));
			general.Add(Row("Str.ProInventoryPicture", Identifier(item.InventoryFid)));
			general.Add(Row("Str.ProSoundCode", item.SoundId.ToString()));

			List<CharacterRow> rows = [];
			if (item.Armor is { } armor)
			{
				rows.Add(new CharacterRow(Loc.Get("Str.Stat9"), Number(armor.ArmorClass)));
				for (Int32 type = 0; type < armor.DamageResistance.Length; type++)
				{
					rows.Add(new CharacterRow(Loc.Get("Str.Damage" + type), ResistanceText(armor.DamageThreshold[type], armor.DamageResistance[type])));
				}

				rows.Add(Row("Str.ProPerk", Identifier(armor.Perk)));
				rows.Add(Row("Str.ProMalePicture", Identifier(armor.MaleFid)));
				rows.Add(Row("Str.ProFemalePicture", Identifier(armor.FemaleFid)));
			}

			if (item.Container is { } container)
			{
				rows.Add(Row("Str.ProMaxSize", container.MaxSize));
				rows.Add(Row("Str.ProOpenFlags", Hex(container.OpenFlags)));
			}

			if (item.Drug is { } drug)
				rows.AddRange(DrugRows(drug));

			if (item.Weapon is { } weapon)
			{
				rows.Add(Row("Str.ProDamage", $"{Number(weapon.MinDamage)} - {Number(weapon.MaxDamage)}"));
				rows.Add(new CharacterRow(Loc.Get("Str.ProDamageType"), Named("Str.Damage", weapon.DamageType, 7)));
				rows.Add(Row("Str.ProRange", $"{Number(weapon.MaxRange1)} / {Number(weapon.MaxRange2)}"));
				rows.Add(Row("Str.ProMinStrength", weapon.MinStrength));
				rows.Add(Row("Str.ProActionPoints", $"{Number(weapon.ActionPointCost1)} / {Number(weapon.ActionPointCost2)}"));
				rows.Add(Row("Str.ProBurstRounds", weapon.Rounds));
				rows.Add(Row("Str.ProCaliber", weapon.Caliber));
				rows.Add(Row("Str.ProAmmo", Identifier(weapon.AmmoPid)));
				rows.Add(Row("Str.ProCapacity", weapon.AmmoCapacity));
				rows.Add(Row("Str.ProCriticalFailure", weapon.CriticalFailureType));
				rows.Add(Row("Str.ProPerk", Identifier(weapon.Perk)));
				rows.Add(Row("Str.ProAnimation", weapon.AnimationCode));
				rows.Add(Row("Str.ProSoundCode", weapon.SoundCode.ToString()));
			}

			if (item.Ammo is { } ammo)
			{
				rows.Add(Row("Str.ProCaliber", ammo.Caliber));
				rows.Add(Row("Str.ProQuantity", ammo.Quantity));
				rows.Add(Row("Str.ProArmorClassModifier", ammo.ArmorClassModifier));
				rows.Add(Row("Str.ProResistanceModifier", ammo.DamageResistanceModifier));
				rows.Add(Row("Str.ProDamageMultiplier", ammo.DamageMultiplier));
				rows.Add(Row("Str.ProDamageDivisor", ammo.DamageDivisor));
			}

			if (item.MiscData is { } misc)
			{
				rows.Add(Row("Str.ProPowerPid", Identifier(misc.PowerPid)));
				rows.Add(Row("Str.ProPowerType", misc.PowerType));
				rows.Add(Row("Str.ProCharges", misc.Charges));
			}

			if (item.Key is { } key)
				rows.Add(Row("Str.ProKeyCode", Identifier(key.KeyCode)));

			sections.Add(new CharacterSection(Loc.Get("Str.ProItem" + (Int32)item.ItemType), rows));
		}

		private static IEnumerable<CharacterRow> DrugRows(DrugData drug)
		{
			for (Int32 i = 0; i < drug.Stat.Length; i++)
			{
				if (drug.Stat[i] != -1 || drug.Amount[i] != 0)
				{
					yield return new CharacterRow(
						Loc.Format("Str.ProDrugEffect", i + 1),
						Loc.Format("Str.ProDrugEffectFormat", Number(drug.Stat[i]), Number(drug.Amount[i]), Number(drug.Amount1[i]), Number(drug.Duration1), Number(drug.Amount2[i]), Number(drug.Duration2)));
				}
			}

			yield return Row("Str.ProAddictionChance", Number(drug.AddictionChance) + " %");
			yield return Row("Str.ProWithdrawalEffect", Identifier(drug.WithdrawalEffect));
			yield return Row("Str.ProWithdrawalOnset", drug.WithdrawalOnset);
		}

		private static void AddCritter(List<CharacterSection> sections, List<CharacterRow> general, CritterProto critter)
		{
			Int32 Stat(Int32 index) => critter.GetStat((CharacterStat)index);

			general.Add(Row("Str.ProExtendedFlags", Hex(critter.ExtendedFlags)));
			general.Add(Row("Str.ProScript", Identifier(critter.ScriptId)));
			general.Add(Row("Str.ProHeadPicture", Identifier(critter.HeadFid)));
			general.Add(Row("Str.ProAiPacket", critter.AiPacket));
			general.Add(Row("Str.ProTeam", critter.Team));
			general.Add(new CharacterRow(Loc.Get("Str.ProBodyType"), Named("Str.ProBody", critter.BodyType, 3)));
			general.Add(Row("Str.GcdExperience", critter.Experience));
			general.Add(Row("Str.ProKillType", critter.KillType));
			general.Add(new CharacterRow(Loc.Get("Str.ProDamageType"), Named("Str.Damage", critter.DamageType, 7)));
			general.Add(Row("Str.ProFlags", Hex(critter.CritterFlags)));

			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionSpecial"), SpecialRows(Stat)));
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionDerived"), DerivedRows(Stat)));
			sections.Add(new CharacterSection(Loc.Get("Str.GcdSectionResistances"), ResistanceRows(Stat)));

			List<CharacterRow> skills = critter.Skills
				.Select((points, index) => (points, index))
				.Where(skill => skill.points != 0)
				.Select(skill => new CharacterRow(Loc.Get("Str.Skill" + skill.index), Number(skill.points)))
				.ToList();
			if (skills.Count > 0)
				sections.Add(new CharacterSection(Loc.Get("Str.ProSectionSkills"), skills));
		}
	}
}