using Newtonsoft.Json;
using PalCalc.UI.Model;
using PalCalc.UI.Persistence.Dto;
using System;
using System.Linq;

namespace PalCalc.UI.Persistence.Serialization
{

    internal static class AppSettingsJsonSerializer
    {
        public static AppSettingsDto FromCurrentJson(string json) =>
            JsonConvert.DeserializeObject<AppSettingsDto>(json)
            ?? throw new JsonSerializationException("Application settings document was empty.");

        public static string ToJson(AppSettingsDto dto) => JsonConvert.SerializeObject(dto);

        public static AppSettingsDto ToDto(AppSettings value)
        {
            value = Normalize(value);
            return new AppSettingsDto
            {
                ExtraSaveLocations = value.ExtraSaveLocations.ToList(),
                FakeSaveNames = value.FakeSaveNames.ToList(),
                SolverSettings = new SolverSettingsDto
                {
                    MaxBreedingSteps = value.SolverSettings.MaxBreedingSteps,
                    MaxSolverIterations = value.SolverSettings.MaxSolverIterations,
                    MaxWildPals = value.SolverSettings.MaxWildPals,
                    MaxInputIrrelevantPassives = value.SolverSettings.MaxInputIrrelevantPassives,
                    MaxBredIrrelevantPassives = value.SolverSettings.MaxBredIrrelevantPassives,
                    MaxThreads = value.SolverSettings.MaxThreads,
                    MaxGoldCost = value.SolverSettings.MaxGoldCost,
                    UseGenderReversers = value.SolverSettings.UseGenderReversers,
                    BannedBredPalInternalNames = value.SolverSettings.BannedBredPalInternalNames.ToList(),
                    BannedWildPalInternalNames = value.SolverSettings.BannedWildPalInternalNames.ToList(),
                    BannedSurgeryPassiveInternalNames = value.SolverSettings.BannedSurgeryPassiveInternalNames.ToList(),
                },
                PassiveSkillsPresets = value.PassiveSkillsPresets.Select(ToDto).ToList(),
                PalListPresets = value.PalListPresets.Select(ToDto).ToList(),
                SelectedGameIdentifier = value.SelectedGameIdentifier,
                Locale = value.Locale,
                IsDarkTheme = value.IsDarkTheme,
                BreedingResultListColumns = ToDto(value.BreedingResultListColumns),
                UiLayout = ToDto(value.UiLayout),
                SkippedAppVersion = value.SkippedAppVersion,
            };
        }

        public static AppSettings FromDto(AppSettingsDto value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            return new AppSettings
            {
                ExtraSaveLocations = value.ExtraSaveLocations.ToList(),
                FakeSaveNames = value.FakeSaveNames.ToList(),
                SolverSettings = new SerializableSolverSettings
                {
                    MaxBreedingSteps = value.SolverSettings.MaxBreedingSteps,
                    MaxSolverIterations = value.SolverSettings.MaxSolverIterations,
                    MaxWildPals = value.SolverSettings.MaxWildPals,
                    MaxInputIrrelevantPassives = value.SolverSettings.MaxInputIrrelevantPassives,
                    MaxBredIrrelevantPassives = value.SolverSettings.MaxBredIrrelevantPassives,
                    MaxThreads = value.SolverSettings.MaxThreads,
                    MaxGoldCost = value.SolverSettings.MaxGoldCost,
                    UseGenderReversers = value.SolverSettings.UseGenderReversers,
                    BannedBredPalInternalNames = value.SolverSettings.BannedBredPalInternalNames.ToList(),
                    BannedWildPalInternalNames = value.SolverSettings.BannedWildPalInternalNames.ToList(),
                    BannedSurgeryPassiveInternalNames = value.SolverSettings.BannedSurgeryPassiveInternalNames.ToList(),
                },
                PassiveSkillsPresets = value.PassiveSkillsPresets.Select(FromDto).ToList(),
                PalListPresets = value.PalListPresets.Select(FromDto).ToList(),
                SelectedGameIdentifier = value.SelectedGameIdentifier,
                Locale = value.Locale,
                IsDarkTheme = value.IsDarkTheme,
                BreedingResultListColumns = FromDto(value.BreedingResultListColumns),
                UiLayout = FromDto(value.UiLayout),
                SkippedAppVersion = value.SkippedAppVersion,
            };
        }

        private static AppSettings Normalize(AppSettings value)
        {
            value ??= new AppSettings();
            value.ExtraSaveLocations ??= [];
            value.FakeSaveNames ??= [];
            value.SolverSettings ??= new SerializableSolverSettings();
            value.SolverSettings.BannedBredPalInternalNames ??= [];
            value.SolverSettings.BannedWildPalInternalNames ??= [];
            value.SolverSettings.BannedSurgeryPassiveInternalNames ??= [];
            value.PassiveSkillsPresets ??= [];
            value.PalListPresets ??= [];
            value.BreedingResultListColumns ??= new BreedingResultListColumnSettings();
            value.BreedingResultListColumns.ColumnVisibility ??= new();
            value.BreedingResultListColumns.ColumnOrder ??= [];
            value.UiLayout ??= new UiLayoutSettings();
            value.UiLayout.Windows ??= new();
            value.UiLayout.Grids ??= new();
            return value;
        }

        private static PassiveSkillsPresetDto ToDto(PassiveSkillsPreset value) => new()
        {
            Name = value.Name,
            Passive1InternalName = value.Passive1InternalName,
            Passive2InternalName = value.Passive2InternalName,
            Passive3InternalName = value.Passive3InternalName,
            Passive4InternalName = value.Passive4InternalName,
            OptionalPassive1InternalName = value.OptionalPassive1InternalName,
            OptionalPassive2InternalName = value.OptionalPassive2InternalName,
            OptionalPassive3InternalName = value.OptionalPassive3InternalName,
            OptionalPassive4InternalName = value.OptionalPassive4InternalName,
        };

        private static PassiveSkillsPreset FromDto(PassiveSkillsPresetDto value) => new()
        {
            Name = value.Name,
            Passive1InternalName = value.Passive1InternalName,
            Passive2InternalName = value.Passive2InternalName,
            Passive3InternalName = value.Passive3InternalName,
            Passive4InternalName = value.Passive4InternalName,
            OptionalPassive1InternalName = value.OptionalPassive1InternalName,
            OptionalPassive2InternalName = value.OptionalPassive2InternalName,
            OptionalPassive3InternalName = value.OptionalPassive3InternalName,
            OptionalPassive4InternalName = value.OptionalPassive4InternalName,
        };

        private static PalListPresetDto ToDto(PalListPreset value) => new()
        {
            Name = value.Name,
            PalInternalNames = (value.PalInternalNames ?? []).ToList(),
        };

        private static PalListPreset FromDto(PalListPresetDto value) => new()
        {
            Name = value.Name,
            PalInternalNames = value.PalInternalNames.ToList(),
        };

        private static BreedingResultListColumnSettingsDto ToDto(BreedingResultListColumnSettings value) => new()
        {
            ColumnVisibility = value.ColumnVisibility.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ColumnOrder = value.ColumnOrder.ToList(),
        };

        private static BreedingResultListColumnSettings FromDto(BreedingResultListColumnSettingsDto value) => new()
        {
            ColumnVisibility = value.ColumnVisibility.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ColumnOrder = value.ColumnOrder.ToList(),
        };

        private static UiLayoutSettingsDto ToDto(UiLayoutSettings value) => new()
        {
            Windows = value.Windows.ToDictionary(kvp => kvp.Key, kvp => ToDto(kvp.Value)),
            Grids = value.Grids.ToDictionary(kvp => kvp.Key, kvp => ToDto(kvp.Value)),
        };

        private static UiLayoutSettings FromDto(UiLayoutSettingsDto value) => new()
        {
            Windows = value.Windows.ToDictionary(kvp => kvp.Key, kvp => FromDto(kvp.Value)),
            Grids = value.Grids.ToDictionary(kvp => kvp.Key, kvp => FromDto(kvp.Value)),
        };

        private static WindowPlacementSettingsDto ToDto(WindowPlacementSettings value) => new()
        {
            Version = value.Version,
            Left = value.Left,
            Top = value.Top,
            Right = value.Right,
            Bottom = value.Bottom,
            IsMaximized = value.IsMaximized,
        };

        private static WindowPlacementSettings FromDto(WindowPlacementSettingsDto value) => new()
        {
            Version = value.Version,
            Left = value.Left,
            Top = value.Top,
            Right = value.Right,
            Bottom = value.Bottom,
            IsMaximized = value.IsMaximized,
        };

        private static GridLayoutSettingsDto ToDto(GridLayoutSettings value) => new()
        {
            Version = value.Version,
            Columns = value.Columns.Select(ToDto).ToList(),
            Rows = value.Rows.Select(ToDto).ToList(),
        };

        private static GridLayoutSettings FromDto(GridLayoutSettingsDto value) => new()
        {
            Version = value.Version,
            Columns = value.Columns.Select(FromDto).ToList(),
            Rows = value.Rows.Select(FromDto).ToList(),
        };

        private static GridLengthSettingsDto ToDto(GridLengthSettings value) => new()
        {
            Unit = value.Unit,
            Value = value.Value,
        };

        private static GridLengthSettings FromDto(GridLengthSettingsDto value) => new()
        {
            Unit = value.Unit,
            Value = value.Value,
        };
    }

}
