namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// The size above which a file is not read for a preview: one limit for all the files, and limits for some extensions (the
	/// movies of the games are large, a text is not). The sizes are in megabytes.
	/// </summary>
	public static class PreviewLimits
	{
		/// <summary>The limit for the files that no extension has a limit for.</summary>
		public const Int32 DefaultMegabytes = 64;

		/// <summary>The smallest and the largest limit: a file is read whole into an array, which cannot hold 2 GB.</summary>
		public const Int32 MinimumMegabytes = 1;
		public const Int32 MaximumMegabytes = 2000;

		private const Int64 Megabyte = 1024L * 1024;

		private static Int32 _global = DefaultMegabytes;
		private static IReadOnlyDictionary<String, Int32> _byExtension = new Dictionary<String, Int32>();

		/// <summary>The limits that new settings start with: the movies are allowed to be large.</summary>
		public static Dictionary<String, Int32> DefaultByExtension() => new(StringComparer.OrdinalIgnoreCase) { [".mve"] = 1536 };

		/// <summary>Sets the limits (in megabytes); the extensions are written with their dot.</summary>
		public static void Set(Int32 globalMegabytes, IEnumerable<KeyValuePair<String, Int32>>? byExtension)
		{
			_global = Clamp(globalMegabytes);
			_byExtension = (byExtension ?? []).Where(pair => FileViewers.Normalize(pair.Key).Length > 1)
				.GroupBy(pair => FileViewers.Normalize(pair.Key))
				.ToDictionary(group => group.Key, group => Clamp(group.Last().Value), StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>The size in bytes above which a file is not previewed: the limit of its extension, else the one for all.</summary>
		public static Int64 For(String fileName)
		{
			Int32 megabytes = _byExtension.TryGetValue(Path.GetExtension(fileName), out Int32 own) ? own : _global;
			return megabytes * Megabyte;
		}

		public static Int32 Clamp(Int32 megabytes) => Math.Clamp(megabytes, MinimumMegabytes, MaximumMegabytes);
	}
}
