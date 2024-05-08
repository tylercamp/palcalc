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

        public static string PrepareSupportFile(ISaveGame specificSave = null)
        {
            var outputPath = Path.GetFullPath("CRASHLOG.zip");
            if (File.Exists(outputPath)) File.Delete(outputPath);

            using (var outStream = new FileStream(outputPath, FileMode.Create))
            using (var archive = new ZipArchive(outStream, ZipArchiveMode.Create, true))
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
                        var save = LoadedSaveHistory[i];
                        if (save == null) continue;

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
