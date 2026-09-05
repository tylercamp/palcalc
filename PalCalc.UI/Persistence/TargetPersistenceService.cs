using PalCalc.Model;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.Persistence.Serialization;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using PalCalc.UI.ViewModel.Solver;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameSettingsModel = PalCalc.Model.GameSettings;

namespace PalCalc.UI.Persistence
{

    // Target persistence is save-scoped because the index, source selections, and target files
    // must be read and committed as one aggregate.
    internal static class TargetPersistenceService
    {
        public const string IndexFileName = "pal-target-ids.json";
        public const string LegacyMonolithFileName = "pal-targets.json";

        private static readonly ILogger logger = Log.ForContext(typeof(TargetPersistenceService));

        public static PalTargetListViewModel Load(SaveGameViewModel save, PalDB db)
        {
            var cachedSave = Storage.LoadSaveFromCache(save.Value, db) ?? save.CachedValue;
            return Load(save, cachedSave, db);
        }

        public static PalTargetListViewModel Load(SaveGameViewModel save, CachedSaveGame cachedSave, PalDB db)
        {
            var gameSettings = GameSettingsViewModel.Load(save.Value).ModelObject;
            if (cachedSave == null)
                return new PalTargetListViewModel(new PalSourceViewModel(save, null));

            var empty = new PalTargetListViewModel(new PalSourceViewModel(save, null));

            var dataPath = Storage.SaveFileDataPath(save.Value);
            var indexPath = Path.Combine(dataPath, IndexFileName);
            if (!File.Exists(indexPath))
                return empty;

            try
            {
                var index = TargetJsonSerializer.FromCurrentJson(File.ReadAllText(indexPath));
                SanitizeIndex(index);

                var context = new TargetRehydrationContext(
                    db,
                    save,
                    cachedSave,
                    gameSettings,
                    AppSettings.Current?.SolverSettings ?? new SerializableSolverSettings()
                );

                var targets = new Dictionary<string, PalSpecifierViewModel>(StringComparer.Ordinal);
                var targetsPath = Storage.SaveFileTargetsDataPath(save.Value);
                foreach (var targetId in index.OrderedTargetIds)
                {
                    var target = LoadTarget(targetsPath, targetId, context);
                    if (target != null)
                        targets.Add(target.Id, target);
                }

                // Keep a valid target whose index entry was lost, while still respecting index order.
                if (Directory.Exists(targetsPath))
                {
                    foreach (var path in Directory.EnumerateFiles(targetsPath, "*.json"))
                    {
                        var fileId = Path.GetFileNameWithoutExtension(path);
                        if (targets.ContainsKey(fileId))
                            continue;

                        var target = LoadTarget(targetsPath, fileId, context);
                        if (target != null && !targets.ContainsKey(target.Id))
                            targets.Add(target.Id, target);
                    }
                }

                return TargetJsonSerializer.FromDto(index, context, targets);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "failed to load targets for {saveId}, preserving files and using an empty target list", CachedSaveGame.IdentifierFor(save.Value));
                return empty;
            }
        }

        public static void SaveList(PalTargetListViewModel list, SaveGameViewModel save, PalDB db, GameSettingsModel gameSettings)
        {
            if (Storage.DEBUG_DisableStorage)
                return;

            var dataPath = Storage.SaveFileDataPath(save.Value);
            var targetsPath = Storage.SaveFileTargetsDataPath(save.Value);
            Directory.CreateDirectory(targetsPath);

            var indexJson = TargetJsonSerializer.ToJson(TargetJsonSerializer.ToDto(list, db, gameSettings));
            File.WriteAllText(Path.Combine(dataPath, IndexFileName), indexJson);

            foreach (var target in list.Targets.Where(target => !target.IsReadOnly && target.IsValid))
                SaveTarget(target, save, db, gameSettings);
        }

        public static void SaveTarget(PalSpecifierViewModel target, SaveGameViewModel save, PalDB db, GameSettingsModel gameSettings)
        {
            if (Storage.DEBUG_DisableStorage || target == null || target.IsReadOnly || !target.IsValid)
                return;

            var targetsPath = Storage.SaveFileTargetsDataPath(save.Value);
            Directory.CreateDirectory(targetsPath);
            var targetJson = TargetJsonSerializer.ToJson(TargetJsonSerializer.ToDto(target, db, gameSettings));
            File.WriteAllText(Path.Combine(targetsPath, TargetFileName(target.Id)), targetJson);
        }

        public static void DeleteTarget(PalSpecifierViewModel target, SaveGameViewModel save)
        {
            if (target == null)
                return;

            var path = Path.Combine(Storage.SaveFileTargetsDataPath(save.Value), TargetFileName(target.Id));
            if (File.Exists(path))
                File.Delete(path);
        }

        private static PalSpecifierViewModel LoadTarget(string targetsPath, string fileId, TargetRehydrationContext context)
        {
            try
            {
                var path = Path.Combine(targetsPath, TargetFileName(fileId));
                if (!File.Exists(path))
                {
                    logger.Warning("target index references missing target {targetId}", fileId);
                    return null;
                }

                var dto = TargetJsonSerializer.FromCurrentTargetJson(File.ReadAllText(path));
                if (!string.Equals(dto.Id, fileId, StringComparison.Ordinal))
                    throw new StorageFormatException($"Target filename '{fileId}' does not match document ID '{dto.Id}'.");

                return TargetJsonSerializer.FromDto(dto, context);
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "failed to load target {targetId}, skipping only that target", fileId);
                return null;
            }
        }

        internal static void SanitizeIndex(PalTargetListDto index)
        {
            var targetIds = new HashSet<string>(StringComparer.Ordinal);
            var validTargetIds = new List<string>();
            foreach (var targetId in index.OrderedTargetIds)
            {
                if (string.IsNullOrWhiteSpace(targetId))
                {
                    logger.Warning("Ignoring blank target ID from target index.");
                    continue;
                }

                if (!targetIds.Add(targetId))
                {
                    logger.Warning("Ignoring duplicate target ID '{targetId}' from target index.", targetId);
                    continue;
                }

                try
                {
                    TargetFileName(targetId);
                    validTargetIds.Add(targetId);
                }
                catch (StorageFormatException ex)
                {
                    logger.Warning(ex, "Ignoring unsafe target ID '{targetId}' from target index.", targetId);
                }
            }

            index.OrderedTargetIds.Clear();
            index.OrderedTargetIds.AddRange(validTargetIds);

            var selectionIds = new HashSet<string>(StringComparer.Ordinal);
            var validSelectionIds = new List<string>();
            foreach (var selectionId in index.SourcePals.SelectionIds)
            {
                if (string.IsNullOrWhiteSpace(selectionId))
                {
                    logger.Warning("Ignoring blank source selection ID from target index.");
                    continue;
                }

                if (!selectionIds.Add(selectionId))
                {
                    logger.Warning("Ignoring duplicate source selection ID '{selectionId}' from target index.", selectionId);
                    continue;
                }

                validSelectionIds.Add(selectionId);
            }

            index.SourcePals.SelectionIds.Clear();
            index.SourcePals.SelectionIds.AddRange(validSelectionIds);
        }

        internal static string TargetFileName(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id) || id.Contains(Path.DirectorySeparatorChar) || id.Contains(Path.AltDirectorySeparatorChar))
                throw new StorageFormatException($"Invalid target ID '{id}'.");

            return id.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? id : id + ".json";
        }
    }

}
