namespace DatExplorerIII.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using Core;
	using Models.Archives;
	using Services.Interface;

	/// <summary>
	/// Decides which files are imported and where they go.
	/// </summary>
	internal static class ImportPlanner
	{
		/// <summary>
		/// Lists the files of the given files and folders.
		/// </summary>
		/// <param name="paths">Files and folders chosen by the user.</param>
		/// <param name="mergeFolderContents">True to import the content of a folder, false to import the folder itself.</param>
		public static List<ImportItem> Collect(IReadOnlyList<String> paths, Boolean mergeFolderContents, CancellationToken token)
		{
			List<ImportItem> items = [];
			EnumerationOptions options = new()
			{
				RecurseSubdirectories = true,
				IgnoreInaccessible = true,
				AttributesToSkip = FileAttributes.ReparsePoint,
			};

			foreach (String path in paths)
			{
				token.ThrowIfCancellationRequested();

				if (Directory.Exists(path))
				{
					String folder = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
					String baseFolder = mergeFolderContents ? folder : Path.GetDirectoryName(folder) ?? folder;

					foreach (String file in Directory.EnumerateFiles(folder, "*", options))
					{
						token.ThrowIfCancellationRequested();
						String relative = Path.GetRelativePath(baseFolder, Path.GetDirectoryName(file)!);
						items.Add(new ImportItem(file, relative == "." ? String.Empty : relative));
					}
				}
				else if (File.Exists(path))
				{
					items.Add(new ImportItem(path, String.Empty));
				}
			}

			return items;
		}

		/// <summary>
		/// Decides where each file goes and what to do about the ones that already exist.
		/// </summary>
		/// <param name="replaced">The files of the archive that the import replaces.</param>
		/// <returns>The steps to run, or null when the user cancelled.</returns>
		public static List<ImportStep>? Plan(DatArchive archive, String targetPath, IReadOnlyList<ImportItem> items, ConflictResolver resolver, out List<DatEntry> replaced)
		{
			List<ImportStep> steps = [with(items.Count)];
			replaced = [];

			foreach (ImportItem item in items)
			{
				String directory = DatArchive.NormalizeDirectory(targetPath + item.RelativeDirectory);
				String name = Path.GetFileName(item.SourceFile);

				DatEntry? existing = archive.Find(directory + name);
				if (existing != null)
				{
					switch (resolver.Resolve(Loc.Format("Str.ReplaceEntry", existing.FullPath)))
					{
						case ConflictDecision.Cancel:
							return null;
						case ConflictDecision.Skip:
							continue;
						default:
							replaced.Add(existing);
							break;
					}
				}

				steps.Add(new ImportStep(item.SourceFile, directory, name));
			}

			return steps;
		}
	}
}