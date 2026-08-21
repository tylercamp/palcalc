using Newtonsoft.Json;
using PalCalc.Model;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class SaveCustomizationsDto
    {
        public List<CustomContainerDto> CustomContainers { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class CustomContainerDto
    {
        [JsonProperty(Required = Required.AllowNull)]
        public string Label { get; init; }
        
        public List<PalInstanceSnapshotDto> Contents { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalLocationDto
    {
        [JsonProperty(Required = Required.AllowNull)]
        public string ContainerId { get; init; }

        public LocationType Type { get; init; }
        public int Index { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalInstanceSnapshotDto
    {
        public string InternalName { get; init; }
        public PalLocationDto Location { get; init; }
        public PalGender Gender { get; init; }
        public List<string> PassiveSkills { get; init; }
        public List<string> ActiveSkills { get; init; }
        public List<string> EquippedActiveSkills { get; init; }
        public int Level { get; init; }
        public int IV_HP { get; init; }
        public int IV_Melee { get; init; }
        public int IV_Shot { get; init; }
        public int IV_Defense { get; init; }
        public bool IsOnExpedition { get; init; }

        [JsonProperty(Required = Required.AllowNull)]
        public string OwnerPlayerId { get; init; }
        
        [JsonProperty(Required = Required.AllowNull)]
        public string NickName { get; init; }
        
        [JsonProperty(Required = Required.AllowNull)]
        public string InstanceId { get; init; }
    }

}