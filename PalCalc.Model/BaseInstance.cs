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

        public static MapCoord FromWorldCoord(WorldCoord coord)
        {
            double xnorm = (coord.X - GameConstants.Map_MinX) / (GameConstants.Map_MaxX - GameConstants.Map_MinX);
            double ynorm = (coord.Y - GameConstants.Map_MinY) / (GameConstants.Map_MaxY - GameConstants.Map_MinY);

            // (X and Y world coords are swapped when presented as map coords; Z is unused)
            return new MapCoord()
            {
                X = 1000 * (ynorm * 2 - 1),
                Y = 1000 * (xnorm * 2 - 1),
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
