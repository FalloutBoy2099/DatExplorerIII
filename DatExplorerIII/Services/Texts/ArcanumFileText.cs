namespace DatExplorerIII.Services.Texts
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Formats.GameFiles;
	using Interface;
	using Models.GameFiles;

	/// <summary>
	/// The small files of Arcanum (jump points, index and data of a saved game, information of a save, fog of a town map) as the text that
	/// the viewer shows, and that the comparison compares.
	/// </summary>
	public static class ArcanumFileText
	{
		/// <summary>Whether a file of this extension (with the dot) is shown as one of these texts.</summary>
		public static Boolean Handles(String extension)
		{
			return extension.ToLowerInvariant() is ".jmp" or ".tfai" or ".tfaf" or ".gsi" or ".tmf" or ".tmi";
		}

		/// <param name="index">For the data of a saved game (.tfaf): the index (.tfai) that describes it, or null when it is not there.</param>
		/// <exception cref="InvalidDataException">The data is not valid.</exception>
		public static String Render(String extension, Byte[] data, Byte[]? index = null)
		{
			return extension.ToLowerInvariant() switch
			{
				".jmp" => RenderJmp(ArcanumSaveReaders.ReadJmp(data)),
				".tfai" => RenderTfai(ArcanumSaveReaders.ReadTfai(data)),
				".tfaf" => RenderTfaf(data, index == null ? null : ArcanumSaveReaders.ReadTfai(index)),
				".gsi" => RenderGsi(ArcanumSaveReaders.ReadGsi(data)),
				".tmf" => RenderTmf(ArcanumSaveReaders.ReadTmf(data)),
				".tmi" => RenderTmi(TmiReader.Read(data)),
				_ => throw new InvalidDataException("This file is not shown as a text."),
			};
		}

		private static String RenderJmp(JmpFile file)
		{
			StringBuilder text = new();
			text.AppendLine(Loc.Format("Str.JmpCount", file.Jumps.Count));
			for (Int32 i = 0; i < file.Jumps.Count; i++)
			{
				JumpPoint jump = file.Jumps[i];
				text.AppendLine(Loc.Format("Str.JmpLine", i + 1, jump.SourceX, jump.SourceY, jump.DestinationMap, jump.DestinationX, jump.DestinationY, jump.Flags));
			}

			return text.ToString();
		}

		private static String RenderTfai(TfaiFile file)
		{
			StringBuilder text = new();
			Int64 files = 0, bytes = 0;
			Append(text, file.Root, 0, ref files, ref bytes);
			text.AppendLine();
			text.AppendLine(Loc.Format("Str.TfaiTotal", files, bytes));
			return text.ToString();
		}

		private static void Append(StringBuilder text, IReadOnlyList<TfaiNode> nodes, Int32 level, ref Int64 files, ref Int64 bytes)
		{
			foreach (TfaiNode node in nodes)
			{
				text.Append(' ', level * 2).Append(node.Name);
				if (node.IsFolder)
				{
					text.AppendLine("/");
					Append(text, node.Children, level + 1, ref files, ref bytes);
				}
				else
				{
					text.Append("  ").AppendLine(Loc.Format("Str.SizeBytes", node.Size));
					files++;
					bytes += node.Size;
				}
			}
		}

		/// <summary>The files of the data, with the place where each starts: the index gives their names and sizes.</summary>
		private static String RenderTfaf(Byte[] data, TfaiFile? index)
		{
			StringBuilder text = new();
			if (index == null)
			{
				text.AppendLine(Loc.Format("Str.TfafNoIndex", data.Length));
				return text.ToString();
			}

			Int64 offset = 0;
			Int32 files = 0;
			List<String> lines = [];
			Collect(index.Root, String.Empty, lines, ref offset, ref files);
			text.AppendLine(Loc.Format("Str.TfafSummary", files, offset, data.Length));
			if (offset != data.Length)
				text.AppendLine(Loc.Get(offset > data.Length ? "Str.TfafTooShort" : "Str.TfafTooLong"));

			text.AppendLine();
			foreach (String line in lines)
			{
				text.AppendLine(line);
			}

			return text.ToString();
		}

		private static void Collect(IReadOnlyList<TfaiNode> nodes, String prefix, List<String> lines, ref Int64 offset, ref Int32 files)
		{
			foreach (TfaiNode node in nodes)
			{
				String path = prefix.Length == 0 ? node.Name : prefix + "/" + node.Name;
				if (node.IsFolder)
				{
					Collect(node.Children, path, lines, ref offset, ref files);
					continue;
				}

				lines.Add(Loc.Format("Str.TfafLine", offset.ToString("X8", CultureInfo.InvariantCulture), node.Size, path));
				offset += node.Size;
				files++;
			}
		}

		private static String RenderGsi(GsiFile file)
		{
			StringBuilder text = new();
			Line(text, "Str.GsiDisplay", file.DisplayName);
			Line(text, "Str.GsiModule", file.ModuleName);
			Line(text, "Str.GsiLeader", file.LeaderName);
			Line(text, "Str.GsiLevel", file.LeaderLevel.ToString(CultureInfo.InvariantCulture));
			Line(text, "Str.GsiMap", file.MapId.ToString(CultureInfo.InvariantCulture));
			Line(text, "Str.GsiPosition", file.LeaderTileX.ToString(CultureInfo.InvariantCulture) + ", " + file.LeaderTileY.ToString(CultureInfo.InvariantCulture));
			TimeSpan time = TimeSpan.FromMilliseconds(file.GameTimeMs);
			Line(text, "Str.GsiTime", Loc.Format("Str.GsiTimeValue", file.GameTimeDays, time.Hours, time.Minutes));
			Line(text, "Str.GsiPortrait", file.LeaderPortrait.ToString(CultureInfo.InvariantCulture));
			Line(text, "Str.GsiStory", file.StoryState.ToString(CultureInfo.InvariantCulture));
			Line(text, "Str.GsiVersion", file.Version.ToString(CultureInfo.InvariantCulture));
			return text.ToString();
		}

		private static void Line(StringBuilder text, String labelKey, String value)
		{
			text.Append(Loc.Get(labelKey)).Append(" ").AppendLine(value);
		}

		private static String RenderTmf(TmfFile file)
		{
			Double percent = file.TotalTiles == 0 ? 0 : 100.0 * file.RevealedTiles / file.TotalTiles;
			return Loc.Format("Str.TmfSummary", file.TotalTiles, file.RevealedTiles, percent.ToString("0.#", CultureInfo.CurrentCulture)) + Environment.NewLine;
		}

		private static String RenderTmi(TmiFile file)
		{
			StringBuilder text = new();
			text.AppendLine(Loc.Format("Str.TmiSummary", file.MapId, file.Version, file.PictureWidth, file.PictureHeight, file.TilesWidth, file.TilesHeight, file.Scale.ToString("0.###", CultureInfo.CurrentCulture)));
			text.AppendLine(Loc.Format("Str.TmiOther", file.Flag, file.OffsetX, file.OffsetY, file.Unknown));
			return text.ToString();
		}
	}
}
