using Newtonsoft.Json;
using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.UI.Persistence;
using PalCalc.UI.Persistence.Dto;
using PalCalc.UI.ViewModel;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.SaveSelection;
using PalCalc.UI.ViewModel.Solver;
using System;
using System.Collections.Generic;
using System.Linq;
using GameSettingsModel = PalCalc.Model.GameSettings;

namespace PalCalc.UI.Persistence.Serialization
{

    internal static class TargetJsonSerializer
    {
        public static PalTargetListDto FromCurrentJson(string json) =>
            JsonConvert.DeserializeObject<PalTargetListDto>(json)
            ?? throw new JsonSerializationException("Target index document was empty.");

        public static PalSpecifierDto FromCurrentTargetJson(string json) =>
            JsonConvert.DeserializeObject<PalSpecifierDto>(json)
            ?? throw new JsonSerializationException("Target document was empty.");

        public static string ToJson(PalTargetListDto value) => JsonConvert.SerializeObject(value);
        public static string ToJson(PalSpecifierDto value) => JsonConvert.SerializeObject(value);

        public static PalTargetListDto ToDto(PalTargetListViewModel value, PalDB db, GameSettingsModel gameSettings)
        {
            var persistedTargets = value.Targets.Where(target => !target.IsReadOnly && target.IsValid).ToList();
            return new PalTargetListDto
            {
                OrderedTargetIds = persistedTargets.Select(target => target.Id).ToList(),
                SourcePals = ToDto(value.SourcePals),
            };
        }

        public static PalSpecifierDto ToDto(PalSpecifierViewModel value, PalDB db, GameSettingsModel gameSettings) => new()
        {
            Id = value.Id,
            TargetPalInternalName = value.TargetPal?.ModelObject.InternalName,
            RequiredAttackInternalNames = value.RequiredAttacks.AsModelEnumerable()
                .Select(attack => attack.InternalName)
                .ToList(),
            RequiredPassiveInternalNames =
            [
                value.RequiredPassives.Passive1?.ModelObject?.InternalName,
                value.RequiredPassives.Passive2?.ModelObject?.InternalName,
                value.RequiredPassives.Passive3?.ModelObject?.InternalName,
                value.RequiredPassives.Passive4?.ModelObject?.InternalName,
            ],
            OptionalPassiveInternalNames =
            [
                value.OptionalPassives.Passive1?.ModelObject?.InternalName,
                value.OptionalPassives.Passive2?.ModelObject?.InternalName,
                value.OptionalPassives.Passive3?.ModelObject?.InternalName,
                value.OptionalPassives.Passive4?.ModelObject?.InternalName,
            ],
            MinimumIV_HP = value.MinIv_HP,
            MinimumIV_Attack = value.MinIv_Attack,
            MinimumIV_Defense = value.MinIv_Defense,
            RequiredGender = value.RequiredGender?.Value ?? PalGender.WILDCARD,
            CurrentResults = value.CurrentResults == null
                ? null
                : ResultJsonSerializer.ToDto(value.CurrentResults, db, gameSettings, value.ModelObject),
        };

        public static PalTargetListViewModel FromDto(PalTargetListDto value, TargetRehydrationContext context, IReadOnlyDictionary<string, PalSpecifierViewModel> targets)
        {
            var source = FromDto(value.SourcePals, context);
            var orderedTargets = value.OrderedTargetIds
                .Where(targets.ContainsKey)
                .Select(id => targets[id])
                .Concat(targets.Values.Where(target => !value.OrderedTargetIds.Contains(target.Id)).OrderBy(target => target.Id, StringComparer.Ordinal))
                .ToList();

            return new PalTargetListViewModel(source, orderedTargets);
        }

        public static PalSpecifierViewModel FromDto(PalSpecifierDto value, TargetRehydrationContext context)
        {
            var requiredAttacks = value.RequiredAttackInternalNames
                .Select(name => name.InternalToActive(context.Database))
                .Distinct()
                .ToList();
            if (requiredAttacks.Count > PalSpecifier.MaxRequiredAttacks)
                throw new JsonSerializationException($"A target can require at most {PalSpecifier.MaxRequiredAttacks} attacks.");

            var model = new PalSpecifier
            {
                Pal = value.TargetPalInternalName.InternalToPal(context.Database),
                RequiredAttacks = requiredAttacks,
                RequiredPassives = value.RequiredPassiveInternalNames
                    .Select(name => name?.InternalToStandardPassive(context.Database))
                    .ToList(),
                OptionalPassives = value.OptionalPassiveInternalNames
                    .Select(name => name?.InternalToStandardPassive(context.Database))
                    .ToList(),
                RequiredGender = value.RequiredGender,
                IV_HP = value.MinimumIV_HP,
                IV_Attack = value.MinimumIV_Attack,
                IV_Defense = value.MinimumIV_Defense,
            };

            var result = new PalSpecifierViewModel(value.Id, model)
            {
                RequiredGender = PalGenderViewModel.Make(value.RequiredGender),
                MinIv_HP = value.MinimumIV_HP,
                MinIv_Attack = value.MinimumIV_Attack,
                MinIv_Defense = value.MinimumIV_Defense,
            };

            if (value.CurrentResults != null)
            {
                try
                {
                    result.CurrentResults = ResultJsonSerializer.FromDto(
                        value.CurrentResults,
                        context.Database,
                        context.CachedSave,
                        context.CurrentGameSettings,
                        model,
                        value.CurrentResults.GameSettings,
                        value.CurrentResults.SolverSettings
                    );
                }
                catch (Exception)
                {
                    // A target remains valid when only its rebuildable result cache is unavailable.
                    result.CurrentResults = null;
                }
            }

            return result;
        }

        private static PalSourceDto ToDto(PalSourceViewModel value) => new()
        {
            SelectionIds = value.SerializedSelectionIds.ToList(),
            IncludeBasePals = value.IncludeBasePals,
            IncludeCustomPals = value.IncludeCustomPals,
            IncludeCagedPals = value.IncludeCagedPals,
            IncludeGlobalStoragePals = value.IncludeGlobalStoragePals,
            IncludeExpeditionPals = value.IncludeExpeditionPals,
        };

        private static PalSourceViewModel FromDto(PalSourceDto value, TargetRehydrationContext context)
        {
            var selections = value.SelectionIds
                .Select(id => IPalSourceTreeSelection.SingleFromId(context.CachedSave, id))
                .Where(selection => selection != null)
                .ToList();

            var source = new PalSourceViewModel(context.Save, selections)
            {
                IncludeBasePals = value.IncludeBasePals,
                IncludeCustomPals = value.IncludeCustomPals,
                IncludeCagedPals = value.IncludeCagedPals,
                IncludeGlobalStoragePals = value.IncludeGlobalStoragePals,
                IncludeExpeditionPals = value.IncludeExpeditionPals,
                PersistedSelectionIds = value.SelectionIds.ToList(),
            };

            return source;
        }
    }

}
