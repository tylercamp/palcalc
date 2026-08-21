using Newtonsoft.Json;
using PalCalc.Model;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalSpecifierDto
    {
        public string Id { get; init; }
        public string TargetPalInternalName { get; init; }
        public List<string> RequiredPassiveInternalNames { get; init; }
        public List<string> OptionalPassiveInternalNames { get; init; }
        public int MinimumIV_HP { get; init; }
        public int MinimumIV_Attack { get; init; }
        public int MinimumIV_Defense { get; init; }
        public PalGender RequiredGender { get; init; }

        [JsonProperty(Required = Required.AllowNull)]
        public BreedingResultListDto CurrentResults { get; init; }
    }

}