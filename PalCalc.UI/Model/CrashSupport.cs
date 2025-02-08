using PalCalc.SaveReader;
using PalCalc.SaveReader.SaveFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal static class CrashSupport
    {
        private const int MAX_HISTORY_LENGTH = 2;

        private static void Trim<T>(List<T> collection)
        {
            while (collection.Count > MAX_HISTORY_LENGTH) collection.RemoveAt(0);
        }

        private static List<ISaveGame> LoadedSaveHistory = new List<ISaveGame>();
        private static List<CachedSaveGame> ReferencedCachedSaveHistory = new List<CachedSaveGame>();

        public static void ReferencedSave(ISaveGame save)
        {
            if (save == null) return;

            if (LoadedSaveHistory.LastOrDefault() != save)
            {
                LoadedSaveHistory.Add(save);
                Trim(LoadedSaveHistory);
            }
        }

        public static void ReferencedCachedSave(CachedSaveGame cached)
        {
            if (cached == null) return;

            if (ReferencedCachedSaveHistory.LastOrDefault() != cached)
            {
                ReferencedCachedSaveHistory.Add(cached);
                Trim(ReferencedCachedSaveHistory);
            }
        }

        public static void RemoveReferences(ISaveGame save)
        {
            LoadedSaveHistory.Remove(save);
            ReferencedCachedSaveHistory.RemoveAll(csg => csg.UnderlyingSave == save);
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

        public static string PrepareSupportFile(string outputPath = null, ISaveGame specificSave = null)
        {
            outputPath ??= Path.GetFullPath("CRASHLOG.zip");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (var outStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create))
            {
                try
                {
                    var basePath = Path.GetFullPath(Storage.DataPath);
                    foreach (var f in Directory.EnumerateFiles(Storage.DataPath, "*", SearchOption.AllDirectories).Select(Path.GetFullPath))
                    {
                        try
                        {
                            archive.CreateEntryFromFile(f, f.Replace(basePath, "").TrimStart('/', '\\'));
                        }
                        catch { }
                    }
                }
                catch { }

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
                                if (relpath.Contains("backup/")) continue;

                                try
                                {
                                    archive.CreateEntryFromFile(f, $"save-{i}/{relpath}");
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            void AddSaveFile(ISaveFile file, string nameBase)
                            {
                                try
                                {
                                    if (file.Exists)
                                    {
                                        var filePaths = file.FilePaths.ToArray();
                                        if (filePaths.Length == 1)
                                        {
                                            archive.CreateEntryFromFile(filePaths[0], $"save-{i}/{nameBase}.sav");
                                        }
                                        else
                                        {
                                            for (int fi = 0; fi <  filePaths.Length; fi++)
                                            {
                                                if (File.Exists(filePaths[fi]))
                                                    archive.CreateEntryFromFile(filePaths[fi], $"save-{i}/{nameBase}-{fi}.sav");
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }

                            AddSaveFile(save.Level, "Level");
                            AddSaveFile(save.LevelMeta, "LevelMeta");
                            AddSaveFile(save.LocalData, "LocalData");
                            AddSaveFile(save.WorldOption, "WorldOption");

                            foreach (var p in save.Players.Where(p => p.Exists))
                                AddSaveFile(p, $"Players/{Path.GetFileName(p.FilePaths.First())}");
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

                        var filePath = Storage.SaveCachePathFor(cached.UnderlyingSave);
                        if (File.Exists(filePath)) archive.CreateEntryFromFile(filePath, $"save-cache-{i}.json");
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
