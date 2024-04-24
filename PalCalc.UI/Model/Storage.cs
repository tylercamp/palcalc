using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.SaveReader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.UI.Model
{
    internal static class Storage
    {
        public static string CachePath => "cache";
        public static string SaveCachePath => $"{CachePath}/saves";

        // path for cached copy of save file data
        public static string SaveCachePathFor(SaveGame forSaveFile) => $"{SaveCachePath}/{CachedSaveGame.IdentifierFor(forSaveFile)}.json";

        // path for storing data associated with a specific save file
        public static string SaveFileDataPath(SaveGame forSaveFile) => $"data/results/{CachedSaveGame.IdentifierFor(forSaveFile)}";

        public static string AppSettingsPath => $"data/settings.json";

        private static bool didInit = false;
        private static void Init()
        {
            if (didInit) return;

            if (!Directory.Exists(CachePath)) Directory.CreateDirectory(CachePath);
            if (!Directory.Exists(SaveCachePath)) Directory.CreateDirectory(SaveCachePath);

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
                    // TODO - log
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

        #region Cached Game Save Files

        private static Dictionary<string, CachedSaveGame> InMemorySaves = new Dictionary<string, CachedSaveGame>();

        // only loads the save if it has been cached, otherwise returns null
        public static CachedSaveGame LoadSaveFromCache(SaveGame save, PalDB db)
        {
            Init();

            var path = SaveCachePathFor(save);
            if (File.Exists(path))
            {
                CachedSaveGame res;
#if HANDLE_ERRORS
                try
                {
#endif
                    res = CachedSaveGame.FromJson(File.ReadAllText(path), db);
#if HANDLE_ERRORS
                }
                catch (Exception e)
                {
                    // TODO - log
                    File.Delete(path);
                    res = null;
                }
#endif
                return res;
            }
            else
            {
                return null;
            }
        }

        // loads the cached save data and updates it if it's outdated or not yet cached
        public static CachedSaveGame LoadSave(SaveGame save, PalDB db)
        {
            Init();

            var path = SaveCachePathFor(save);
            if (!save.IsValid)
            {
                if (File.Exists(path))
                {
                    // TODO - log
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
                if (res == null) return null;

                File.WriteAllText(path, res.ToJson(db));
                InMemorySaves.Add(identifier, res);
                return res;
            }
        }

        #endregion
    }
}
