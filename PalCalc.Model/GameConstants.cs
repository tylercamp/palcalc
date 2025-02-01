using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class GameConstants
    {
        // Used for calculating map coords from world coords
        //
        // (these values are fetched from game files and output at the end of `PalCalc.GenDB.BuildDBProgram`)

        // transformation matrix converting world coords to in-game map UI coords (shown on bottom
        // left of in-game Map)
        public static readonly double[,] WorldToMapMatrix = new double[3, 3]
        {
            { -3.79519292446884E-07, 0.0021798096874746462, -344.34052549588256 },
            { 0.0021767405112596513, -9.438090431935445E-07, 270.1383799358495 },
            { 0, 0, 1 },
        };

        // transformation matrix converting world coords to normalized image coords within
        // the world map texture, multiply the resulting coord by image size to get appropriate
        // X/Y for placing things on the map image
        public static readonly double[,] WorldToImageMatrix = new double[3, 3]
        {
            { 1.159760335217757E-09, 6.928117576533099E-07, 0.5102813537826183 },
            { -6.890618703631407E-07, 6.348508350224871E-10, 0.30963301697476875 },
            { 0, 0, 1 },
        };

        /*
         * Outstanding questions:
         * 
         * - What's the formula for how long breeding will take?
         * - What's the probability of wild pals having a specific gender?
         * - What's the probability of wild pals having exactly N passives?
         */

        public static readonly int MaxTotalPassives = 4;

        // (note: changing these from dictionaries to arrays has negligible impact on performance. dictionaries are more legible in this case)

        // probability of inheriting exactly N IVs from parents
        public static readonly Dictionary<int, float> IVProbabilityDirect = new()
        {
            // will always inherit at least 1
            { 0, 0.0f },
            // (determined manually by gathering samples, unlike passive probabilities which were reverse engineered)
            { 1, 0.5f },
            { 2, 0.25f },
            { 3, 0.25f },
        };

        // roughly estimate time to catch a given pal
        public static TimeSpan TimeToCatch(Pal pal)
        {
            var minTime = TimeSpan.FromMinutes(3);

            // TODO - tweak
            var rarityModifier = (pal.Price - 1000) / 100.0f + (pal.Id.IsVariant ? 5 : 0);
            return minTime + TimeSpan.FromMinutes(rarityModifier);
        }

        // https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/
        // supposedly the child will always inherit at least 1 passive directly from a parent?

        // probability of getting N passives from parent pool
        public static readonly IReadOnlyDictionary<int, float> PassiveProbabilityDirect = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.20f },
            { 2, 0.30f },
            { 1, 0.40f },
            { 0, 0.0f },
        };

        // probability of getting N passives from parent pool without any random passives
        public static readonly IReadOnlyDictionary<int, float> PassiveProbabilityNoRandom = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.08f },
            { 2, 0.12f },
            { 1, 0.16f },
        };

        public static readonly IReadOnlyDictionary<int, float> PassiveProbabilityAtLeastN = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.30f },
            { 2, 0.60f },
            { 1, 1.00f },
        };

        public static readonly IReadOnlyDictionary<int, float> PassiveProbabilityNoRandomAtLeastN = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.12f },
            { 2, 0.24f },
            { 1, 0.40f },
        };

        // probability of getting N additional random passives added
        public static readonly IReadOnlyDictionary<int, float> PassiveRandomAddedProbability = new Dictionary<int, float>()
        {
            { 4, 0.0f },
            { 3, 0.10f },
            { 2, 0.20f },
            { 1, 0.30f },
            { 0, 0.40f },
        };

        public static readonly IReadOnlyDictionary<int, float> PassiveRandomAddedAtLeastN = new Dictionary<int, float>()
        {
            { 4, 0.0f },
            { 3, 0.10f },
            { 2, 0.3f },
            { 1, 0.6f },
            { 0, 1.0f }
        };

        // probability of a wild pal having, at most, N random passives
        // (assume equal probability of gaining anywhere from 0 through 4 random passives)
        // (20% chance of exactly N passives)
        public static readonly IReadOnlyDictionary<int, float> PassivesWildAtMostN = new Dictionary<int, float>()
        {
            { 0, 0.2f },
            { 1, 0.4f },
            { 2, 0.6f },
            { 3, 0.8f },
            { 4, 1.0f },
        };
    }
}
