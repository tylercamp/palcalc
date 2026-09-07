using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.ResultPruning;
using System.Numerics;

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
    private const int AttackSlotCount = AttackProfile.TargetMaskCount;

    private readonly ResultPruningRule retainedAlternativeSelection;
    private readonly IEffectivePropertiesKeyProvider propertiesKeyProvider;
    private readonly bool attackProfilesActive;

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

    internal bool SupportsCandidateDrafts =>
        ReferenceEquals(
            propertiesKeyProvider,
            DefaultEffectivePropertiesKeyProvider.Instance
        );

    internal EffectivePropertiesKey KeyOf(in CandidateDraft candidate) =>
        new(
            palId: candidate.Pal.Id,
            gender: candidate.Gender,
            passives: new PassiveSetKey(candidate.EffectivePassives),
            ivs: new RelevantIVKey(candidate.IVs)
        );

    /// <summary>
    /// Performs the cheap comparison used by workers within one iteration.
    ///
    /// Lower breeding effort is preferred by this early projection. When effort
    /// is equal, lower cost replaces the candidate used for later comparisons.
    /// Candidates tied on both values are both kept because their IVs or breeding
    /// paths may differ; the full simplification pass decides between them.
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
    /// simplification pass. A result classified as a guaranteed improvement is
    /// preferred by this projection only; retirement is deferred to the full
    /// frontier merge.
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

    internal FrontierCandidateAssessment AssessAgainstFrontier(
        in CandidateDraft candidate,
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

        comparison = TotalMaxIV(candidate.IVs).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return comparison > 0
                ? FrontierCandidateAssessment.PotentialImprovement
                : CoversAttackCapability(incumbent.AttackProfile, candidate.AttackProfile)
                    ? FrontierCandidateAssessment.Inferior
                    : FrontierCandidateAssessment.PotentialImprovement;

        return TotalMinIV(candidate.IVs) > TotalMinIV(incumbent)
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
        // Ordinary pruning may enforce a hard result limit. Preserve one best
        // provider for every exact mask capability even when that exceeds it.
        var exactChampions = new AttackCapability[AttackSlotCount];
        Span<bool> occupied = stackalloc bool[AttackSlotCount];
        foreach (var candidate in distinctCandidates)
        {
            foreach (ref readonly var entry in candidate.AttackProfile.EntriesSpan)
            {
                var capability = new AttackCapability(candidate, entry);
                var slot = entry.LearnedTargetMask;
                if (!occupied[slot] || CompareCapabilities(capability, exactChampions[slot]) < 0)
                {
                    exactChampions[slot] = capability;
                    occupied[slot] = true;
                }
            }
        }

        for (var slot = 0; slot < AttackSlotCount; slot++)
        {
            if (!occupied[slot])
                continue;
            var champion = exactChampions[slot].Candidate;
            if (!providers.Any(provider =>
                    CoversAttackEntry(
                        provider.AttackProfile.EntriesSpan,
                        exactChampions[slot].Entry
                    )))
                providers.Add(champion);
        }

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

        if ((provider.EntryTargetMasks & required.EntryTargetMasks) !=
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

    private static bool CoversAttackCapability(
        in PreparedAttackProfile provider,
        AttackProfile required
    )
    {
        if ((provider.EntryTargetMasks & required.EntryTargetMasks) !=
            required.EntryTargetMasks)
            return false;

        foreach (ref readonly var requiredEntry in required.EntriesSpan)
            if (provider.EntryForMask(requiredEntry.LearnedTargetMask).TotalSpecialCakes >
                requiredEntry.TotalSpecialCakes)
                return false;
        return true;
    }

    private static bool CoversAttackCapability(
        AttackProfile provider,
        in PreparedAttackProfile required
    )
    {
        if ((provider.EntryTargetMasks & required.EntryTargetMasks) !=
            required.EntryTargetMasks)
            return false;

        var masks = required.EntryTargetMasks;
        var providerEntries = provider.EntriesSpan;
        while (masks != 0)
        {
            var mask = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            if (!CoversAttackEntry(providerEntries, required.EntryForMask(mask)))
                return false;
        }
        return true;
    }

    private static bool CoversAttackEntry(
        ReadOnlySpan<AttackProfileEntry> providerEntries,
        in AttackProfileEntry requiredEntry
    )
    {
        // Profiles contain at most one champion for each exact mask.
        foreach (ref readonly var providerEntry in providerEntries)
        {
            if (providerEntry.LearnedTargetMask == requiredEntry.LearnedTargetMask &&
                providerEntry.TotalSpecialCakes <= requiredEntry.TotalSpecialCakes)
                return true;
        }

        return false;
    }

    private static int CompareCapabilities(in AttackCapability left, in AttackCapability right)
    {
        var comparison = left.Entry.TotalSpecialCakes.CompareTo(right.Entry.TotalSpecialCakes);
        if (comparison == 0)
            comparison = left.Entry.LearnedTargetMask.CompareTo(right.Entry.LearnedTargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Candidate.BreedingEffort.CompareTo(right.Candidate.BreedingEffort);
        if (comparison != 0) return comparison;
        comparison = left.Candidate.TotalCost.CompareTo(right.Candidate.TotalCost);
        if (comparison != 0) return comparison;
        return left.Candidate.GetHashCode().CompareTo(right.Candidate.GetHashCode());
    }

    // random IVs have no known value and score as zero
    private static int ScoreOf(IV_Value iv, Func<IV_Value, int> select) =>
        iv == IV_Value.Random ? 0 : select(iv);

    private static int TotalMaxIV(IPalReference candidate) =>
        TotalMaxIV(candidate.IVs);

    private static int TotalMaxIV(IV_Set ivs) =>
        ScoreOf(ivs.HP, iv => iv.Max) +
        ScoreOf(ivs.Attack, iv => iv.Max) +
        ScoreOf(ivs.Defense, iv => iv.Max);

    private static int TotalMinIV(IPalReference candidate) =>
        TotalMinIV(candidate.IVs);

    private static int TotalMinIV(IV_Set ivs) =>
        ScoreOf(ivs.HP, iv => iv.Min) +
        ScoreOf(ivs.Attack, iv => iv.Min) +
        ScoreOf(ivs.Defense, iv => iv.Min);

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
