using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class GameConstants
    {
        // TODO - Could parse cake effects from DA_BreedingItemEffectData
        // [{ TalentBonusMin, TalenBonusMax, MutationRateBonusPercent, CombiRankBonus, BreedCount, bInheritAllActiveSkills, PassiveInheritCountOverride }]

        // TODO - Could scrape from PalEggRankInfoArray? [{ PalRarity, EggScale, HatchingSpeedDivisionRate }]
        public static readonly Dictionary<EggSize, int> EggSizeMinRarity = new()
        {
            // couldn't find this info when scraping through game data, found by checking against https://paldb.cc/en/Eggs
            // and saw that Rarity correlates with egg size
            { EggSize.Normal, 0 },
            { EggSize.Large, 5 },
            { EggSize.Huge, 8 },
        };

        // Used for calculating map coords from world coords
        //
        // (these values are fetched from game files and output at the end of `PalCalc.GenDB.BuildDBProgram`)

        // transformation matrix converting world coords to in-game map UI coords (shown on bottom
        // left of in-game Map)
        public static readonly double[,] WorldToMapMatrix = new double[3, 3]
        {
            { -4.830223727277094E-07, 0.0021796738568829717, -344.193826581459 },
            { 0.0021779338609583232, 1.3843765562632747E-06, 269.9073674619908 },
            { 0, 0, 1 }
        };

        // transformation matrix converting world coords to normalized image coords within
        // the world map texture, multiply the resulting coord by image size to get appropriate
        // X/Y for placing things on the map image
        public static readonly double[,] WorldToImageMatrix = new double[3, 3]
        {
            { 5.853358785966763E-10, 6.942623697264833E-07, 0.49957354110764096 },
            { -6.900889463287533E-07, -3.9501572187562305E-10, 0.24117673696704256 },
            { 0, 0, 1 }
        };

        public static readonly int MaxTotalPassives = 4;
    }
}
