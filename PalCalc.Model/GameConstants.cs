using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class GameConstants
    {
        public static readonly int PlayerPartySize = 5;

        public static readonly int PalBox_GridWidth = 6;
        public static readonly int PalBox_GridHeight = 5;
        public static readonly int PalBox_SlotsPerTab = PalBox_GridWidth * PalBox_GridHeight;

        public static readonly int Base_GridWidth = 5;
        public static readonly int Base_GridHeight = 4;

        /*
         * Outstanding questions:
         * 
         * - What's the formula for how long breeding will take?
         * - What's the probability of wild pals having a specific gender?
         * - What's the probability of wild pals having exactly N traits?
         * - Do child pals ALWAYS inherit at least one of the parent's traits?
         */

        public static readonly int MaxTotalTraits = 4;

        // roughly estimate time to catch a given pal based on paldex num. and whether it's a pal variant rather than base pal
        public static TimeSpan TimeToCatch(Pal pal)
        {
            var minTime = TimeSpan.FromMinutes(3);

            // TODO - tweak
            return minTime + pal.Id.PalDexNo * TimeSpan.FromSeconds(2);
        }

        // https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/
        // supposedly the child will always inherit at least 1 trait directly from a parent?

        // probability of getting N traits from parent pool
        public static readonly IReadOnlyDictionary<int, float> TraitProbabilityDirect = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.20f },
            { 2, 0.30f },
            { 1, 0.40f },
        };

        // probability of getting N traits from parent pool without any random passives
        public static readonly IReadOnlyDictionary<int, float> TraitProbabilityNoRandom = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.08f },
            { 2, 0.12f },
            { 1, 0.16f },
        };

        public static readonly IReadOnlyDictionary<int, float> TraitProbabilityAtLeastN = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.30f },
            { 2, 0.60f },
            { 1, 1.00f },
        };

        public static readonly IReadOnlyDictionary<int, float> TraitProbabilityNoRandomAtLeastN = new Dictionary<int, float>()
        {
            { 4, 0.10f },
            { 3, 0.12f },
            { 2, 0.24f },
            { 1, 0.40f },
        };

        // probability of getting N additional random traits added
        public static readonly IReadOnlyDictionary<int, float> TraitRandomAddedProbability = new Dictionary<int, float>()
        {
            { 4, 0.0f },
            { 3, 0.10f },
            { 2, 0.20f },
            { 1, 0.30f },
            { 0, 0.40f },
        };

        // probability of a wild pal having, at most, N random traits
        // (assume equal probability of gaining anywhere from 0 through 4 random traits)
        // (20% chance of exactly N traits)
        public static readonly IReadOnlyDictionary<int, float> TraitWildAtMostN = new Dictionary<int, float>()
        {
            { 0, 0.2f },
            { 1, 0.4f },
            { 2, 0.6f },
            { 3, 0.8f },
            { 4, 1.0f },
        };
    }
}
