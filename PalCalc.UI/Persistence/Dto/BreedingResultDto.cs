using Newtonsoft.Json;
using PalCalc.Model;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{
    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class BreedingResultDto
    {
        public PalReferenceDto PalReference { get; init; }

        [JsonProperty(Required = Required.AllowNull)]
        public List<int> CheckedNodes { get; init; }
    }

    // Discriminated union: every property must be present, but only RefType is always non-null.
    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class PalReferenceDto
    {
        [JsonProperty(Required = Required.Always)]
        public string RefType { get; init; }

        public PalInstanceSnapshotDto Instance { get; init; }
        public PalGender? ActualGender { get; init; }
        public IvSetDto IVs { get; init; }
        public List<string> EffectivePassiveInternalNames { get; init; }

        public string PalInternalName { get; init; }
        public List<string> GuaranteedPassiveInternalNames { get; init; }
        public int? RandomPassiveCount { get; init; }
        public PalGender? Gender { get; init; }

        public PalReferenceDto Parent1 { get; init; }
        public PalReferenceDto Parent2 { get; init; }
        public float? PassivesProbability { get; init; }
        public float? IVsProbability { get; init; }

        public PalReferenceDto Male { get; init; }
        public PalReferenceDto Female { get; init; }

        public PalReferenceDto Input { get; init; }
        public List<SurgeryOperationDto> Operations { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class IvSetDto
    {
        public IvValueDto HP { get; init; }
        public IvValueDto Attack { get; init; }
        public IvValueDto Defense { get; init; }
    }

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class IvValueDto
    {
        public bool IsRandom { get; init; }
        public bool IsRelevant { get; init; }
        public int Min { get; init; }
        public int Max { get; init; }
    }

    // Discriminated union: every property must be present, but only Type is always non-null.
    [JsonObject(ItemRequired = Required.AllowNull)]
    internal sealed class SurgeryOperationDto
    {
        [JsonProperty(Required = Required.Always)]
        public string Type { get; init; }
        
        public string AddedPassiveInternalName { get; init; }
        public string RemovedPassiveInternalName { get; init; }
    }

}
