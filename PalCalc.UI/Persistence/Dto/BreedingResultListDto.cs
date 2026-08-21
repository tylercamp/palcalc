using Newtonsoft.Json;
using PalCalc.Model;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class BreedingResultListDto
    {
        public GameSettingsSnapshotDto GameSettings { get; init; }
        public SolverSettingsSnapshotDto SolverSettings { get; init; }
        public List<BreedingResultDto> Results { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class GameSettingsSnapshotDto
    {
        public int BreedingTimeSeconds { get; init; }
        public int MassiveEggIncubationTimeMinutes { get; init; }
        public bool MultipleBreedingFarms { get; init; }
        public bool MultipleIncubators { get; init; }
        public int PlayerPartySize { get; init; }
        public Dictionary<LocationType, int> LocationTypeGridWidths { get; init; }
        public Dictionary<LocationType, int?> LocationTypeGridHeights { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class SolverSettingsSnapshotDto
    {
        public int MaxBreedingSteps { get; init; }
        public int MaxSolverIterations { get; init; }
        public int MaxWildPals { get; init; }
        public int MaxInputIrrelevantPassives { get; init; }
        public int MaxBredIrrelevantPassives { get; init; }
        public int MaxThreads { get; init; }
        public int MaxGoldCost { get; init; }
        public bool UseGenderReversers { get; init; }
        public List<string> BannedBredPalInternalNames { get; init; }
        public List<string> BannedWildPalInternalNames { get; init; }
        public List<string> BannedSurgeryPassiveInternalNames { get; init; }
    }

}