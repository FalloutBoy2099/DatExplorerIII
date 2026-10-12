namespace DatExplorerIII.Services
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;
	using Microsoft.Win32.SafeHandles;

	/// <summary>
	/// Gives a program that has no console window (it is built for windows) a way to write to the console it was started from.
	/// </summary>
	public static class ConsoleHost
	{
		private const Int32 AttachParentProcess = -1;
		private const Int32 StandardOutput = -11;
		private const UInt32 GenericWrite = 0x40000000;
		private const UInt32 FileShareWrite = 0x2;
		private const UInt32 OpenExisting = 3;

		/// <summary>
		/// A writer to the standard output when it goes somewhere (a file, a pipe), else to the console of the parent program.
		/// Null when there is neither (the program was started by a double click).
		/// </summary>
		public static TextWriter? OpenOutput()
		{
			Boolean attached = AttachConsole(AttachParentProcess);
			Stream? stream = null;

			IntPtr standard = GetStdHandle(StandardOutput);
			if (standard != IntPtr.Zero && standard != new IntPtr(-1))
			{
				stream = Console.OpenStandardOutput();
			}
			else if (attached)
			{
				SafeFileHandle handle = CreateFile("CONOUT$", GenericWrite, FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
				if (!handle.IsInvalid)
				{
					stream = new FileStream(handle, FileAccess.Write);
				}
			}

			return stream == null ? null : new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
		}

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern Boolean AttachConsole(Int32 processId);

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetStdHandle(Int32 standardHandle);

		[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern SafeFileHandle CreateFile(String fileName, UInt32 desiredAccess, UInt32 shareMode, IntPtr securityAttributes, UInt32 creationDisposition, UInt32 flagsAndAttributes, IntPtr templateFile);
	}
}