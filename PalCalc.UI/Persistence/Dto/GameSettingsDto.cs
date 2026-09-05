using Newtonsoft.Json;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PerSaveGameSettingsDto
    {
        public int BreedingTimeSeconds { get; init; }
        public int MassiveEggIncubationTimeMinutes { get; init; }
        public bool MultipleBreedingFarms { get; init; }
        public int PalboxTabWidth { get; init; }
        public int PalboxTabHeight { get; init; }
    }

}