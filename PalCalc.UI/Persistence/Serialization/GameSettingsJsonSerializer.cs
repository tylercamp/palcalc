using Newtonsoft.Json;
using PalCalc.UI.Persistence.Dto;

namespace PalCalc.UI.Persistence.Serialization
{

    internal static class GameSettingsJsonSerializer
    {
        public static PerSaveGameSettingsDto FromCurrentJson(string json) =>
            JsonConvert.DeserializeObject<PerSaveGameSettingsDto>(json)
            ?? throw new JsonSerializationException("Per-save game settings document was empty.");

        public static string ToJson(PerSaveGameSettingsDto dto) => JsonConvert.SerializeObject(dto);
    }

}
