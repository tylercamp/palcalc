using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.ResultPruning;
using Serilog;

namespace PalCalc.Solver.Processing.Search;

// TODO - Conceptual simplifications
//
// This was structured to reflect and preserve the original solver logic, but
// there isn't a great distinction between "early candidate" and "frontier assessment"
// comparisons. In the future, we could move to the notion of "primary metrics"
// and "secondary metrics", which are expected to be consistent with the pruning
// rules applied by `SelectRetainedAlternatives`, but this will carry some
// behavioral changes which are outside the scope of the current refactor.

internal enum EarlyCandidateSelection
{
    RejectCandidate,
    ReplaceIncumbent,
    KeepBoth,
}

internal enum FrontierCandidateAssessment
{
    Inferior,
    PotentialImprovement,
    GuaranteedImprovement,
}

internal readonly record struct BreedingEffortGroupKey(long EffortTicks);

internal interface ICandidateSelectionPolicy : IEffectivePropertiesKeyProvider
{
    EarlyCandidateSelection SelectEarlyCandidate(
        IPalReference candidate,
        IPalReference incumbent
    );

    FrontierCandidateAssessment AssessAgainstFrontier(
        IPalReference candidate,
        IPalReference incumbent
    );

    IReadOnlyList<IPalReference> SelectRetainedAlternatives(
        IEnumerable<IPalReference> candidates
    );

    BreedingEffortGroupKey BreedingEffortGroupOf(IPalReference candidate);

    IComparer<IPalReference> ExpansionPriorityComparer { get; }
}

internal static class CandidateSelectionPolicyExtensions
{
    /// <summary>
    /// Orders candidates for expansion while preserving discovery order among
    /// candidates with equal priority.
    /// </summary>
    public static List<IPalReference> OrderForExpansion(
        this ICandidateSelectionPolicy policy,
        IEnumerable<IPalReference> candidates
    ) =>
        candidates
            .OrderBy(
                candidate => candidate,
                policy.ExpansionPriorityComparer
            )
            .ToList();
}

/// <summary>
/// Decides which breeding paths are worth retaining among candidates with the
/// same effective properties. Workers use its quick comparisons; the frontier
/// later applies its complete ordered simplification rules.
/// </summary>
internal sealed class DefaultCandidateSelectionPolicy : ICandidateSelectionPolicy
{
    private const int MaxStackAttackCapabilities = 256;

    private static readonly ILogger logger = Log.ForContext<DefaultCandidateSelectionPolicy>();

    private readonly ResultPruningRule retainedAlternativeSelection;
    private readonly IEffectivePropertiesKeyProvider propertiesKeyProvider;
    private readonly bool attackProfilesActive;
    private long profileSelectionCalls;
    private long inputCandidates;
    private long preferredCandidates;
    private long retainedCandidates;
    private long profileEntries;
    private long envelopeEntries;
    private long addedAttackProviders;
    private int maxInputCandidates;
    private int maxPreferredCandidates;
    private int maxRetainedCandidates;
    private int maxProfileEntries;
    private int maxEnvelopeEntries;

    public DefaultCandidateSelectionPolicy(
        ResultPruningPolicy resultPruning,
        CancellationToken cancellationToken,
        AttackTargetContext attackTargets,
        IEffectivePropertiesKeyProvider propertiesKeyProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(resultPruning);

        attackProfilesActive = attackTargets?.IsActive == true;
        this.propertiesKeyProvider =
            propertiesKeyProvider ?? DefaultEffectivePropertiesKeyProvider.Instance;
        retainedAlternativeSelection =
            resultPruning.Create(cancellationToken);
    }

    public IComparer<IPalReference> ExpansionPriorityComparer { get; } =
        BreedingEffortComparer.Instance;

    public EffectivePropertiesKey KeyOf(IPalReference reference) =>
        propertiesKeyProvider.KeyOf(reference);

    /// <summary>
    /// Performs the cheap comparison used by workers within one iteration.
    ///
    /// Lower breeding effort is a guaranteed improvement. When effort is equal,
    /// lower cost replaces the candidate used for later comparisons. Candidates
    /// tied on both values are both kept because their IVs or breeding paths may
    /// differ; the full simplification pass decides between them.
    /// </summary>
    public EarlyCandidateSelection SelectEarlyCandidate(
        IPalReference candidate,
        IPalReference incumbent
    )
    {
        var comparison = CompareScalarPreference(candidate, incumbent);
        if (comparison < 0)
            return CoversAttackCapability(candidate, incumbent)
                ? EarlyCandidateSelection.ReplaceIncumbent
                : EarlyCandidateSelection.KeepBoth;
        if (comparison > 0)
            return CoversAttackCapability(incumbent, candidate)
                ? EarlyCandidateSelection.RejectCandidate
                : EarlyCandidateSelection.KeepBoth;

        return EarlyCandidateSelection.KeepBoth;
    }

    /// <summary>
    /// Performs the cheap comparison against the retained frontier.
    ///
    /// Lower cost or better IVs make a candidate worth sending to the full
    /// simplification pass. Only lower breeding effort guarantees that matching
    /// frontier candidates can be marked as outdated immediately.
    /// </summary>
    public FrontierCandidateAssessment AssessAgainstFrontier(
        IPalReference candidate,
        IPalReference incumbent
    )
    {
        var comparison = candidate.BreedingEffort.CompareTo(incumbent.BreedingEffort);
        if (comparison < 0)
            return CoversAttackCapability(candidate, incumbent)
                ? FrontierCandidateAssessment.GuaranteedImprovement
                : FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversAttackCapability(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        if (comparison < 0)
            return FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversAttackCapability(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = TotalMaxIV(candidate).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return comparison > 0
                ? FrontierCandidateAssessment.PotentialImprovement
                : CoversAttackCapability(incumbent, candidate)
                    ? FrontierCandidateAssessment.Inferior
                    : FrontierCandidateAssessment.PotentialImprovement;

        return TotalMinIV(candidate) > TotalMinIV(incumbent)
            ? FrontierCandidateAssessment.PotentialImprovement
            : CoversAttackCapability(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;
    }

    public IReadOnlyList<IPalReference> SelectRetainedAlternatives(
        IEnumerable<IPalReference> candidates
    )
    {
        var distinctCandidates = candidates.Distinct().ToList();
        if (distinctCandidates.Count == 0)
            return distinctCandidates;

        var preferred = retainedAlternativeSelection
            .Apply(
                distinctCandidates,
                new CachedResultData(distinctCandidates)
            )
            .ToList();
        if (!attackProfilesActive)
            return preferred;

        // TODO - This is a bit of a hackfix, should have a more cohesive approach

        // Ordinary pruning may apply a hard result limit. Add back the providers
        // required to preserve the group's nondominated attack envelope, even if
        // that makes an attack-sensitive group exceed the usual limit.
        // TODO: Generalize this seam if non-attack realization profiles are added.
        var providers = preferred.ToList();
        var orderedCandidates = distinctCandidates
            .OrderBy(candidate => candidate, ExpansionPriorityComparer)
            .ThenBy(candidate => candidate.GetHashCode())
            .ToList();
        var envelope = BuildAttackEnvelope(orderedCandidates, out var entryCount);
        var addedProviderCount = 0;
        foreach (var required in envelope)
        {
            if (providers.Any(provider => CoversAttackEntry(provider, required.Candidate, required.Entry)))
                continue;

            providers.Add(orderedCandidates.First(provider =>
                CoversAttackEntry(provider, required.Candidate, required.Entry)
            ));
            addedProviderCount++;
        }

        RecordAttackSelection(
            distinctCandidates.Count,
            preferred.Count,
            providers.Count,
            entryCount,
            envelope.Count,
            addedProviderCount
        );

        return providers;
    }

    public BreedingEffortGroupKey BreedingEffortGroupOf(IPalReference candidate) =>
        new(candidate.BreedingEffort.Ticks);

    private static int CompareScalarPreference(
        IPalReference candidate,
        IPalReference incumbent
    )
    {
        var comparison = candidate.BreedingEffort.CompareTo(incumbent.BreedingEffort);
        return comparison != 0
            ? comparison
            : candidate.TotalCost.CompareTo(incumbent.TotalCost);
    }

    private bool CoversAttackCapability(
        IPalReference provider,
        IPalReference required
    )
    {
        if (!attackProfilesActive) return true;

        // Unfolded `.All()`
        foreach (var entry in required.AttackProfile.EntriesSpan)
        {
            if (!CoversAttackEntry(provider, required, entry))
                return false;
        }

        return true;
    }

    private static bool CoversAttackEntry(
        IPalReference provider,
        IPalReference required,
        AttackProfileEntry requiredEntry
    )
    {
        if (!provider.AttackProfile.HasNoopAttack && required.AttackProfile.HasNoopAttack)
            return false;

        // Unfolded `.Any()`
        foreach (var providerEntry in provider.AttackProfile.EntriesSpan)
        {
            if (AttackProfileReducer.Covers(providerEntry, requiredEntry))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the profile entries which are not covered by another candidate in
    /// the same structural group. Mutually equivalent entries use stable input
    /// order so only one representative is required.
    /// </summary>
    private IReadOnlyList<AttackCapability> BuildAttackEnvelope(
        IReadOnlyList<IPalReference> candidates,
        out int capabilityCount
    )
    {
        capabilityCount = 0;
        foreach (var candidate in candidates)
            capabilityCount += candidate.AttackProfile.EntriesSpan.Length;

        Span<bool> covered = capabilityCount <= MaxStackAttackCapabilities
            ? stackalloc bool[capabilityCount]
            : new bool[capabilityCount];

        var requiredOffset = 0;
        for (var requiredCandidateIndex = 0;
             requiredCandidateIndex < candidates.Count;
             requiredCandidateIndex++)
        {
            var required = candidates[requiredCandidateIndex];
            var requiredEntries = required.AttackProfile.EntriesSpan;
            for (var providerCandidateIndex = 0;
                 providerCandidateIndex < candidates.Count;
                 providerCandidateIndex++)
            {
                if (providerCandidateIndex == requiredCandidateIndex)
                    continue;

                var provider = candidates[providerCandidateIndex];
                if (providerCandidateIndex > requiredCandidateIndex &&
                    CoversAttackCapability(required, provider))
                    continue;

                for (var entryIndex = 0; entryIndex < requiredEntries.Length; entryIndex++)
                {
                    if (!covered[requiredOffset + entryIndex] &&
                        CoversAttackEntry(provider, required, requiredEntries[entryIndex]))
                        covered[requiredOffset + entryIndex] = true;
                }
            }

            requiredOffset += requiredEntries.Length;
        }

        var envelope = new List<AttackCapability>();
        var capabilityIndex = 0;
        foreach (var candidate in candidates)
        foreach (var entry in candidate.AttackProfile.EntriesSpan)
        {
            if (!covered[capabilityIndex++])
                envelope.Add(new AttackCapability(candidate, entry));
        }

        return envelope;
    }

    internal void LogAttackSelectionDiagnostics()
    {
        var calls = Interlocked.Read(ref profileSelectionCalls);
        if (!attackProfilesActive || calls == 0)
            return;

        logger.Debug(
            "Attack selection profile: calls={Calls}, candidates={InputCandidates}->{PreferredCandidates}->{RetainedCandidates}, profileEntries={ProfileEntries}, envelopeEntries={EnvelopeEntries}, addedAttackProviders={AddedAttackProviders}, maxGroup={MaxInputCandidates}->{MaxPreferredCandidates}->{MaxRetainedCandidates}, maxProfileEntries={MaxProfileEntries}, maxEnvelopeEntries={MaxEnvelopeEntries}",
            calls,
            Interlocked.Read(ref inputCandidates),
            Interlocked.Read(ref preferredCandidates),
            Interlocked.Read(ref retainedCandidates),
            Interlocked.Read(ref profileEntries),
            Interlocked.Read(ref envelopeEntries),
            Interlocked.Read(ref addedAttackProviders),
            Volatile.Read(ref maxInputCandidates),
            Volatile.Read(ref maxPreferredCandidates),
            Volatile.Read(ref maxRetainedCandidates),
            Volatile.Read(ref maxProfileEntries),
            Volatile.Read(ref maxEnvelopeEntries)
        );
    }

    private void RecordAttackSelection(
        int inputCount,
        int preferredCount,
        int retainedCount,
        int entryCount,
        int envelopeCount,
        int addedProviderCount
    )
    {
        Interlocked.Increment(ref profileSelectionCalls);
        Interlocked.Add(ref inputCandidates, inputCount);
        Interlocked.Add(ref preferredCandidates, preferredCount);
        Interlocked.Add(ref retainedCandidates, retainedCount);
        Interlocked.Add(ref profileEntries, entryCount);
        Interlocked.Add(ref envelopeEntries, envelopeCount);
        Interlocked.Add(ref addedAttackProviders, addedProviderCount);
        UpdateMax(ref maxInputCandidates, inputCount);
        UpdateMax(ref maxPreferredCandidates, preferredCount);
        UpdateMax(ref maxRetainedCandidates, retainedCount);
        UpdateMax(ref maxProfileEntries, entryCount);
        UpdateMax(ref maxEnvelopeEntries, envelopeCount);
    }

    private static void UpdateMax(ref int maximum, int value)
    {
        var current = Volatile.Read(ref maximum);
        while (value > current)
        {
            var previous = Interlocked.CompareExchange(ref maximum, value, current);
            if (previous == current)
                return;
            current = previous;
        }
    }

    // random IVs have no known value and score as zero
    private static int ScoreOf(IV_Value iv, Func<IV_Value, int> select) =>
        iv == IV_Value.Random ? 0 : select(iv);

    private static int TotalMaxIV(IPalReference candidate) =>
        ScoreOf(candidate.IVs.HP, iv => iv.Max) +
        ScoreOf(candidate.IVs.Attack, iv => iv.Max) +
        ScoreOf(candidate.IVs.Defense, iv => iv.Max);

    private static int TotalMinIV(IPalReference candidate) =>
        ScoreOf(candidate.IVs.HP, iv => iv.Min) +
        ScoreOf(candidate.IVs.Attack, iv => iv.Min) +
        ScoreOf(candidate.IVs.Defense, iv => iv.Min);

    private readonly record struct AttackCapability(
        IPalReference Candidate,
        AttackProfileEntry Entry
    );

    private sealed class BreedingEffortComparer : IComparer<IPalReference>
    {
        public static BreedingEffortComparer Instance { get; } = new();

        public int Compare(IPalReference left, IPalReference right) =>
            left.BreedingEffort.CompareTo(right.BreedingEffort);
    }
}
