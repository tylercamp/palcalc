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
        Dictionary<int, int> ivInheritanceWeights,
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

        IVProbabilityDirect = NormFromWeights(0, 3, ivInheritanceWeights);
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

    [JsonProperty]
    public Dictionary<int, int> IVInheritanceWeights { get; set; }

    [JsonProperty]
    public Dictionary<int, int> PassiveInheritanceWeights { get; set; }

    [JsonProperty]
    public Dictionary<int, int> PassiveRandomWeights { get; set; }

    // direct probability helpers
    [JsonIgnore]
    public IReadOnlyDictionary<int, float> IVProbabilityDirect { get; }

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

    private readonly float[] ivDesiredProbabilities;

    /// <summary>
    /// Gets the chance that the inherited IV categories include all desired
    /// categories, before selecting between the two parents' values.
    /// </summary>
    public float ProbabilityOfInheritingDesiredIVs(int desiredIVCount)
    {
        if (desiredIVCount < 1 || desiredIVCount > MaxInheritedIVs)
            throw new ArgumentOutOfRangeException(nameof(desiredIVCount));

        return ivDesiredProbabilities[desiredIVCount - 1];
    }

    public TimeSpan TimeToCatch(Pal pal)
    {
        // TODO - tweak

        ArgumentNullException.ThrowIfNull(pal);

        var rarityModifier = Math.Max(0, pal.Price - 1000) / 100.0f + (pal.Id.IsVariant ? 5.0f : 0);

        return TimeSpan.FromMinutes(3) + TimeSpan.FromMinutes(rarityModifier);
    }

    private float[] BuildDesiredIVProbabilities()
    {
        // IV desired-count probabilities: chance of getting `numDesired` specific IVs given the
        // inherited-count distribution. combinations table is fixed (there are 3 IV stats).
        var combinationsProbabilityTable =
            new Dictionary<int, Dictionary<int, float>>
            {
                { 1, new() { { 1, 1f / 3f }, { 2, 0f },      { 3, 0f } } },
                { 2, new() { { 1, 2f / 3f }, { 2, 1f / 3f }, { 3, 0f } } },
                { 3, new() { { 1, 1f },      { 2, 1f },      { 3, 1f } } },
            };

        var result = new float[MaxInheritedIVs];
        for (var inherited = 1; inherited <= MaxInheritedIVs; inherited++)
        {
            for (var desired = 1; desired <= MaxInheritedIVs; desired++)
            {
                result[desired - 1] +=
                    IVProbabilityDirect[inherited] *
                    combinationsProbabilityTable[inherited][desired];
            }
        }

        return result;
    }
}
