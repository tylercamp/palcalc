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
        /// Returns the world coords in normalized map coordinates (X and Y in range [0 1], used for UI position calcs in Pal Calc)
        /// </summary>
        public static MapCoord NormalizedFromWorldCoord(WorldCoord coord)
        {
            // (X and Y world coords are swapped when presented as map coords; Z is unused)
            return new MapCoord()
            {
                X = (coord.Y - GameConstants.Map_MinY) / (GameConstants.Map_MaxY - GameConstants.Map_MinY),
                Y = (coord.X - GameConstants.Map_MinX) / (GameConstants.Map_MaxX - GameConstants.Map_MinX)
            };
        }

        /// <summary>
        /// Returns the associated coordinates you would see in-game if you opened the map and moved the cursor to this world position.
        /// </summary>
        public static MapCoord UIFromWorldCoord(WorldCoord coord)
        {
            var norm = NormalizedFromWorldCoord(coord);

            return new MapCoord()
            {
                X = 1000 * (norm.X * 2 - 1),
                Y = 1000 * (norm.Y * 2 - 1),
            };
        }
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
