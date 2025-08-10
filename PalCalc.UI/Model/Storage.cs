﻿using Newtonsoft.Json;
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

        public static event Action<ISaveGame> SaveReloaded;

        // (debug-only setting)
        public static readonly bool DEBUG_DisableStorage = false;

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

        public static string SaveFileTargetsDataPath(ISaveGame forSaveFile) => Path.Join(SaveFileDataPath(forSaveFile), "targets");

        // path for storing game-specific game settings (breeding time, etc.)
        public static string GameSettingsPath(ISaveGame forSaveFile)
        {
            Init();
            return SaveFileDataPath(forSaveFile) + "/game-settings.json";
        }

        public static string CustomContainerPath(ISaveGame forSaveFile)
        {
            Init();
            return SaveFileDataPath(forSaveFile) + "/custom-containers.json";
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
            if (DEBUG_DisableStorage) return new();

            if (File.Exists(AppSettingsPath))
            {
                try
                {
                    var res = JsonConvert.DeserializeObject<AppSettings>(
                        File.ReadAllText(AppSettingsPath),
                        // `res.SolverSettings.BannedWildPalInternalNames` has a non-empty-list default value, base Newtonsoft JSON
                        // behavior is to *MERGE* the deserialized list with the default value, leading to a bunch of duplicates.
                        new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }
                    ) ?? new();

                    // remove duplicates caused by missing `ObjectCreationHandling` in older versions
                    res.SolverSettings.BannedBredPalInternalNames = res.SolverSettings.BannedBredPalInternalNames.Distinct().ToList();
                    res.SolverSettings.BannedWildPalInternalNames = res.SolverSettings.BannedWildPalInternalNames.Distinct().ToList();

                    return res;
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
                return new();
            }
        }

        public static void SaveAppSettings(AppSettings settings) => File.WriteAllText(AppSettingsPath, JsonConvert.SerializeObject(settings));

        public static void ClearForSave(ISaveGame save)
        {
            try
            {
                var cachePath = SaveCachePathFor(save);
                if (File.Exists(cachePath)) File.Delete(cachePath);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Unable to delete cache-file for {saveId}", save.GameId);
            }

            try
            {
                var dataPath = SaveFileDataPath(save);
                if (Directory.Exists(dataPath))
                    Directory.Delete(dataPath, true);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "Unable to delete data-folder for {saveId}", save.GameId);
            }
        }

        public static SaveCustomizations LoadSaveCustomizations(ISaveGame forSaveGame, PalDB db)
        {
            if (DEBUG_DisableStorage) return new SaveCustomizations();

            var filePath = CustomContainerPath(forSaveGame);
            if (!File.Exists(filePath)) return new SaveCustomizations();

            SaveCustomizations res = PCDebug.HandleErrors(
                action: () => JsonConvert.DeserializeObject<SaveCustomizations>(File.ReadAllText(filePath), new PalInstanceJsonConverter(db)),
                handleErr: (re) =>
                {
                    logger.Warning(re, "failed to load save customizations for {label}, clearing", CachedSaveGame.IdentifierFor(forSaveGame));
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception fe)
                    {
                        logger.Warning(fe, "failed to delete customizations file");
                    }
                    return null;
                }
            );

            res ??= new SaveCustomizations();
            res.CustomContainers ??= [];
            return res;
        }

        public static void SaveCustomizations(ISaveGame forSaveGame, SaveCustomizations custom, PalDB db)
        {
            if (DEBUG_DisableStorage) return;

            File.WriteAllText(
                CustomContainerPath(forSaveGame),
                JsonConvert.SerializeObject(custom, new PalInstanceJsonConverter(db))
            );
        }

        #region Cached Game Save Files

        private static Dictionary<string, CachedSaveGame> InMemorySaves = new Dictionary<string, CachedSaveGame>();

        // only loads the save if it has been cached, otherwise returns null
        public static CachedSaveGame LoadSaveFromCache(ISaveGame save, PalDB db)
        {
            Init();

            CrashSupport.ReferencedSave(save);

            if (DEBUG_DisableStorage) return null;

            var path = SaveCachePathFor(save);
            if (File.Exists(path))
            {
                CachedSaveGame res = PCDebug.HandleErrors(
                    action: () =>
                    {
                        var csg = CachedSaveGame.FromJson(File.ReadAllText(path), db);
                        csg.UnderlyingSave = save;
                        return csg;
                    },
                    handleErr: (ex) =>
                    {
                        logger.Error(ex, "failed to load cached save-game data, clearing");

                        File.Delete(path);
                        return null;
                    }
                );

                CrashSupport.ReferencedCachedSave(res);
                return res;
            }
            else
            {
                return null;
            }
        }

        // loads the cached save data and updates it if it's outdated or not yet cached
        public static CachedSaveGame LoadSave(ISavesLocation containerLocation, ISaveGame save, PalDB db, GameSettings settings)
        {
            Init();

            CrashSupport.ReferencedSave(save);

            var path = SaveCachePathFor(save);
            if (!save.IsValid)
            {
                if (!DEBUG_DisableStorage && File.Exists(path))
                {
                    logger.Warning("cached save available but the save-game itself is invalid, deleting cached save for {savePath}", save.BasePath);
                    File.Delete(path);
                }
                return null;
            }

            var identifier = CachedSaveGame.IdentifierFor(save);

            lock (InMemorySaves)
            {
                if (InMemorySaves.ContainsKey(identifier)) return InMemorySaves[identifier];

                if (!DEBUG_DisableStorage && File.Exists(path))
                {
                    var res = LoadSaveFromCache(save, db);

                    if (!res.IsValid)
                    {
                        // TODO - no longer necessary? should have been covered by check at top of this method
                        // TODO - log
                        File.Delete(path);
                        return null;
                    }

                    if (res.IsOutdated(db))
                    {
                        File.Delete(path);
                        return LoadSave(containerLocation, save, db, settings);
                    }

                    InMemorySaves.Add(identifier, res);
                    return res;
                }
                else
                {
                    var res = CachedSaveGame.FromSaveGame(containerLocation, save, db, settings);
                    if (res != null)
                    {
                        CrashSupport.ReferencedCachedSave(res);

                        if (!DEBUG_DisableStorage)
                            File.WriteAllText(path, res.ToJson(db));
                    }

                    // TODO - adding `null` entries will prevent re-adding a save at the same path until the app is restarted
                    if (InMemorySaves.ContainsKey(identifier))
                        InMemorySaves.Remove(identifier);

                    InMemorySaves.Add(identifier, res);
                    return res;
                }
            }
        }

        // Removes all data related to the save (in memory + on disk), but does _not_ remove
        // any related entries within AppSettings
        public static void RemoveSave(ISaveGame save)
        {
            lock (InMemorySaves)
                InMemorySaves.Remove(CachedSaveGame.IdentifierFor(save));

            CrashSupport.RemoveReferences(save);
            ClearForSave(save);
        }

        public static void ReloadSave(ISavesLocation containerLocation, ISaveGame save, PalDB db, GameSettings settings)
        {
            Init();

            if (save == null) return;

            CrashSupport.ReferencedSave(save);

            lock (InMemorySaves)
            {
                var identifier = CachedSaveGame.IdentifierFor(save);
                var originalCachedSave = InMemorySaves.GetValueOrDefault(identifier);

                if (originalCachedSave != null)
                {
                    CrashSupport.ReferencedCachedSave(originalCachedSave);
                    InMemorySaves.Remove(identifier);
                }

                var path = SaveCachePathFor(save);
                var wasStored = !DEBUG_DisableStorage && File.Exists(path);
                var backupPath = wasStored ? path + ".bak" : null;

                if (wasStored)
                {
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(path, backupPath);
                }

                var newCachedSave = LoadSave(containerLocation, save, db, settings);

                if (newCachedSave == null)
                {
                    if (!DEBUG_DisableStorage && wasStored)
                    {
                        if (File.Exists(path)) File.Delete(path);

                        File.Move(backupPath, path);
                    }

                    InMemorySaves[identifier] = originalCachedSave;
                }
                else
                {
                    if (!DEBUG_DisableStorage)
                    {
                        if (wasStored) File.Delete(backupPath);

                        if (originalCachedSave != null)
                            originalCachedSave.CopyFrom(newCachedSave);
                    }

                    InMemorySaves[identifier] = originalCachedSave ?? newCachedSave;

                    SaveReloaded?.Invoke(save);
                }
            }
        }

        #endregion
    }
}
