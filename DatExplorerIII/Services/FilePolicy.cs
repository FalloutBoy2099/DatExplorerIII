namespace DatExplorerIII.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Which files are never started from an archive: a double click must not run a program that came out of a DAT.
	/// </summary>
	public static class FilePolicy
	{
		private static readonly HashSet<String> Blocked = BuildBlockedExtensions();

		/// <summary>True for programs, scripts, shortcuts and other files that Windows would run or interpret.</summary>
		public static Boolean IsDangerous(String fileName)
		{
			String extension = Path.GetExtension(fileName);
			return extension.Length > 0 && Blocked.Contains(extension);
		}

		private static HashSet<String> BuildBlockedExtensions()
		{
			HashSet<String> extensions = new(StringComparer.OrdinalIgnoreCase)
			{
				".exe", ".com", ".bat", ".cmd", ".scr", ".pif", ".dll", ".sys", ".drv", ".ocx", ".cpl", ".msc",
				".msi", ".msp", ".mst", ".appx", ".msix", ".appxbundle", ".msixbundle", ".application", ".appref-ms", ".gadget",
				".ps1", ".ps1xml", ".psm1", ".psd1", ".vbs", ".vbe", ".vb", ".js", ".jse", ".wsf", ".wsh", ".hta", ".jar",
				".lnk", ".url", ".scf", ".inf", ".reg", ".chm", ".xll", ".docm", ".xlsm", ".pptm", ".iqy",
			};

			// The extensions Windows itself runs as programs.
			String? pathExt = Environment.GetEnvironmentVariable("PATHEXT");
			if (pathExt != null)
			{
				foreach (String extension in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(item => item.Trim()))
				{
					extensions.Add(extension);
				}
			}

			return extensions;
		}
	}
}