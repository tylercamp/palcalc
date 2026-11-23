using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public class WorldCoord
    {
        public double X { get; set; }
        public double Y{ get; set; }
        public double Z { get; set; }
    }

    public class MapCoord
    {
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// Returns the position on PalCalc's map image, with coordinates from 0 to 1 within the map bounds.
        /// </summary>
        public static MapCoord NormalizedFromWorldCoord(WorldCoord coord) => new()
        {
            // Reverse engineered from WorldLocationToWidgetOffset with the MainMap landscape bounds.
            // We assume the FirstRegion texture covers those bounds. See ../docres/research/palworld-misc/README.md#map-pin-placement.
            X = (coord.Y + 724400.0) / 1448800.0,
            Y = (349400.0 - coord.X) / 1448800.0,
        };

        /// <summary>
        /// Returns the associated coordinates you would see in-game if you opened the map and moved the cursor to this world position.
        /// </summary>
        public static MapCoord UIFromWorldCoord(WorldCoord coord) => new()
        {
            // Reverse engineered from WBP_Map_Base_C.PrintPosition. See ../docres/research/palworld-misc/README.md#coordinate-labels.
            X = (coord.Y - 158000.0) / 459.0,
            Y = (coord.X + 123888.0) / 459.0,
        };
    }

    public class BaseInstance
    {
        public string Id { get; set; }
        public string OwnerGuildId { get; set; }
        
        public BasePalContainer Container { get; set; }

        public List<ViewingCageContainer> ViewingCages { get; set; }

        public WorldCoord Position { get; set; }
    }
}
