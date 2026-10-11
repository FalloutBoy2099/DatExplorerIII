namespace DatExplorerIII.Core
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Models;

	public sealed partial class DatArchive
	{
		/// <summary>The folder this archive was made from, until it is saved as a DAT file; null for an archive that is a file.</summary>
		private String? _sourceFolder;

		/// <summary>True while nothing was changed in an archive made from a folder (opening it is not a change to save).</summary>
		private Boolean _folderPristine;

		private readonly List<String> _skippedFiles = new();

		/// <summary>
		/// True for an archive made from a folder: its files are read from the folder, and it can only be saved under another name
		/// (see <see cref="SaveAs"/>).
		/// </summary>
		public Boolean IsFolderBacked => _sourceFolder != null;

		/// <summary>The files and folders of the folder that cannot be stored in a DAT archive (a name that cannot be stored, a file larger than 2 GB, a folder that cannot be read).</summary>
		public IReadOnlyList<String> SkippedFiles => _skippedFiles;

		/// <summary>
		/// Opens a folder like a DAT archive: the files of the folder and of its sub folders are the files of the archive, and
		/// they are read from the disk. Nothing is copied until the archive is saved with <see cref="SaveAs"/>.
		/// </summary>
		/// <param name="folderPath">The folder (for instance one named master.dat that holds the files of a DAT).</param>
		/// <param name="format">The format the archive has when it is saved (it can be changed by <see cref="SaveAs"/>).</param>
		/// <exception cref="DatException">The folder does not exist.</exception>
		public static DatArchive OpenFolder(String folderPath, DatFormat format = DatFormat.Fallout2)
		{
			if (String.IsNullOrWhiteSpace(folderPath))
			{
				throw new DatException(DatErrorKind.InvalidFileName, "The folder name is empty.");
			}

			String full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
			if (!Directory.Exists(full))
			{
				throw new DatException(DatErrorKind.FileNotFound, $"The folder '{full}' does not exist.", full);
			}

			DatArchive archive = new(full, format) { _sourceFolder = full };
			archive.AddFolderContent(full, String.Empty);
			archive._folderPristine = true;
			return archive;
		}

		private void AddFolderContent(String disk, String directory)
		{
			IEnumerable<String> files;
			IEnumerable<String> folders;
			try
			{
				files = Directory.EnumerateFiles(disk).ToList();
				folders = Directory.EnumerateDirectories(disk).ToList();
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				_skippedFiles.Add(directory.Length == 0 ? Path.GetFileName(disk) : directory);
				return;
			}

			foreach (String file in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
			{
				try
				{
					AddFile(file, directory);
				}
				catch (DatException)
				{
					_skippedFiles.Add(directory + Path.GetFileName(file));
				}
			}

			foreach (String folder in folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
			{
				String name = Path.GetFileName(folder);

				// A link to a folder could lead back to where it is: it is not followed.
				if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
				{
					_skippedFiles.Add(directory + name + '\\');
					continue;
				}

				try
				{
					ValidateName(name);
					AddFolder(directory + name);
				}
				catch (DatException)
				{
					_skippedFiles.Add(directory + name + '\\');
					continue;
				}

				AddFolderContent(folder, directory + name + '\\');
			}
		}

		/// <summary>Remembers that the archive was changed: an archive made from a folder then has something to save.</summary>
		private void Touch()
		{
			_folderPristine = false;
		}
	}
}