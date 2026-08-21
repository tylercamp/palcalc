using Newtonsoft.Json;
using System.Collections.Generic;

namespace PalCalc.UI.Persistence.Dto
{

    [JsonObject(ItemRequired = Required.Always)]
    internal sealed class PalSourceDto
    {
        public List<string> SelectionIds { get; init; }
        public bool IncludeBasePals { get; init; }
        public bool IncludeCustomPals { get; init; }
        public bool IncludeCagedPals { get; init; }
        public bool IncludeGlobalStoragePals { get; init; }
        public bool IncludeExpeditionPals { get; init; }
    }

}