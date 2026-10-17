namespace DatExplorerIII.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using DatExplorerIII.Core;
	using DatExplorerIII.Models;
	using DatExplorerIII.Services;

	public sealed class TempRealFilesTests
	{
		[Fact]
		public void Probe()
		{
			StringBuilder log = new();
			String scr = @"C:\Games\Arcanum\Arcanum2.dat\scr";
			Int32 ok = 0, failed = 0;
			String? sample = null;
			foreach (String file in Directory.EnumerateFiles(scr, "*.scr", SearchOption.AllDirectories))
			{
				try
				{
					String text = ScrText.Render(File.ReadAllBytes(file));
					ok++;
					if (sample == null && text.Length > 300) { sample = file + "\n" + text.Substring(0, Math.Min(1200, text.Length)); }
				}
				catch (Exception e)
				{
					failed++;
					if (failed <= 5) { log.AppendLine("SCR FAIL " + file + " " + e.Message); }
				}
			}

			log.AppendLine($"SCR ok={ok} failed={failed}");
			log.AppendLine(sample ?? "(no sample)");

			foreach (String dat in Directory.EnumerateFiles(@"D:\Torrent\The Temple of Elemental Evil", "*.dat", SearchOption.AllDirectories))
			{
				try
				{
					using DatArchive a = DatArchive.Open(dat);
					DatVerifyResult v = a.Verify();
					log.AppendLine($"{Path.GetFileName(dat)}: format={a.Format} files={a.Entries.Count} checked={v.Checked} damaged={v.Damaged.Count} issues={a.Issues.Count}");
					foreach (DatEntry e in a.Entries.Take(3)) { log.AppendLine("   " + e.FullPath + " " + e.UnpackedSize + " c=" + e.IsCompressed); }
					foreach (DatEntry d in v.Damaged.Take(3)) { log.AppendLine("   DAMAGED " + d.FullPath); }
				}
				catch (Exception e)
				{
					log.AppendLine($"{Path.GetFileName(dat)}: FAIL {e.GetType().Name} {e.Message}");
				}
			}

			File.WriteAllText(@"D:\Git\Fallout\probe.txt", log.ToString());
		}
	}
}
