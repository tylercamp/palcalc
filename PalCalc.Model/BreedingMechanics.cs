using Newtonsoft.Json;
using System.Collections.Frozen;

namespace PalCalc.Model;

/// <summary>
/// Immutable game mechanics used to estimate breeding and capture effort.
/// </summary>
public sealed class BreedingMechanics
{
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

    public const int MaxInheritedIVs = 3;
    private static int MaxPassiveSkills =>
        GameConstants.MaxTotalPassives;

    public static BreedingMechanics Default { get; } = CreateDefault();

    [JsonConstructor]
    public BreedingMechanics(
        IReadOnlyDictionary<int, float> ivProbabilityDirect,
        IReadOnlyDictionary<int, float> passiveProbabilityDirect,
        IReadOnlyDictionary<int, float> passiveRandomAddedProbability,
        IReadOnlyDictionary<int, float> passivesWildAtMostN
    )
    {
        IVProbabilityDirect = CopyProbabilityTable(
            nameof(ivProbabilityDirect),
            ivProbabilityDirect,
            Enumerable.Range(0, MaxInheritedIVs + 1)
        );
        PassiveProbabilityDirect = CopyProbabilityTable(
            nameof(passiveProbabilityDirect),
            passiveProbabilityDirect,
            Enumerable.Range(0, MaxPassiveSkills + 1)
        );
        PassiveRandomAddedProbability = CopyProbabilityTable(
            nameof(passiveRandomAddedProbability),
            passiveRandomAddedProbability,
            Enumerable.Range(0, MaxPassiveSkills + 1)
        );
        PassivesWildAtMostN = CopyProbabilityTable(
            nameof(passivesWildAtMostN),
            passivesWildAtMostN,
            Enumerable.Range(0, MaxPassiveSkills + 1)
        );

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
    public IReadOnlyDictionary<int, float> IVProbabilityDirect { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveProbabilityDirect { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveRandomAddedProbability { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassivesWildAtMostN { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveProbabilityAtLeastN { get; }

    [JsonProperty]
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
        ArgumentNullException.ThrowIfNull(pal);

        var rarityModifier = Math.Max(0, pal.Price - 1000) / 100.0f + (pal.Id.IsVariant ? 5.0f : 0);

        return TimeSpan.FromMinutes(3) + TimeSpan.FromMinutes(rarityModifier);
    }

    /// <summary>
    /// Creates a distinct instance containing the mechanics used by the
    /// unmodified game.
    /// </summary>
    public static BreedingMechanics CreateDefault() =>
        new(
            ivProbabilityDirect: new Dictionary<int, float>
            {
                { 0, 0.0f },
                { 1, 0.5f },
                { 2, 0.25f },
                { 3, 0.25f },
            },
            passiveProbabilityDirect: new Dictionary<int, float>
            {
                { 4, 0.10f },
                { 3, 0.20f },
                { 2, 0.30f },
                { 1, 0.40f },
                { 0, 0.0f },
            },
            passiveRandomAddedProbability: new Dictionary<int, float>
            {
                { 4, 0.0f },
                { 3, 0.10f },
                { 2, 0.20f },
                { 1, 0.30f },
                { 0, 0.40f },
            },
            // probability of a wild pal having, at most, N random passives
            // (assume equal probability of gaining anywhere from 0 through 4 random passives)
            // (20% chance of exactly N passives)
            passivesWildAtMostN: new Dictionary<int, float>
            {
                { 0, 0.2f },
                { 1, 0.4f },
                { 2, 0.6f },
                { 3, 0.8f },
                { 4, 1.0f },
            }
        );

    private float[] BuildDesiredIVProbabilities()
    {
        var combinationsProbabilityTable =
            new Dictionary<int, Dictionary<int, float>>
            {
                {
                    1,
                    new()
                    {
                        { 1, 1.0f / 3.0f },
                        { 2, 0.0f },
                        { 3, 0.0f },
                    }
                },
                {
                    2,
                    new()
                    {
                        { 1, 2.0f / 3.0f },
                        { 2, 1.0f / 3.0f },
                        { 3, 0.0f },
                    }
                },
                {
                    3,
                    new()
                    {
                        { 1, 1.0f },
                        { 2, 1.0f },
                        { 3, 1.0f },
                    }
                },
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

    private static FrozenDictionary<int, float> CopyProbabilityTable(
        string parameterName,
        IReadOnlyDictionary<int, float> source,
        IEnumerable<int> requiredKeys
    )
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);

        foreach (var key in requiredKeys)
        {
            if (!source.ContainsKey(key))
                throw new ArgumentException(
                    $"Probability table must contain key {key}.",
                    parameterName
                );
        }

        foreach (var probability in source.Values)
        {
            if (
                probability < 0 ||
                probability > 1 ||
                !float.IsFinite(probability)
            )
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Probabilities must be finite values from zero through one."
                );
            }
        }

        return source.ToFrozenDictionary();
    }
}
