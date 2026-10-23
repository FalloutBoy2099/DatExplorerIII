namespace DatExplorerIII.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>The source files of the program, for the tests that check what is written in them (the XAML, the texts).</summary>
	internal static class SourceFiles
	{
		/// <summary>The folder of the program project (DatExplorerIII/DatExplorerIII), found above the folder the tests run from.</summary>
		public static String ProjectFolder
		{
			get
			{
				String? folder = AppContext.BaseDirectory;
				while (folder != null)
				{
					String candidate = Path.Combine(folder, "DatExplorerIII", "DatExplorerIII.csproj");
					if (File.Exists(candidate))
						return Path.GetDirectoryName(candidate)!;

					folder = Path.GetDirectoryName(folder);
				}

				throw new FileNotFoundException("The project of the program was not found above " + AppContext.BaseDirectory);
			}
		}

		public static String Read(params String[] relativePath)
		{
			return File.ReadAllText(Path.Combine(new[] { ProjectFolder }.Concat(relativePath).ToArray()));
		}

		/// <summary>The files of the project with the extension, without the build output.</summary>
		public static IEnumerable<String> Files(String extension)
		{
			String separator = Path.DirectorySeparatorChar.ToString();
			return Directory.EnumerateFiles(ProjectFolder, "*" + extension, SearchOption.AllDirectories)
				.Where(path => !path.Contains(separator + "obj" + separator) && !path.Contains(separator + "bin" + separator));
		}
	}
}