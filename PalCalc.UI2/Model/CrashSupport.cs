using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using PalCalc.UI2.Model.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI2.Model
{
    internal static class CrashSupport
    {
        private const int MAX_HISTORY_LENGTH = 2;

        private static void Trim<T>(List<T> collection)
        {
            while (collection.Count > MAX_HISTORY_LENGTH) collection.RemoveAt(0);
        }

        private static List<ISaveGame> LoadedSaveHistory = new List<ISaveGame>();
        private static List<SaveGameDetails> ReferencedCachedSaveHistory = new List<SaveGameDetails>();

        public static void ReferencedSave(ISaveGame save)
        {
            if (save == null) return;

            if (LoadedSaveHistory.LastOrDefault() != save)
            {
                LoadedSaveHistory.Add(save);
                Trim(LoadedSaveHistory);
            }
        }

        public static void ReferencedCachedSave(SaveGameDetails cached)
        {
            if (cached == null) return;

            if (ReferencedCachedSaveHistory.LastOrDefault() != cached)
            {
                ReferencedCachedSaveHistory.Add(cached);
                Trim(ReferencedCachedSaveHistory);
            }
        }

        private const int KB = 1000;
        private const int MB = KB * 1000;

        private static bool IsSmallDirectory(string targetPath)
        {
            long totalSize = 0;
            int count = 0;

            foreach (var path in Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    totalSize += new FileInfo(path).Length;
                    count++;
                } catch { }

                if (count > 1000 || totalSize > 100 * MB) return false;
            }

            return true;
        }

        public static string PrepareSupportFile(ISaveGame specificSave = null)
        {
            var outputPath = Path.GetFullPath("CRASHLOG.zip");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (var outStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
            {
                StorageManager.Data.AttachSupportFiles(archive);

                var saves = specificSave != null ? [specificSave] : LoadedSaveHistory;

                for (int i = 0; i < saves.Count; i++)
                {
                    try
                    {
                        var save = saves[i];
                        if (save == null) continue;

                        if (IsSmallDirectory(save.BasePath))
                        {
                            // try to get the whole save folder (if it's a reasonable size)
                            foreach (var f in Directory.EnumerateFiles(save.BasePath, "*", SearchOption.AllDirectories))
                            {
                                var relpath = f.Substring(save.BasePath.Length + 1).NormalizedPath();
                                if (relpath.Contains("/backup/")) continue;

                                try
                                {
                                    archive.CreateEntryFromFile(f, $"save-{i}/{relpath}");
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            void AddSaveFile(ISaveFile file)
                            {
                                try
                                {
                                    if (file.Exists) archive.CreateEntryFromFile(file.FilePath, $"save-{i}/{Path.GetFileName(file.FilePath)}");
                                }
                                catch { }
                            }

                            AddSaveFile(save.Level);
                            AddSaveFile(save.LevelMeta);
                            AddSaveFile(save.LocalData);
                            AddSaveFile(save.WorldOption);

                            foreach (var p in save.Players.Where(p => p.Exists))
                                archive.CreateEntryFromFile(p.FilePath, $"save-{i}/Players/{Path.GetFileName(p.FilePath)}");
                        }
                    }
                    catch { }
                }

                for (int i = 0; i < ReferencedCachedSaveHistory.Count; i++)
                {
                    try
                    {
                        var cached = ReferencedCachedSaveHistory[i];
                        if (cached?.UnderlyingSave == null) continue;

                        StorageManager.Cache.AttachSupportFiles(archive, $"save-cache-{i}.json", cached.UnderlyingSave);
                    }
                    catch { }
                }

                foreach (var path in Directory.EnumerateFiles(App.LogFolder).OrderByDescending(File.GetLastWriteTime).Take(MAX_HISTORY_LENGTH))
                {
                    try
                    {
                        archive.CreateEntryFromFile(path, "logs/" + Path.GetFileName(path));
                    }
                    catch { }
                }
            }

            return outputPath;
        }
    }
}
