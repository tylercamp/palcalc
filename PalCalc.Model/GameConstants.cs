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

        /*
         * Outstanding questions:
         * 
         * - What's the formula for how long breeding will take?
         * - What's the probability of wild pals having a specific gender?
         * - What's the probability of wild pals having exactly N passives?
         */

        public static readonly int MaxTotalPassives = 4;

        // (note: changing these from dictionaries to arrays has negligible impact on performance. dictionaries are more legible in this case)

        // === Inheritance probability tables ===
        //
        // Derived from the game's raw weight arrays in `PalGameSetting` (scraped by PalCalc.GenDB):
        //   Combi_TalentInheritNum    -> how many IVs are copied from parents
        //   Combi_PassiveInheritNum   -> how many passives are copied from parents
        //   Combi_PassiveRandomAddNum -> how many random passives are added
        //
        // A weight array [w0, w1, ...] over outcomes {c, c+1, ...} gives P(k) = w / sum(w).
        // e.g. Combi_TalentInheritNum = [3,2,1] -> inherit 1/2/3 IVs at 50% / 33.3% / 16.7%.
        //
        // The raw arrays are scraped into db.json and applied at load via ApplyScrapedInheritance().
        // The defaults below are the datamined values, used as a fallback if a db.json predates scraping.
        // (datamined from BP_PalGameSetting, Steam build 24181527, 2026-07)
        private static IReadOnlyList<int> talentInheritWeights = new[] { 3, 2, 1 };
        private static IReadOnlyList<int> passiveInheritWeights = new[] { 4, 3, 2, 1 };
        private static IReadOnlyList<int> passiveRandomAddWeights = new[] { 4, 3, 2, 1 };

        // probability of inheriting exactly N IVs from parents (always inherits at least 1)
        public static IReadOnlyDictionary<int, float> IVProbabilityDirect { get; private set; }
        // probability of ending up with N desired IVs, indexed [numDesired - 1]
        public static IReadOnlyList<float> IVDesiredProbabilities { get; private set; }

        // probability of inheriting exactly / at least N passives from the parent pool
        public static IReadOnlyDictionary<int, float> PassiveProbabilityDirect { get; private set; }
        public static IReadOnlyDictionary<int, float> PassiveProbabilityAtLeastN { get; private set; }
        // probability of adding exactly / at least N random passives
        public static IReadOnlyDictionary<int, float> PassiveRandomAddedProbability { get; private set; }
        public static IReadOnlyDictionary<int, float> PassiveRandomAddedAtLeastN { get; private set; }

        static GameConstants() => RecomputeInheritanceTables();

        /// <summary>
        /// Overrides the raw inheritance weight arrays with values scraped from game files
        /// (see PalCalc.GenDB.GameSettingReader). Null/empty arguments keep the datamined defaults.
        /// </summary>
        public static void ApplyScrapedInheritance(
            IReadOnlyList<int> talentInherit,
            IReadOnlyList<int> passiveInherit,
            IReadOnlyList<int> passiveRandomAdd)
        {
            if (talentInherit is { Count: > 0 }) talentInheritWeights = talentInherit;
            if (passiveInherit is { Count: > 0 }) passiveInheritWeights = passiveInherit;
            if (passiveRandomAdd is { Count: > 0 }) passiveRandomAddWeights = passiveRandomAdd;
            RecomputeInheritanceTables();
        }

        private static void RecomputeInheritanceTables()
        {
            // exact-count distributions
            IVProbabilityDirect = WithKey(DistFromWeights(talentInheritWeights, startCount: 1), 0);
            PassiveProbabilityDirect = WithKey(DistFromWeights(passiveInheritWeights, startCount: 1), 0);
            PassiveRandomAddedProbability = WithKey(DistFromWeights(passiveRandomAddWeights, startCount: 0), MaxTotalPassives);

            // cumulative "at least N"
            PassiveProbabilityAtLeastN = AtLeast(DistFromWeights(passiveInheritWeights, startCount: 1));
            PassiveRandomAddedAtLeastN = AtLeast(PassiveRandomAddedProbability);

            // IV desired-count probabilities: chance of getting `numDesired` specific IVs given the
            // inherited-count distribution. combinations table is fixed (there are 3 IV stats).
            var comb = new Dictionary<int, Dictionary<int, float>>()
            {
                { 1, new() { { 1, 1f / 3f }, { 2, 0f },      { 3, 0f } } },
                { 2, new() { { 1, 2f / 3f }, { 2, 1f / 3f }, { 3, 0f } } },
                { 3, new() { { 1, 1f },      { 2, 1f },      { 3, 1f } } },
            };
            var ivDesired = new float[3];
            for (int numInherited = 1; numInherited <= 3; numInherited++)
                for (int numDesired = 1; numDesired <= 3; numDesired++)
                    ivDesired[numDesired - 1] += IVProbabilityDirect[numInherited] * comb[numInherited][numDesired];
            IVDesiredProbabilities = ivDesired;
        }

        // weights[i] is the weight for outcome (startCount + i); P(outcome) = weight / sum
        private static Dictionary<int, float> DistFromWeights(IReadOnlyList<int> weights, int startCount)
        {
            float sum = weights.Sum();
            var d = new Dictionary<int, float>();
            for (int i = 0; i < weights.Count; i++)
                d[startCount + i] = weights[i] / sum;
            return d;
        }

        // ensure `key` exists (probability 0) so consumers can index it safely
        private static Dictionary<int, float> WithKey(Dictionary<int, float> dist, int key)
        {
            if (!dist.ContainsKey(key)) dist[key] = 0.0f;
            return dist;
        }

        // P(X >= k) for each k present in the exact-count distribution
        private static Dictionary<int, float> AtLeast(IReadOnlyDictionary<int, float> exact) =>
            exact.Keys.ToDictionary(k => k, k => exact.Where(kv => kv.Key >= k).Sum(kv => kv.Value));

        // roughly estimate time to catch a given pal
        public static TimeSpan TimeToCatch(Pal pal)
        {
            var minTime = TimeSpan.FromMinutes(3);

            // TODO - tweak
            var rarityModifier = Math.Max(0, (pal.Price - 1000)) / 100.0f + (pal.Id.IsVariant ? 5 : 0);
            return minTime + TimeSpan.FromMinutes(rarityModifier);
        }

        // https://www.reddit.com/r/Palworld/comments/1af9in7/passive_skill_inheritance_mechanics_in_breeding/
        // supposedly the child will always inherit at least 1 passive directly from a parent?

        /*
         * TODO - Could scrape some of this from game files - `BP_PalGameSetting`
              "Combi_TalentInheritNum": [
                3.0,
                2.0,
                1.0
              ],
              "Combi_PassiveInheritNum": [
                4.0,
                3.0,
                2.0,
                1.0
              ],
              "Combi_PassiveRandomAddNum": [
                4.0,
                3.0,
                2.0,
                1.0
              ],
        */

        // (PassiveProbabilityDirect / AtLeastN and PassiveRandomAdded* are now computed from the
        //  scraped weight arrays — see the inheritance tables section above)

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
