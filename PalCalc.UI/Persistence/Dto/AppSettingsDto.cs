using Newtonsoft.Json;
using PalCalc.UI.Localization;
using PalCalc.UI.Model;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class AppSettingsDto
    {
        public List<string> ExtraSaveLocations { get; init; }
        public List<string> FakeSaveNames { get; init; }
        public SolverSettingsDto SolverSettings { get; init; }
        public List<PassiveSkillsPresetDto> PassiveSkillsPresets { get; init; }
        public List<PalListPresetDto> PalListPresets { get; init; }
        public TranslationLocale Locale { get; init; }
        public bool IsDarkTheme { get; init; }
        public BreedingResultListColumnSettingsDto BreedingResultListColumns { get; init; }
        public UiLayoutSettingsDto UiLayout { get; init; }

        [JsonProperty(Required = Required.AllowNull)]
        public string SelectedGameIdentifier { get; init; }

        [JsonProperty(Required = Required.AllowNull)]
        public string SkippedAppVersion { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class SolverSettingsDto
    {
        public int MaxBreedingSteps { get; init; }
        public int MaxSolverIterations { get; init; }
        public int MaxWildPals { get; init; }
        public int MaxInputIrrelevantPassives { get; init; }
        public int MaxBredIrrelevantPassives { get; init; }
        public int MaxThreads { get; init; }
        public int MaxGoldCost { get; init; }
        public int MaxSpecialCakes { get; init; }
        public bool UseGenderReversers { get; init; }
        public List<string> BannedBredPalInternalNames { get; init; }
        public List<string> BannedWildPalInternalNames { get; init; }
        public List<string> BannedSurgeryPassiveInternalNames { get; init; }
    }

    // Sparse slot DTO: every property must be present, but null values are allowed.
    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class PassiveSkillsPresetDto
    {
        public string Name { get; init; }
        public string Passive1InternalName { get; init; }
        public string Passive2InternalName { get; init; }
        public string Passive3InternalName { get; init; }
        public string Passive4InternalName { get; init; }
        public string OptionalPassive1InternalName { get; init; }
        public string OptionalPassive2InternalName { get; init; }
        public string OptionalPassive3InternalName { get; init; }
        public string OptionalPassive4InternalName { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalListPresetDto
    {
        [JsonProperty(Required = Required.AllowNull)]
        public string Name { get; init; }


        public List<string> PalInternalNames { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class BreedingResultListColumnSettingsDto
    {
        public Dictionary<string, bool> ColumnVisibility { get; init; }
        public List<string> ColumnOrder { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class UiLayoutSettingsDto
    {
        public Dictionary<string, WindowPlacementSettingsDto> Windows { get; init; }
        public Dictionary<string, GridLayoutSettingsDto> Grids { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class WindowPlacementSettingsDto
    {
        public int Version { get; init; }
        public int Left { get; init; }
        public int Top { get; init; }
        public int Right { get; init; }
        public int Bottom { get; init; }
        public bool IsMaximized { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class GridLayoutSettingsDto
    {
        public int Version { get; init; }
        public List<GridLengthSettingsDto> Columns { get; init; }
        public List<GridLengthSettingsDto> Rows { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class GridLengthSettingsDto
    {
        public LayoutGridUnit Unit { get; init; }
        public double Value { get; init; }
    }

}
