using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal static class Storage
    {
        private static ILogger logger = Log.ForContext(typeof(Storage));

        public static string CachePath => "cache";
        public static string SaveCachePath => $"{CachePath}/saves";
        public static string DataPath => "data";

        public static string AppSettingsPath
        {
            get
            {
                Init();
                return $"{DataPath}/settings.json";
            }
        }

        // path for cached copy of save file data
        public static string SaveCachePathFor(ISaveGame forSaveFile)
        {
            Init();
            return $"{SaveCachePath}/{CachedSaveGame.IdentifierFor(forSaveFile)}.json";
        }

        // path for storing data associated with a specific save file
        public static string SaveFileDataPath(ISaveGame forSaveFile)
        {
            Init();
            var path = $"{DataPath}/{CachedSaveGame.IdentifierFor(forSaveFile)}";
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        // path for storing game-specific game settings (breeding time, etc.)
        public static string GameSettingsPath(ISaveGame forSaveFile)
        {
            Init();
            return SaveFileDataPath(forSaveFile) + "/game-settings.json";
        }

        private static bool didInit = false;
        public static void Init()
        {
            if (didInit) return;

            if (!Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
            if (!Directory.Exists(SaveCachePath)) Directory.CreateDirectory(SaveCachePath);
            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);

            // migrate file locations from before beta-v0.5
            if (Directory.Exists($"{DataPath}/results"))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries($"{DataPath}/results"))
                {
                    var newPath = $"{DataPath}/{Path.GetFileName(entry)}";
                    if (File.GetAttributes(entry).HasFlag(FileAttributes.Directory))
                    {
                        Directory.Move(entry, newPath);
                    }
                    else
                    {
                        File.Move(entry, newPath);
                    }
                }

                Directory.Delete($"{DataPath}/results");
            }

            didInit = true;
        }

        public static AppSettings LoadAppSettings()
        {
            if (File.Exists(AppSettingsPath))
            {
                try
                {
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(AppSettingsPath));
                }
                catch (Exception e)
                {
                    logger.Error(e, "error reading app settings files, resetting");

                    File.Delete(AppSettingsPath);
                    return LoadAppSettings();
                }
            }
            else
            {
                return new AppSettings();
            }
        }

        public static void SaveAppSettings(AppSettings settings) => File.WriteAllText(AppSettingsPath, JsonConvert.SerializeObject(settings));

        public static void ClearForSave(ISaveGame save)
        {
            var cachePath = SaveCachePathFor(save);
            if (File.Exists(cachePath)) File.Delete(cachePath);

            var dataPath = SaveFileDataPath(save);
            if (Directory.Exists(dataPath))
                Directory.Delete(dataPath, true);
        }

        #region Cached Game Save Files

        private static Dictionary<string, CachedSaveGame> InMemorySaves = new Dictionary<string, CachedSaveGame>();

        // only loads the save if it has been cached, otherwise returns null
        public static CachedSaveGame LoadSaveFromCache(ISaveGame save, PalDB db)
        {
            Init();

            CrashSupport.ReferencedSave(save);

            var path = SaveCachePathFor(save);
            if (File.Exists(path))
            {
                CachedSaveGame res;
#if HANDLE_ERRORS
                try
                {
#endif
                    res = CachedSaveGame.FromJson(File.ReadAllText(path), db);
                    res.UnderlyingSave = save;
#if HANDLE_ERRORS
                }
                catch (Exception e)
                {
                    logger.Error(e, "failed to load cached save-game data, clearing");

                    File.Delete(path);
                    res = null;
                }
#endif
                CrashSupport.ReferencedCachedSave(res);
                return res;
            }
            else
            {
                return null;
            }
        }

        // loads the cached save data and updates it if it's outdated or not yet cached
        public static CachedSaveGame LoadSave(ISaveGame save, PalDB db)
        {
            Init();

            CrashSupport.ReferencedSave(save);

            var path = SaveCachePathFor(save);
            if (!save.IsValid)
            {
                if (File.Exists(path))
                {
                    logger.Warning("cached save available but the save-game itself is invalid, deleting cached save for {savePath}", save.BasePath);
                    File.Delete(path);
                }
                return null;
            }

            var identifier = CachedSaveGame.IdentifierFor(save);
            if (InMemorySaves.ContainsKey(identifier)) return InMemorySaves[identifier];

            if (File.Exists(path))
            {
                var res = LoadSaveFromCache(save, db);

                if (!res.IsValid)
                {
                    // TODO - no longer necessary? should have been covered by check at top of this method
                    // TODO - log
                    File.Delete(path);
                    return null;
                }

                if (res.IsOutdated)
                {
                    File.Delete(path);
                    return LoadSave(save, db);
                }

                InMemorySaves.Add(identifier, res);
                return res;
            }
            else
            {
                var res = CachedSaveGame.FromSaveGame(save, db);
                if (res != null)
                {
                    CrashSupport.ReferencedCachedSave(res);
                    File.WriteAllText(path, res.ToJson(db));
                }

                // TODO - adding `null` entries will prevent re-adding a save at the same path until the app is restarted
                InMemorySaves.Add(identifier, res);
                return res;
            }
        }

        #endregion
    }
}
