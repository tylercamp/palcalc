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
            // there are a couple dozen of those, but these are the only real skills I'm aware of
            { "259", "RockBeat" },
            { "260", "IceWall" },
            { "255", "Unique_YakushimaMonster003_BatCharge" }
        };
    }
}
