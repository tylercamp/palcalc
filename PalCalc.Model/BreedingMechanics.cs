using Newtonsoft.Json;
using System.Collections.Frozen;

namespace PalCalc.Model;

/// <summary>
/// Immutable game mechanics used to estimate breeding and capture effort.
/// A <see cref="PalDB"/> owns one instance so customized databases can coexist.
/// </summary>
public sealed class BreedingMechanics
{
    public const int MaxInheritedIVs = 3;
    private static int MaxPassiveSkills =>
        GameConstants.MaxTotalPassives;

    public static BreedingMechanics Default { get; } = CreateDefault();

    [JsonConstructor]
    public BreedingMechanics(
        IReadOnlyDictionary<int, float> ivProbabilityDirect,
        IReadOnlyDictionary<int, float> passiveProbabilityDirect,
        IReadOnlyDictionary<int, float> passiveRandomAddedProbability,
        IReadOnlyDictionary<int, float> passivesWildAtMostN,
        TimeSpan minimumCaptureTime,
        int capturePriceThreshold,
        float capturePricePointsPerMinute,
        float variantCaptureTimeBonusMinutes,
        IReadOnlyDictionary<int, float>
            passiveProbabilityNoRandom = null,
        IReadOnlyDictionary<int, float>
            passiveProbabilityAtLeastN = null,
        IReadOnlyDictionary<int, float>
            passiveProbabilityNoRandomAtLeastN = null,
        IReadOnlyDictionary<int, float>
            passiveRandomAddedAtLeastN = null
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

        if (minimumCaptureTime < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumCaptureTime));
        if (capturePricePointsPerMinute <= 0 || !float.IsFinite(capturePricePointsPerMinute))
            throw new ArgumentOutOfRangeException(nameof(capturePricePointsPerMinute));
        if (variantCaptureTimeBonusMinutes < 0 || !float.IsFinite(variantCaptureTimeBonusMinutes))
            throw new ArgumentOutOfRangeException(nameof(variantCaptureTimeBonusMinutes));

        MinimumCaptureTime = minimumCaptureTime;
        CapturePriceThreshold = capturePriceThreshold;
        CapturePricePointsPerMinute = capturePricePointsPerMinute;
        VariantCaptureTimeBonusMinutes = variantCaptureTimeBonusMinutes;

        PassiveProbabilityAtLeastN =
            passiveProbabilityAtLeastN is null
                ? Enumerable
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
                    )
                : CopyProbabilityTable(
                    nameof(passiveProbabilityAtLeastN),
                    passiveProbabilityAtLeastN,
                    Enumerable.Range(1, MaxPassiveSkills)
                );

        var noRandomProbability = PassiveRandomAddedProbability[0];
        PassiveProbabilityNoRandom =
            passiveProbabilityNoRandom is null
                ? Enumerable
                    .Range(1, MaxPassiveSkills)
                    .ToFrozenDictionary(
                        count => count,
                        count => count == MaxPassiveSkills
                            ? PassiveProbabilityDirect[count]
                            : PassiveProbabilityDirect[count] *
                                noRandomProbability
                    )
                : CopyProbabilityTable(
                    nameof(passiveProbabilityNoRandom),
                    passiveProbabilityNoRandom,
                    Enumerable.Range(1, MaxPassiveSkills)
                );
        PassiveProbabilityNoRandomAtLeastN =
            passiveProbabilityNoRandomAtLeastN is null
                ? Enumerable
                    .Range(1, MaxPassiveSkills)
                    .ToFrozenDictionary(
                        count => count,
                        count => count == MaxPassiveSkills
                            ? PassiveProbabilityAtLeastN[count]
                            : PassiveProbabilityAtLeastN[count] *
                                noRandomProbability
                    )
                : CopyProbabilityTable(
                    nameof(passiveProbabilityNoRandomAtLeastN),
                    passiveProbabilityNoRandomAtLeastN,
                    Enumerable.Range(1, MaxPassiveSkills)
                );
        PassiveRandomAddedAtLeastN =
            passiveRandomAddedAtLeastN is null
                ? Enumerable
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
                    )
                : CopyProbabilityTable(
                    nameof(passiveRandomAddedAtLeastN),
                    passiveRandomAddedAtLeastN,
                    Enumerable.Range(0, MaxPassiveSkills + 1)
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
    public TimeSpan MinimumCaptureTime { get; }

    [JsonProperty]
    public int CapturePriceThreshold { get; }

    [JsonProperty]
    public float CapturePricePointsPerMinute { get; }

    [JsonProperty]
    public float VariantCaptureTimeBonusMinutes { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveProbabilityNoRandom { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveProbabilityAtLeastN { get; }

    [JsonProperty]
    public IReadOnlyDictionary<int, float> PassiveProbabilityNoRandomAtLeastN { get; }

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

        var rarityModifier =
            Math.Max(0, pal.Price - CapturePriceThreshold) /
            CapturePricePointsPerMinute +
            (pal.Id.IsVariant ? VariantCaptureTimeBonusMinutes : 0);

        return MinimumCaptureTime + TimeSpan.FromMinutes(rarityModifier);
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
            passivesWildAtMostN: new Dictionary<int, float>
            {
                { 0, 0.2f },
                { 1, 0.4f },
                { 2, 0.6f },
                { 3, 0.8f },
                { 4, 1.0f },
            },
            minimumCaptureTime: TimeSpan.FromMinutes(3),
            capturePriceThreshold: 1000,
            capturePricePointsPerMinute: 100.0f,
            variantCaptureTimeBonusMinutes: 5.0f,
            passiveProbabilityNoRandom: new Dictionary<int, float>
            {
                { 4, 0.10f },
                { 3, 0.08f },
                { 2, 0.12f },
                { 1, 0.16f },
            },
            passiveProbabilityAtLeastN: new Dictionary<int, float>
            {
                { 4, 0.10f },
                { 3, 0.30f },
                { 2, 0.60f },
                { 1, 1.00f },
            },
            passiveProbabilityNoRandomAtLeastN:
                new Dictionary<int, float>
                {
                    { 4, 0.10f },
                    { 3, 0.12f },
                    { 2, 0.24f },
                    { 1, 0.40f },
                },
            passiveRandomAddedAtLeastN:
                new Dictionary<int, float>
                {
                    { 4, 0.0f },
                    { 3, 0.10f },
                    { 2, 0.3f },
                    { 1, 0.6f },
                    { 0, 1.0f },
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
