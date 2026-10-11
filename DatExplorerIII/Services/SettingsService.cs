namespace DatExplorerIII.Services
{
	using System;
	using System.IO;
	using System.Reflection;
	using System.Text.Json;
	using Models;

	/// <summary>
	/// User preferences, stored as JSON in the roaming application data folder.
	/// </summary>
	public sealed class SettingsService
	{
		private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

		private readonly String _path;

		/// <summary>The name of the folder of the settings when the program was called DatExplorerMk2.</summary>
		private const String LegacyFolderName = "DatExplorerMk2";

		public SettingsService()
			: this(
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DatExplorerIII"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyFolderName))
		{
		}

		/// <param name="folder">Where the settings are kept.</param>
		/// <param name="legacyFolder">Where the settings of the former name of the program are; they are copied the first time, when there are none in <paramref name="folder"/>.</param>
		public SettingsService(String folder, String? legacyFolder)
		{
			_path = Path.Combine(folder, "settings.json");
			if (legacyFolder != null)
			{
				CopyLegacySettings(Path.Combine(legacyFolder, "settings.json"));
			}

			Data = Load();
		}

		private void CopyLegacySettings(String legacyPath)
		{
			try
			{
				if (!File.Exists(_path) && File.Exists(legacyPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
					File.Copy(legacyPath, _path);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The preferences are a convenience: the defaults are used.
			}
		}

		public SettingsData Data { get; }

		public void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

				// Written aside then put in place: a crash or a full disk in the middle leaves the previous settings, not half a file.
				String temporary = _path + ".tmp";
				File.WriteAllText(temporary, JsonSerializer.Serialize(Data, JsonOptions));
				File.Move(temporary, _path, true);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// Preferences are a convenience: losing them must not disturb the user.
			}
		}

		private SettingsData Load()
		{
			try
			{
				if (File.Exists(_path))
				{
					return Parse(File.ReadAllText(_path));
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The file cannot be read: the defaults are used.
			}

			return new SettingsData();
		}

		/// <summary>
		/// Reads settings from JSON, one setting at a time: a setting that is not valid (a wrong type, a name of an option that does
		/// not exist, <c>null</c> where a value is needed) keeps its default, and the others are kept. What is not JSON, or not an
		/// object, gives the defaults. The numbers are brought back between their limits.
		/// </summary>
		public static SettingsData Parse(String json)
		{
			SettingsData data = new();
			try
			{
				using JsonDocument document = JsonDocument.Parse(json);
				if (document.RootElement.ValueKind != JsonValueKind.Object)
				{
					return data;
				}

				NullabilityInfoContext nullability = new();
				foreach (PropertyInfo property in typeof(SettingsData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
				{
					if (!property.CanWrite || !document.RootElement.TryGetProperty(property.Name, out JsonElement value))
					{
						continue;
					}

					try
					{
						Object? parsed = value.Deserialize(property.PropertyType, JsonOptions);
						Boolean allowsNull = Nullable.GetUnderlyingType(property.PropertyType) != null || nullability.Create(property).WriteState == NullabilityState.Nullable;
						if (parsed != null || allowsNull)
						{
							property.SetValue(data, parsed);
						}
					}
					catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException or FormatException)
					{
						// This setting is not valid: it keeps its default.
					}
				}
			}
			catch (JsonException)
			{
				return new SettingsData();
			}

			Normalize(data);
			return data;
		}

		/// <summary>Brings back what a hand-made file can get wrong: empty entries in the lists, numbers out of their limits.</summary>
		private static void Normalize(SettingsData data)
		{
			data.RecentFiles.RemoveAll(path => String.IsNullOrWhiteSpace(path));
			data.ScrollBarWidth = ScrollBarMetrics.Clamp(data.ScrollBarWidth, ScrollBarMetrics.MinimumWidth, ScrollBarMetrics.MaximumWidth);
			data.ScrollBarMinimumThumb = ScrollBarMetrics.Clamp(data.ScrollBarMinimumThumb, ScrollBarMetrics.MinimumThumbLength, ScrollBarMetrics.MaximumThumbLength);
		}
	}
}