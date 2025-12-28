using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.GenDB
{
    internal static class ManualFixes
    {
        public static Dictionary<string, string> ActiveSkillInternalNameOverrides = new Dictionary<string, string>()
        {
            // for some reason a bunch of attack skills come in with a numeric ID rather than a plaintext ID.
            // there are a couple dozen of those, but these are the only real skills I'm aware of. these mappings
            // may change between releases, so double-check their accuracy when re-running GenDB
            //
            // related: https://github.com/FabianFG/CUE4Parse/issues/284
            //
            { "269", "Unique_Yakushima_EyeTossin" },
            { "268", "Unique_Yakushima_SummonServant" },
            { "277", "Unique_YakushimaBoss001_Small_DemonEyeCharge" },
            { "275", "Unique_YakushimaMonster001_SlimePress_Dark" },
            { "274", "Unique_YakushimaMonster001_SlimePress_Fire" },
            { "272", "Unique_YakushimaMonster001_SlimePress_Leaf" },
            { "271", "Unique_YakushimaMonster001_SlimePress_Normal" },
            { "273", "Unique_YakushimaMonster001_SlimePress_Water" },
            { "276", "Unique_YakushimaMonster001_SlimePress_Rainbow" },
            { "278", "Unique_YakushimaMonster002_SwordCharge" },
            { "279", "Unique_YakushimaMonster003_BatCharge" },
            { "256", "Unique_PoseidonOrca_TorrentLaser" },
            { "255", "Unique_GhostAnglerfish_Fire_SweepBait_Fire" },
            { "283", "RockBeat" },
            { "284", "IceWall" },
            { "261", "Unique_LegendDeer_WarpPillarBurst" },
            { "266", "Unique_LegendDeer_RadiantWingRush" },
            { "267", "Unique_LegendDeer_RadiantPurge_Otomo" },
            { "281", "PredatorWave" },
            { "282", "PredatorLockon" },
            { "280", "PredatorBeam" },
        };
    }
}
