using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LocationType
    {
        PlayerParty,
        Palbox,
        Base,
        ViewingCage,
        Custom,
    }

    public class PalLocation
    {
        public string ContainerId { get; set; }
        public LocationType Type { get; set; }
        public int Index { get; set; }

        // pal box is 6x5
        public override string ToString()
        {
            string indexStr;
            if (Type == LocationType.PlayerParty)
            {
                indexStr = $"Slot #{Index + 1}";
            }
            else
            {
                var coord = PalDisplayCoord.FromLocation(this);
                indexStr = coord.ToString();
            }

            return $"{Type} ({indexStr})";
        }

        public override int GetHashCode() => HashCode.Combine(Type, Index);
    }

    public class PalDisplayCoord
    {
        // all 1-indexed
        public int? Tab { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }

        public int X => Column;
        public int Y => Row;

        public override string ToString()
        {
            if (Tab != null) return $"Tab {Tab.Value} at ({X},{Y})";
            else return $"Slot ({X},{Y})";
        }

        public static PalDisplayCoord FromLocation(PalLocation loc) => FromLocation(loc.Type, loc.Index);

        public static PalDisplayCoord FromLocation(LocationType type, int slotIndex)
        {
            var numCols = GameConstants.LocationTypeGridWidths[type];
            var numRows = GameConstants.LocationTypeGridHeights[type];

            int? tab = null;

            if (numRows != null)
            {
                var palsPerTab = numCols * numRows.Value;

                tab = (slotIndex - slotIndex % palsPerTab) / palsPerTab;
                slotIndex -= tab.Value * palsPerTab;
            }

            var row = (slotIndex - slotIndex % numCols) / numCols;
            slotIndex -= row * numCols;
            var col = slotIndex;

            return new PalDisplayCoord()
            {
                Tab = tab == null ? tab : (tab.Value + 1),
                Row = row + 1,
                Column = col + 1
            };
        }
    }
}
