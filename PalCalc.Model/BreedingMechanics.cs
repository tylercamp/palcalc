using Newtonsoft.Json;
using System.Collections.Frozen;

namespace PalCalc.Model;

public sealed class BreedingMechanics
{
    /*
    * Outstanding questions:
    * 
    * - What's the formula for how long breeding will take?
    * - What's the probability of wild pals having a specific gender?
    * - What's the probability of wild pals having exactly N passives?
    */

    public const int MaxInheritedIVs = 3;
    private static int MaxPassiveSkills =>
        GameConstants.MaxTotalPassives;

    [JsonConstructor]
    public BreedingMechanics(
        int[] ivInheritanceWeights,
        Dictionary<int, int> passiveInheritanceWeights,
        Dictionary<int, int> passiveRandomWeights
    )
    {
        IVInheritanceWeights = ivInheritanceWeights;
        PassiveInheritanceWeights = passiveInheritanceWeights;
        PassiveRandomWeights = passiveRandomWeights;

        static FrozenDictionary<int, float> NormFromWeights(int minKey, int maxKey, Dictionary<int, int> weights)
        {
            // (keys not in weights are kept as 0 probabilities)
            var sum = weights.Sum(i => i.Value);
            return Enumerable
                .Range(minKey, maxKey - minKey + 1)
                .ToFrozenDictionary(
                    k => k,
                    k => weights.TryGetValue(k, out int value) ? value / (float)sum : 0
                );
        }

        PassiveProbabilityDirect = NormFromWeights(0, 4, passiveInheritanceWeights);
        PassiveRandomAddedProbability = NormFromWeights(0, 4, passiveRandomWeights);

        // probability of a wild pal having, at most, N random passives
        // (not scrapable - assume equal probability of gaining anywhere from 0 through 4 random passives)
        // (20% chance of exactly N passives)
        PassivesWildAtMostN = new Dictionary<int, float>
        {
            { 0, 0.2f },
            { 1, 0.4f },
            { 2, 0.6f },
            { 3, 0.8f },
            { 4, 1.0f },
        };

        PassiveProbabilityAtLeastN =
            Enumerable
                .Range(1, MaxPassiveSkills)
                .ToFrozenDictionary(
                    count => count,
                    count => Enumerable
                        .Range(
                            count,
                            MaxPassiveSkills - count + 1
                        )
                        .Sum(inherited =>
                            PassiveProbabilityDirect[inherited]
                        )
                );

        PassiveRandomAddedAtLeastN =
            Enumerable
                .Range(0, MaxPassiveSkills + 1)
                .ToFrozenDictionary(
                    count => count,
                    count => Enumerable
                        .Range(
                            count,
                            MaxPassiveSkills - count + 1
                        )
                        .Sum(added =>
                            PassiveRandomAddedProbability[added]
                        )
                );

        ivDesiredProbabilities = BuildDesiredIVProbabilities();
    }

    /// <summary>
    /// Raw IV weights used by the inheritance algorithm;
    /// see `../PalCalc.Solver/README-PALWORLD-MECHANICS.md#iv-source-selection`
    /// and impl. comments below
    /// </summary>
    [JsonProperty]
    public int[] IVInheritanceWeights { get; set; }

    [JsonProperty]
    public Dictionary<int, int> PassiveInheritanceWeights { get; set; }

    [JsonProperty]
    public Dictionary<int, int> PassiveRandomWeights { get; set; }

    // direct probability helpers

    [JsonIgnore]
    public IReadOnlyDictionary<int, float> PassiveProbabilityDirect { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<int, float> PassiveRandomAddedProbability { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<int, float> PassivesWildAtMostN { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<int, float> PassiveProbabilityAtLeastN { get; }

    [JsonIgnore]
    public IReadOnlyDictionary<int, float> PassiveRandomAddedAtLeastN { get; }

    /// <summary>
    /// Inheritance probabilities depend on the specific IVs requested (HP, ATK, and/or DEF)
    /// and are asymmetric (e.g., HP is more likely to be inherited than any other stat).
    /// (See `../PalCalc.Solver/README-PALWORLD-MECHANICS.md#iv-source-selection`)
    ///
    /// This array covers the final probabilities of individual inheritance outcomes
    /// (e.g., HP, HP+ATK, DEF, DEF+HP, etc.) where index Bit0 = HP, Bit1 = ATK, Bit2 = DEF
    /// </summary>
    private readonly float[] ivDesiredProbabilities;

    /// <summary>
    /// Gets the chance that the parent-sourced stat set includes all requested
    /// stats. (Not including each roll for choosing the "correct" parent for a stat.)
    /// </summary>
    public float ProbabilityOfInheritingDesiredIVs(bool hp, bool attack, bool defense) =>
        ivDesiredProbabilities[(hp ? 1 : 0) | (attack ? 2 : 0) | (defense ? 4 : 0)];

    public TimeSpan TimeToCatch(Pal pal)
    {
        // TODO - tweak

        ArgumentNullException.ThrowIfNull(pal);

        var rarityModifier = Math.Max(0, pal.Price - 1000) / 100.0f + (pal.Id.IsVariant ? 5.0f : 0);

        return TimeSpan.FromMinutes(3) + TimeSpan.FromMinutes(rarityModifier);
    }

    private float[] BuildDesiredIVProbabilities()
    {
        const int hp = 1, attack = 2, defense = 4;

        // see `../PalCalc.Solver/README-PALWORLD-MECHANICS.md#iv-source-selection`
        /*
        Palworld's IV inheritance logic is essentially:

        switch (weighted_random(Combi_TalentInheritNum))
        {
            case 0 => random_choice(HP, ATK, DEF)
            case 1 => random_choice(HP+ATK+DEF, HP+DEF, HP+ATK)
            case 2 => HP+ATK+DEF
        }

        (... indicating which stats get pulled from parents; unselected stats are set to random())
        */

        int[][] parentSourceSetsBySelectorIndex =
        [
            [hp, attack, defense],
            [hp | attack | defense, hp | defense, hp | attack],
            [hp | attack | defense],
        ];
        var totalWeight = IVInheritanceWeights.Sum();
        var result = new float[8]; // 7 unordered combinations, +1 for "no relevant IVs"

        // `0` means no HP, ATK, or DEF IVs to inherit:
        // we don't care about inheritance, 100% "success" regardless of outcome
        result[0] = 1f;

        // go through each desired outcome
        for (var outcomeBits = 1; outcomeBits < result.Length; outcomeBits++)
            // each different way of achieving that outcome increases the odds of success
            // (go through each selectable branch (`selectorIndex`), then each sub-outcome in the branches (`.Count(..) / .Length`))
            for (var selectorIndex = 0; selectorIndex < parentSourceSetsBySelectorIndex.Length; selectorIndex++)
            {
                var selectorProbability = IVInheritanceWeights[selectorIndex] / (float)totalWeight;
                result[outcomeBits] += selectorProbability *
                    parentSourceSetsBySelectorIndex[selectorIndex].Count(selected => (selected & outcomeBits) == outcomeBits) /
                    parentSourceSetsBySelectorIndex[selectorIndex].Length;
            }

        return result;
    }
}
