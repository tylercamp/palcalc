﻿using System;
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
         * - What's the probability of wild pals having exactly N passives?
         */

        public static readonly int MaxTotalPassives = 4;

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
