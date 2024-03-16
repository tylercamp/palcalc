using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.model
{
    [JsonConverter(typeof(StringEnumConverter))]
    enum LocationType
    {
        PlayerParty,
        Palbox,
        Base
    }

    internal class PalLocation
    {
        public LocationType Type { get; set; }
        public int Index { get; set; }

        public override string ToString() => $"{Type} (Slot #{Index})";
    }
}
