using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using GameSettingsModel = PalCalc.Model.GameSettings;

namespace PalCalc.UI.Persistence.Serialization
{
    internal static class ResultJsonSerializer
    {
        public static BreedingResultListDto FromCurrentJson(string json) =>
            JsonConvert.DeserializeObject<BreedingResultListDto>(json)
            ?? throw new JsonSerializationException("Result document was empty.");

        public static string ToJson(BreedingResultListDto value) => JsonConvert.SerializeObject(value);

        public static BreedingResultListDto ToDto(
            BreedingResultListViewModel value,
            PalDB db,
            GameSettingsModel currentGameSettings,
            PalSpecifier target)
        {
            var settings = value.SettingsSnapshot ?? new BreedingResultListViewModelSettingsSnapshot
            {
                GameSettings = currentGameSettings,
                SolverSettings = new SerializableSolverSettings(),
            };

            return new BreedingResultListDto
            {
                GameSettings = ResultJsonSerializer.ToDto(settings.GameSettings ?? currentGameSettings),
                SolverSettings = ResultJsonSerializer.ToDto(settings.SolverSettings ?? new SerializableSolverSettings()),
                Results = (value.Results ?? []).Select(result => ToDto(result)).ToList(),
            };

            BreedingResultDto ToDto(BreedingResultViewModel result) => new()
            {
                PalReference = ResultJsonSerializer.ToDto(result.DisplayedResult),
                CheckedNodes = result.Graph?.Nodes
                    .Select((node, index) => node.IsChecked ? index : -1)
                    .Where(index => index >= 0)
                    .ToList(),
            };
        }

        public static BreedingResultListViewModel FromDto(
            BreedingResultListDto value,
            PalDB db,
            CachedSaveGame cachedSave,
            GameSettingsModel currentGameSettings,
            PalSpecifier target,
            GameSettingsSnapshotDto storedGameSettings,
            SolverSettingsSnapshotDto storedSolverSettings)
        {
            var gameSettings = FromDto(storedGameSettings ?? value.GameSettings);
            var solverSettings = FromDto(storedSolverSettings ?? value.SolverSettings, db);

            var result = new BreedingResultListViewModel
            {
                SettingsSnapshot = new BreedingResultListViewModelSettingsSnapshot
                {
                    GameSettings = gameSettings,
                    SolverSettings = solverSettings,
                },
                Results = value.Results.Select(item =>
                {
                    var displayedResult = FromDto(item.PalReference, db, gameSettings, solverSettings);
                    var vm = new BreedingResultViewModel(cachedSave, gameSettings, displayedResult);

                    if (item.CheckedNodes != null && vm.Graph != null)
                    {
                        foreach (var index in item.CheckedNodes)
                        {
                            if (index >= 0 && index < vm.Graph.Nodes.Count)
                                vm.Graph.Nodes[index].IsChecked = true;
                        }
                    }

                    return vm;
                }).ToList(),
            };

            return result;
        }

        internal static GameSettingsSnapshotDto ToDto(GameSettingsModel value) => new()
        {
            BreedingTimeSeconds = (int)value.BreedingTime.TotalSeconds,
            MassiveEggIncubationTimeMinutes = (int)value.MassiveEggIncubationTime.TotalMinutes,
            MultipleBreedingFarms = value.MultipleBreedingFarms,
            MultipleIncubators = value.MultipleIncubators,
            PlayerPartySize = value.PlayerPartySize,
            LocationTypeGridWidths = value.LocationTypeGridWidths.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LocationTypeGridHeights = value.LocationTypeGridHeights.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };

        internal static GameSettingsModel FromDto(GameSettingsSnapshotDto value) => new()
        {
            BreedingTime = TimeSpan.FromSeconds(value.BreedingTimeSeconds),
            MassiveEggIncubationTime = TimeSpan.FromMinutes(value.MassiveEggIncubationTimeMinutes),
            MultipleBreedingFarms = value.MultipleBreedingFarms,
            MultipleIncubators = value.MultipleIncubators,
            PlayerPartySize = value.PlayerPartySize,
            LocationTypeGridWidths = value.LocationTypeGridWidths.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LocationTypeGridHeights = value.LocationTypeGridHeights.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };

        internal static SolverSettingsSnapshotDto ToDto(SerializableSolverSettings value) => new()
        {
            MaxBreedingSteps = value.MaxBreedingSteps,
            MaxSolverIterations = value.MaxSolverIterations,
            MaxWildPals = value.MaxWildPals,
            MaxInputIrrelevantPassives = value.MaxInputIrrelevantPassives,
            MaxBredIrrelevantPassives = value.MaxBredIrrelevantPassives,
            MaxThreads = value.MaxThreads,
            MaxGoldCost = value.MaxGoldCost,
            UseGenderReversers = value.UseGenderReversers,
            BannedBredPalInternalNames = value.BannedBredPalInternalNames.ToList(),
            BannedWildPalInternalNames = value.BannedWildPalInternalNames.ToList(),
            BannedSurgeryPassiveInternalNames = value.BannedSurgeryPassiveInternalNames.ToList(),
        };

        internal static SerializableSolverSettings FromDto(SolverSettingsSnapshotDto value, PalDB db) => new()
        {
            MaxBreedingSteps = value.MaxBreedingSteps,
            MaxSolverIterations = value.MaxSolverIterations,
            MaxWildPals = value.MaxWildPals,
            MaxInputIrrelevantPassives = value.MaxInputIrrelevantPassives,
            MaxBredIrrelevantPassives = value.MaxBredIrrelevantPassives,
            MaxThreads = value.MaxThreads,
            MaxGoldCost = value.MaxGoldCost,
            UseGenderReversers = value.UseGenderReversers,
            BannedBredPalInternalNames = value.BannedBredPalInternalNames.ToList(),
            BannedWildPalInternalNames = value.BannedWildPalInternalNames.ToList(),
            BannedSurgeryPassiveInternalNames = value.BannedSurgeryPassiveInternalNames.ToList(),
        };

        internal static PalReferenceDto ToDto(IPalReference value) => value switch
        {
            OwnedPalReference owned => new()
            {
                RefType = "OWNED_PAL",
                Instance = CustomizationsJsonSerializer.ToDto(owned.UnderlyingInstance),
                ActualGender = owned.Gender,
                IVs = ToDto(owned.IVs),
                EffectivePassiveInternalNames = owned.EffectivePassives.Select(passive => passive.InternalName).ToList(),
            },
            WildPalReference wild => new()
            {
                RefType = "WILD_PAL",
                PalInternalName = wild.Pal.InternalName,
                GuaranteedPassiveInternalNames = wild.EffectivePassives.Where(passive => passive is not RandomPassiveSkill).Select(passive => passive.InternalName).ToList(),
                RandomPassiveCount = wild.EffectivePassives.Count(passive => passive is RandomPassiveSkill),
                Gender = wild.Gender,
            },
            BredPalReference bred => new()
            {
                RefType = "BRED_PAL",
                PalInternalName = bred.Pal.InternalName,
                EffectivePassiveInternalNames = bred.EffectivePassives.Select(passive => passive.InternalName).ToList(),
                Parent1 = ToDto(bred.Parent1),
                Parent2 = ToDto(bred.Parent2),
                Gender = bred.Gender,
                PassivesProbability = bred.PassivesProbability,
                IVs = ToDto(bred.IVs),
                IVsProbability = bred.IVsProbability,
            },
            CompositeOwnedPalReference composite => new()
            {
                RefType = "COMPOSITE_PAL",
                Male = ToDto(composite.Male),
                Female = ToDto(composite.Female),
                Gender = composite.Gender,
            },
            SurgeryTablePalReference surgery => new()
            {
                RefType = "SURGERY_PAL",
                Input = ToDto(surgery.Input),
                Operations = surgery.Operations.Select(ToDto).ToList(),
            },
            _ => throw new InvalidOperationException($"Unsupported IPalReference type '{value?.GetType().Name}'."),
        };

        internal static IPalReference FromDto(PalReferenceDto value, PalDB db, GameSettingsModel settings, SerializableSolverSettings solverSettings)
        {
            return value.RefType switch
            {
                "OWNED_PAL" => FromOwned(value, db, solverSettings),
                "WILD_PAL" => FromWild(value, db, solverSettings),
                "BRED_PAL" => FromBred(value, db, settings, solverSettings),
                "COMPOSITE_PAL" => new CompositeOwnedPalReference(
                    (OwnedPalReference)FromDto(value.Male, db, settings, solverSettings),
                    (OwnedPalReference)FromDto(value.Female, db, settings, solverSettings)
                ).WithGuaranteedGender(db, value.Gender.Value, false),
                "SURGERY_PAL" => new SurgeryTablePalReference(
                    FromDto(value.Input, db, settings, solverSettings),
                    value.Operations.Select(operation => FromDto(operation, db)).ToList()
                ),
                _ => throw new InvalidOperationException($"Unsupported persisted IPalReference discriminator '{value.RefType}'."),
            };
        }

        private static OwnedPalReference FromOwned(PalReferenceDto value, PalDB db, SerializableSolverSettings solverSettings)
        {
            var instance = CustomizationsJsonSerializer.FromDto(value.Instance, db);
            var effectivePassives = value.EffectivePassiveInternalNames
                .Select(name => name.InternalToStandardPassive(db))
                .ToList();
            var result = new OwnedPalReference(instance, effectivePassives, FromDto(value.IVs));
            return value.ActualGender.Value == instance.Gender
                ? result
                : (OwnedPalReference)result.WithGuaranteedGender(db, value.ActualGender.Value, solverSettings.UseGenderReversers);
        }

        private static IPalReference FromWild(PalReferenceDto value, PalDB db, SerializableSolverSettings solverSettings)
        {
            var result = new WildPalReference(
                value.PalInternalName.InternalToPal(db),
                value.GuaranteedPassiveInternalNames.Select(name => name.InternalToStandardPassive(db)),
                value.RandomPassiveCount.Value,
                db.BreedingMechanics
            );
            return result.WithGuaranteedGender(db, value.Gender.Value, solverSettings.UseGenderReversers);
        }

        private static IPalReference FromBred(PalReferenceDto value, PalDB db, GameSettingsModel settings, SerializableSolverSettings solverSettings)
        {
            var result = new BredPalReference(
                settings,
                value.PalInternalName.InternalToPal(db),
                FromDto(value.Parent1, db, settings, solverSettings),
                FromDto(value.Parent2, db, settings, solverSettings),
                value.EffectivePassiveInternalNames.Select(name => name.InternalToStandardPassive(db)).ToList(),
                value.PassivesProbability.Value,
                FromDto(value.IVs),
                value.IVsProbability.Value
            );
            return result.WithGuaranteedGender(db, value.Gender.Value, solverSettings.UseGenderReversers);
        }

        private static SurgeryOperationDto ToDto(ISurgeryOperation value) => value switch
        {
            AddPassiveSurgeryOperation add => new() { Type = "ADD_PASSIVE", AddedPassiveInternalName = add.AddedPassive.InternalName },
            ReplacePassiveSurgeryOperation replace => new()
            {
                Type = "REPLACE_PASSIVE",
                AddedPassiveInternalName = replace.AddedPassive.InternalName,
                RemovedPassiveInternalName = replace.RemovedPassive.InternalName,
            },
            _ => throw new InvalidOperationException($"Unsupported surgery operation type '{value?.GetType().Name}'."),
        };

        private static ISurgeryOperation FromDto(SurgeryOperationDto value, PalDB db) => value.Type switch
        {
            "ADD_PASSIVE" => AddPassiveSurgeryOperation.NewCached(value.AddedPassiveInternalName.InternalToStandardPassive(db)),
            "REPLACE_PASSIVE" => ReplacePassiveSurgeryOperation.NewCached(
                value.RemovedPassiveInternalName.InternalToStandardPassive(db),
                value.AddedPassiveInternalName.InternalToStandardPassive(db)
            ),
            _ => throw new InvalidOperationException($"Unsupported surgery operation discriminator '{value.Type}'."),
        };

        private static IvSetDto ToDto(IV_Set value) => new()
        {
            HP = ToDto(value.HP),
            Attack = ToDto(value.Attack),
            Defense = ToDto(value.Defense),
        };

        private static IV_Set FromDto(IvSetDto value) => new(FromDto(value.HP), FromDto(value.Attack), FromDto(value.Defense));

        private static IvValueDto ToDto(IV_Value value) => new()
        {
            IsRandom = value == IV_Value.Random,
            IsRelevant = value.IsRelevant,
            Min = value.Min,
            Max = value.Max,
        };

        private static IV_Value FromDto(IvValueDto value) => value.IsRandom
            ? IV_Value.Random
            : new IV_Value(value.IsRelevant, value.Min, value.Max);
    }
}
