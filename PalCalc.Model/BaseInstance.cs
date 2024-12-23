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

        static double[] ApplyMatrix(double[,] matrix, double x, double y)
        {
            // matrix is 3×3, point is [x, y, 1]
            double xPrime = matrix[0, 0] * x + matrix[0, 1] * y + matrix[0, 2];
            double yPrime = matrix[1, 0] * x + matrix[1, 1] * y + matrix[1, 2];
            // In a pure 2D affine transform, the 3rd row always yields 1, so ignore it
            return new double[] { xPrime, yPrime };
        }


        /// <summary>
        /// Returns the world coords in normalized map coordinates (X and Y in range [0 1], used for UI position calcs in Pal Calc)
        /// </summary>
        public static MapCoord NormalizedFromWorldCoord(WorldCoord coord)
        {
            var transformed = ApplyMatrix(GameConstants.WorldToImageMatrix, coord.X, coord.Y);

            return new MapCoord()
            {
                X = transformed[0],
                Y = transformed[1]
            };
        }

        /// <summary>
        /// Returns the associated coordinates you would see in-game if you opened the map and moved the cursor to this world position.
        /// </summary>
        public static MapCoord UIFromWorldCoord(WorldCoord coord)
        {
            var transformed = ApplyMatrix(GameConstants.WorldToMapMatrix, coord.X, coord.Y);

            return new MapCoord()
            {
                X = transformed[0],
                Y = transformed[1]
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
