using Newtonsoft.Json;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalTargetListDto
    {
        public List<string> OrderedTargetIds { get; init; }
        public PalSourceDto SourcePals { get; init; }
    }

}