namespace DatExplorerIII.Services.Platform
{
	using System;
	using System.ComponentModel;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using Microsoft.Win32;
	using Services.Interface;

	/// <summary>
	/// Associates .dat files (and, separately, the .patch0 to .patch9 files of Arcanum) with the application for the current user
	/// (no administrator rights needed).
	/// </summary>
	public static class FileAssociationService
	{
		private const String Extension = ".dat";
		private const String ProgramId = "DatExplorerIII.DatFile";
		private const String PatchProgramId = "DatExplorerIII.PatchFile";
		private const String ClassesRoot = @"Software\Classes\";
		private const Int32 ShellChangeAssociations = 0x08000000;

		/// <summary>The extensions of the patches of Arcanum: .patch0 to .patch9.</summary>
		public static IReadOnlyList<String> PatchExtensions { get; } = Enumerable.Range(0, 10).Select(number => ".patch" + number).ToArray();

		public static Boolean IsAssociated => IsRegistered(Extension, ProgramId);

		/// <summary>True when every extension from .patch0 to .patch9 opens with this program.</summary>
		public static Boolean ArePatchesAssociated => PatchExtensions.All(extension => IsRegistered(extension, PatchProgramId));

		private static Boolean IsRegistered(String extension, String programId)
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(ClassesRoot + extension);
			return String.Equals(key?.GetValue(String.Empty) as String, programId, StringComparison.Ordinal);
		}

		/// <summary>
		/// True when the user chose another program for .dat files in Windows: that choice wins over the registration made here.
		/// </summary>
		public static Boolean IsOverriddenByUserChoice => PickedAnotherProgram(Extension, ProgramId);

		/// <summary>True when the user chose another program in Windows for at least one of the .patch0 to .patch9 files.</summary>
		public static Boolean ArePatchesOverriddenByUserChoice => PatchExtensions.Any(extension => PickedAnotherProgram(extension, PatchProgramId));

		private static Boolean PickedAnotherProgram(String extension, String programId)
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\" + extension + @"\UserChoice");
			String? chosen = key?.GetValue("ProgId") as String;
			return chosen != null && !String.Equals(chosen, programId, StringComparison.Ordinal);
		}

		/// <summary>Opens the default apps page of the Windows settings, where the program for .dat files can be chosen.</summary>
		public static void OpenDefaultAppsSettings()
		{
			try
			{
				Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });
			}
			catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
			{
				// The settings page is only a help: nothing to report when it cannot be opened.
			}
		}

		public static void Associate()
		{
			Register([Extension], ProgramId, Loc.Get("Str.DatFileType"));
		}

		/// <summary>Associates the .patch0 to .patch9 files of Arcanum, which are archives like the .dat files.</summary>
		public static void AssociatePatches()
		{
			Register(PatchExtensions, PatchProgramId, Loc.Get("Str.PatchFileType"));
		}

		private static void Register(IEnumerable<String> extensions, String programId, String typeName)
		{
			String executable = Environment.ProcessPath ?? throw new InvalidOperationException(Loc.Get("Str.NoExecutable"));
			String? icon = ExtractIcon();

			foreach (String name in extensions)
			{
				using RegistryKey extension = Registry.CurrentUser.CreateSubKey(ClassesRoot + name);
				extension.SetValue(String.Empty, programId);
				using RegistryKey progIds = extension.CreateSubKey("OpenWithProgids");
				progIds.SetValue(programId, Array.Empty<Byte>(), RegistryValueKind.None);
			}

			using (RegistryKey program = Registry.CurrentUser.CreateSubKey(ClassesRoot + programId))
			{
				program.SetValue(String.Empty, typeName);
				program.SetValue("AlwaysShowExt", String.Empty);

				using RegistryKey defaultIcon = program.CreateSubKey("DefaultIcon");
				defaultIcon.SetValue(String.Empty, icon ?? executable + ",0");

				using RegistryKey command = program.CreateSubKey(@"shell\open\command");
				command.SetValue(String.Empty, $"\"{executable}\" \"%1\"");
			}

			SHChangeNotify(ShellChangeAssociations, 0, IntPtr.Zero, IntPtr.Zero);
		}

		/// <summary>
		/// Windows needs the icon of a file type as a file: the one inside the program is written to the application data folder.
		/// </summary>
		private static String? ExtractIcon()
		{
			try
			{
				using Stream? resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("DatExplorerIII.dat.ico");
				if (resource == null)
					return null;

				String folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DatExplorerIII");
				Directory.CreateDirectory(folder);

				String path = Path.Combine(folder, "dat.ico");
				using FileStream file = File.Create(path);
				resource.CopyTo(file);
				return path;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				return null;
			}
		}

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("shell32.dll")]
		private static extern void SHChangeNotify(Int32 eventId, UInt32 flags, IntPtr item1, IntPtr item2);
	}
}