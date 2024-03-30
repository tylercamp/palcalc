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

        // pal box is 6x5
        public override string ToString()
        {
            string indexStr;
            if (Type == LocationType.Palbox)
            {
                var numCols = 6;
                var numRows = 5;
                var palsPerTab = numCols * numRows;

                var idx = Index - 1;

                var tab = (idx - (idx % palsPerTab)) / palsPerTab;
                idx -= tab * palsPerTab;

                var row = (idx - (idx % numCols)) / numCols;
                idx -= row * numCols;
                var col = idx;

                indexStr = $"tab #{tab+1} at ({col + 1}, {row + 1})";
            }
            else
            {
                indexStr = $"Slot #{Index}";
            }

            return $"{Type} ({indexStr})";
        }
    }
}
