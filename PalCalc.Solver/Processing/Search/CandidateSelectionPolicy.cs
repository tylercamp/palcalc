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
    private const int NoopSlotOffset = AttackProfile.TargetMaskCount;
    private const int AttackSlotCount = NoopSlotOffset * 2;
    private const int TargetMaskBits = NoopSlotOffset - 1;
    private const int ExhaustiveProviderMetricsThreshold = 32;
    private const int ProviderSampleRate = 64;
    private const int ProviderSampleMask = ProviderSampleRate - 1;

    [ThreadStatic]
    private static int providerSampleCounter;

    private static readonly ILogger logger = Log.ForContext<DefaultCandidateSelectionPolicy>();

    private readonly ResultPruningRule retainedAlternativeSelection;
    private readonly IEffectivePropertiesKeyProvider propertiesKeyProvider;
    private readonly bool attackProfilesActive;
    private long profileSelectionCalls;
    private long inputCandidates;
    private long preferredCandidates;
    private long retainedCandidates;
    private long profileEntries;
    private long championSlots;
    private long addedAttackProviders;
    private long providerSamples;
    private long sampledDirectExactRetainedCandidates;
    private long sampledCurrentRetainedCandidates;
    private int maxInputCandidates;
    private int maxPreferredCandidates;
    private int maxRetainedCandidates;
    private int maxProfileEntries;
    private int maxCandidateProfileEntries;
    private int maxChampionSlots;
    private int maxSampledCurrentRetainedCandidates;
    private int maxSampledDirectExactRetainedCandidates;
    private int maxSampledDirectExactIncrease;

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
            return CoversAttackCapability(candidate.AttackProfile, incumbent.AttackProfile)
                ? EarlyCandidateSelection.ReplaceIncumbent
                : EarlyCandidateSelection.KeepBoth;
        if (comparison > 0)
            return CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
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
            return CoversAttackCapability(candidate.AttackProfile, incumbent.AttackProfile)
                ? FrontierCandidateAssessment.GuaranteedImprovement
                : FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        if (comparison < 0)
            return FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = TotalMaxIV(candidate).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return comparison > 0
                ? FrontierCandidateAssessment.PotentialImprovement
                : CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
                    ? FrontierCandidateAssessment.Inferior
                    : FrontierCandidateAssessment.PotentialImprovement;

        return TotalMinIV(candidate) > TotalMinIV(incumbent)
            ? FrontierCandidateAssessment.PotentialImprovement
            : CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
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

        var providers = preferred.ToList();
        var preferredCount = providers.Count;
        // Ordinary pruning may enforce a hard result limit. Preserve one best
        // provider for every exact mask/noop capability, then let equal-or-better
        // supersets cover those slots. This can intentionally exceed that limit.
        var exactChampions = new AttackCapability[AttackSlotCount];
        Span<bool> occupied = stackalloc bool[AttackSlotCount];
        var sampleProviderCounts =
            distinctCandidates.Count >= ExhaustiveProviderMetricsThreshold ||
            ((++providerSampleCounter & ProviderSampleMask) == 0);
        HashSet<IPalReference> directExactRetainedSample = sampleProviderCounts
            ? new(preferred)
            : null;
        var entryCount = 0;
        var maxCandidateEntries = 0;
        foreach (var candidate in distinctCandidates)
        {
            var candidateEntryCount = candidate.AttackProfile.EntriesSpan.Length;
            entryCount += candidateEntryCount;
            maxCandidateEntries = Math.Max(maxCandidateEntries, candidateEntryCount);
            foreach (ref readonly var entry in candidate.AttackProfile.EntriesSpan)
            {
                var capability = new AttackCapability(candidate, entry);
                var slot = entry.LearnedTargetMask +
                    (candidate.AttackProfile.HasNoopAttack ? NoopSlotOffset : 0);
                if (!occupied[slot] || CompareCapabilities(capability, exactChampions[slot]) < 0)
                {
                    exactChampions[slot] = capability;
                    occupied[slot] = true;
                }
            }
        }

        var selectedSlots = 0;
        for (var requiredSlot = 0; requiredSlot < AttackSlotCount; requiredSlot++)
        {
            if (!occupied[requiredSlot])
                continue;

            directExactRetainedSample?.Add(exactChampions[requiredSlot].Candidate);

            AttackCapability best = default;
            var found = false;
            var requiredMask = requiredSlot & TargetMaskBits;
            var requiresNoop = requiredSlot >= NoopSlotOffset;
            for (var providerSlot = 0; providerSlot < AttackSlotCount; providerSlot++)
            {
                if (!occupied[providerSlot])
                    continue;
                var providerMask = providerSlot & TargetMaskBits;
                var hasNoop = providerSlot >= NoopSlotOffset;
                // Noop is an additional capability: a Pal that has one can act
                // like a Pal without one, but the reverse substitution is unsafe.
                if ((providerMask & requiredMask) != requiredMask || requiresNoop && !hasNoop)
                    continue;

                var capability = exactChampions[providerSlot];
                if (!found || CompareCapabilities(capability, best) < 0)
                {
                    best = capability;
                    found = true;
                }
            }

            if (!found)
                continue;
            selectedSlots++;
            if (!providers.Contains(best.Candidate))
                providers.Add(best.Candidate);
        }

        RecordAttackSelection(
            distinctCandidates.Count,
            preferred.Count,
            providers.Count,
            entryCount,
            maxCandidateEntries,
            selectedSlots,
            providers.Count - preferredCount,
            directExactRetainedSample?.Count,
            sampleProviderCounts ? providers.Count : null
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
        if (!attackProfilesActive)
            return true;

        return CoversAttackCapability(
            provider.AttackProfile,
            required.AttackProfile
        );
    }

    private static bool CoversAttackCapability(
        AttackProfile provider,
        AttackProfile required
    )
    {
        var requiredEntries = required.EntriesSpan;
        if (requiredEntries.IsEmpty)
            return true;

        if (!provider.HasNoopAttack && required.HasNoopAttack)
            return false;

        if ((provider.StructurallyCoveredTargetMasks & required.EntryTargetMasks) !=
            required.EntryTargetMasks)
            return false;

        // Unfolded `.All(CoversAttackEntry)`
        var providerEntries = provider.EntriesSpan;
        foreach (ref readonly var requiredEntry in requiredEntries)
        {
            if (!CoversAttackEntry(providerEntries, requiredEntry))
                return false;
        }

        return true;
    }

    private static bool CoversAttackEntry(
        ReadOnlySpan<AttackProfileEntry> providerEntries,
        in AttackProfileEntry requiredEntry
    )
    {
        // Unfolded `.Any(Covers)`
        foreach (ref readonly var providerEntry in providerEntries)
        {
            if (AttackProfileReducer.Covers(providerEntry, requiredEntry))
                return true;
        }

        return false;
    }

    private static int CompareCapabilities(in AttackCapability left, in AttackCapability right)
    {
        var comparison = AttackProfileEntryComparer.Instance.Compare(left.Entry, right.Entry);
        if (comparison != 0) return comparison;
        // Prefer a candidate that can champion more slots, reducing the number
        // of extra providers retained after the ordinary result limit.
        comparison = right.Candidate.AttackProfile.EntriesSpan.Length.CompareTo(
            left.Candidate.AttackProfile.EntriesSpan.Length
        );
        if (comparison != 0) return comparison;
        comparison = left.Candidate.BreedingEffort.CompareTo(right.Candidate.BreedingEffort);
        if (comparison != 0) return comparison;
        comparison = left.Candidate.TotalCost.CompareTo(right.Candidate.TotalCost);
        if (comparison != 0) return comparison;
        return left.Candidate.GetHashCode().CompareTo(right.Candidate.GetHashCode());
    }

    internal void LogAttackSelectionDiagnostics()
    {
        var calls = Interlocked.Read(ref profileSelectionCalls);
        if (!attackProfilesActive || calls == 0)
            return;

        logger.Debug(
            "Attack selection profile: calls={Calls}, candidates={InputCandidates}->{PreferredCandidates}->{RetainedCandidates}, profileEntries={ProfileEntries}, championSlots={ChampionSlots}, addedAttackProviders={AddedAttackProviders}, retainedIfExact=all>={ExhaustiveThreshold}+1/{ProviderSampleRate}small:{ProviderSamples}:{CurrentRetained}->{DirectExactRetained}, maxRetainedIfExact={MaxCurrentRetained}->{MaxDirectExactRetained}(+{MaxDirectExactIncrease}), maxGroup={MaxInputCandidates}->{MaxPreferredCandidates}->{MaxRetainedCandidates}, maxGroupProfileEntries={MaxProfileEntries}, maxCandidateProfileEntries={MaxCandidateProfileEntries}, maxChampionSlots={MaxChampionSlots}",
            calls,
            Interlocked.Read(ref inputCandidates),
            Interlocked.Read(ref preferredCandidates),
            Interlocked.Read(ref retainedCandidates),
            Interlocked.Read(ref profileEntries),
            Interlocked.Read(ref championSlots),
            Interlocked.Read(ref addedAttackProviders),
            ExhaustiveProviderMetricsThreshold,
            ProviderSampleRate,
            Interlocked.Read(ref providerSamples),
            Interlocked.Read(ref sampledCurrentRetainedCandidates),
            Interlocked.Read(ref sampledDirectExactRetainedCandidates),
            Volatile.Read(ref maxSampledCurrentRetainedCandidates),
            Volatile.Read(ref maxSampledDirectExactRetainedCandidates),
            Volatile.Read(ref maxSampledDirectExactIncrease),
            Volatile.Read(ref maxInputCandidates),
            Volatile.Read(ref maxPreferredCandidates),
            Volatile.Read(ref maxRetainedCandidates),
            Volatile.Read(ref maxProfileEntries),
            Volatile.Read(ref maxCandidateProfileEntries),
            Volatile.Read(ref maxChampionSlots)
        );
    }

    private void RecordAttackSelection(
        int inputCount,
        int preferredCount,
        int retainedCount,
        int entryCount,
        int maxCandidateEntries,
        int selectedSlotCount,
        int addedProviderCount,
        int? directExactRetainedCount,
        int? currentRetainedCount
    )
    {
        Interlocked.Increment(ref profileSelectionCalls);
        Interlocked.Add(ref inputCandidates, inputCount);
        Interlocked.Add(ref preferredCandidates, preferredCount);
        Interlocked.Add(ref retainedCandidates, retainedCount);
        Interlocked.Add(ref profileEntries, entryCount);
        Interlocked.Add(ref championSlots, selectedSlotCount);
        Interlocked.Add(ref addedAttackProviders, addedProviderCount);
        if (directExactRetainedCount is int directExactCount &&
            currentRetainedCount is int currentCount)
        {
            Interlocked.Increment(ref providerSamples);
            Interlocked.Add(ref sampledDirectExactRetainedCandidates, directExactCount);
            Interlocked.Add(ref sampledCurrentRetainedCandidates, currentCount);
            UpdateMax(ref maxSampledCurrentRetainedCandidates, currentCount);
            UpdateMax(ref maxSampledDirectExactRetainedCandidates, directExactCount);
            UpdateMax(ref maxSampledDirectExactIncrease, directExactCount - currentCount);
        }
        UpdateMax(ref maxInputCandidates, inputCount);
        UpdateMax(ref maxPreferredCandidates, preferredCount);
        UpdateMax(ref maxRetainedCandidates, retainedCount);
        UpdateMax(ref maxProfileEntries, entryCount);
        UpdateMax(ref maxCandidateProfileEntries, maxCandidateEntries);
        UpdateMax(ref maxChampionSlots, selectedSlotCount);
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
