using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.ResultPruning;

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
    EarlyCandidateSelection SelectEarlyCandidate(IPalReference candidate, IPalReference incumbent);

    FrontierCandidateAssessment AssessAgainstFrontier(IPalReference candidate, IPalReference incumbent);

    IReadOnlyList<IPalReference> SelectRetainedAlternatives(IEnumerable<IPalReference> candidates);

    /// <summary>Groups completed candidates by the policy's priorities and selects the final alternatives.</summary>
    IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates);

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
            .OrderBy(candidate => candidate, policy.ExpansionPriorityComparer)
            .ToList();
}

/// <summary>
/// Decides which breeding paths are worth retaining among candidates with the
/// same effective properties. Workers use its quick comparisons; the frontier
/// later applies its complete ordered simplification rules.
/// </summary>
internal sealed class DefaultCandidateSelectionPolicy : ICandidateSelectionPolicy
{
    private readonly ResultPruningRule retainedAlternativeSelection;
    private readonly IEffectivePropertiesKeyProvider propertiesKeyProvider;
    private readonly bool hasRequiredAttacks;
    private readonly bool preserveSpecialCakeOptions;

    public DefaultCandidateSelectionPolicy(
        ResultPruningPolicy resultPruning,
        CancellationToken cancellationToken,
        AttackTargetContext attackTargets,
        bool considerPassiveCakes,
        IEffectivePropertiesKeyProvider propertiesKeyProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(resultPruning);

        this.propertiesKeyProvider = propertiesKeyProvider ?? DefaultEffectivePropertiesKeyProvider.Instance;

        hasRequiredAttacks = attackTargets?.HasRequiredAttacks == true;
        preserveSpecialCakeOptions = hasRequiredAttacks || considerPassiveCakes;
        retainedAlternativeSelection = resultPruning.Create(cancellationToken);
    }

    public IComparer<IPalReference> ExpansionPriorityComparer { get; } =
        BreedingEffortComparer.Instance;

    public EffectivePropertiesKey KeyOf(IPalReference reference) =>
        propertiesKeyProvider.KeyOf(reference);

    internal bool SupportsCandidateDrafts =>
        ReferenceEquals(propertiesKeyProvider, DefaultEffectivePropertiesKeyProvider.Instance);

    internal EffectivePropertiesKey KeyOf(in CandidateDraft candidate) =>
        new(
            palId: candidate.Pal.Id, gender: candidate.Gender,
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
    public EarlyCandidateSelection SelectEarlyCandidate(IPalReference candidate, IPalReference incumbent)
    {
        var comparison = CompareScalarPreference(candidate, incumbent);
        if (comparison < 0)
            return CoversCandidate(candidate, incumbent)
                ? EarlyCandidateSelection.ReplaceIncumbent
                : EarlyCandidateSelection.KeepBoth;
        if (comparison > 0)
            return CoversCandidate(incumbent, candidate)
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
    public FrontierCandidateAssessment AssessAgainstFrontier(IPalReference candidate, IPalReference incumbent)
    {
        var comparison = candidate.BreedingEffort.CompareTo(incumbent.BreedingEffort);
        if (comparison < 0)
            return CoversCandidate(candidate, incumbent)
                ? FrontierCandidateAssessment.GuaranteedImprovement
                : FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversCandidate(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        if (comparison < 0)
            return FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversCandidate(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = TotalMaxIV(candidate).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return comparison > 0
                ? FrontierCandidateAssessment.PotentialImprovement
                : CoversCandidate(incumbent, candidate)
                    ? FrontierCandidateAssessment.Inferior
                    : FrontierCandidateAssessment.PotentialImprovement;

        return TotalMinIV(candidate) > TotalMinIV(incumbent) ||
            (preserveSpecialCakeOptions && TotalMinIV(candidate) == TotalMinIV(incumbent) && candidate.TotalRequiredLevels < incumbent.TotalRequiredLevels)
            ? FrontierCandidateAssessment.PotentialImprovement
            : CoversCandidate(incumbent, candidate)
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;
    }

    internal FrontierCandidateAssessment AssessAgainstFrontier(in CandidateDraft candidate, IPalReference incumbent)
    {
        var comparison = candidate.BreedingEffort.CompareTo(incumbent.BreedingEffort);
        if (comparison < 0)
            return CoversCandidate(
                candidate.AttackProfile, incumbent.AttackProfile,
                candidate.BaseSpecialCakes, incumbent.BaseSpecialCakes
            )
                ? FrontierCandidateAssessment.GuaranteedImprovement
                : FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversCandidate(
                incumbent.AttackProfile, candidate.AttackProfile,
                incumbent.BaseSpecialCakes, candidate.BaseSpecialCakes
            )
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        if (comparison < 0)
            return FrontierCandidateAssessment.PotentialImprovement;
        if (comparison > 0)
            return CoversCandidate(
                incumbent.AttackProfile, candidate.AttackProfile,
                incumbent.BaseSpecialCakes, candidate.BaseSpecialCakes
            )
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;

        comparison = TotalMaxIV(candidate.IVs).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return comparison > 0
                ? FrontierCandidateAssessment.PotentialImprovement
                : CoversCandidate(
                    incumbent.AttackProfile, candidate.AttackProfile,
                    incumbent.BaseSpecialCakes, candidate.BaseSpecialCakes
                )
                    ? FrontierCandidateAssessment.Inferior
                    : FrontierCandidateAssessment.PotentialImprovement;

        return TotalMinIV(candidate.IVs) > TotalMinIV(incumbent) ||
            (preserveSpecialCakeOptions && TotalMinIV(candidate.IVs) == TotalMinIV(incumbent) && candidate.TotalRequiredLevels < incumbent.TotalRequiredLevels)
            ? FrontierCandidateAssessment.PotentialImprovement
            : CoversCandidate(
                incumbent.AttackProfile, candidate.AttackProfile,
                incumbent.BaseSpecialCakes, candidate.BaseSpecialCakes
            )
                ? FrontierCandidateAssessment.Inferior
                : FrontierCandidateAssessment.PotentialImprovement;
    }

    public IReadOnlyList<IPalReference> SelectRetainedAlternatives(IEnumerable<IPalReference> candidates)
    {
        var distinctCandidates = candidates.Distinct().ToList();
        if (distinctCandidates.Count == 0)
            return distinctCandidates;

        var preferred = retainedAlternativeSelection
            .Apply(distinctCandidates, new CachedResultData(distinctCandidates))
            .ToList();
        if (!preserveSpecialCakeOptions)
            return preferred;

        // Preserve useful cake/effort tradeoffs even when the ordinary result
        // limit would keep only the fastest tier.
        var alternatives = new CandidateCostFrontier(hasRequiredAttacks, markReplacedOutdated: false);
        foreach (var candidate in distinctCandidates)
            alternatives.TryAdd(candidate);

        var providers = preferred.ToList();
        foreach (var candidate in alternatives.Candidates)
            if (!providers.Any(provider => CoversRetainedAlternative(provider, candidate)))
                providers.Add(candidate);

        return providers;
    }

    private bool CoversRetainedAlternative(IPalReference provider, IPalReference candidate) =>
        CoversCandidate(provider, candidate) &&
        // Existing pruning chooses within a cake tier. Across tiers, a cheaper
        // provider also needs to be as fast to replace this path.
        (provider.BaseSpecialCakes == candidate.BaseSpecialCakes || provider.BreedingEffort <= candidate.BreedingEffort);

    public BreedingEffortGroupKey BreedingEffortGroupOf(IPalReference candidate) =>
        new(candidate.BreedingEffort.Ticks);

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates)
    {
        var distinct = candidates.Distinct().ToList();
        if (!preserveSpecialCakeOptions)
            return distinct
                .GroupBy(BreedingEffortGroupOf)
                .SelectMany(SelectRetainedAlternatives);

        var comparison = new FinalizationComparison();
        return distinct
            .GroupBy(reference => (
                Outcome: comparison.KeyOf(reference),
                Effort: BreedingEffortGroupOf(reference),
                Cakes: reference.ExpectedSpecialCakes
            ))
            .SelectMany(SelectRetainedAlternatives);
    }

    private static int CompareScalarPreference(IPalReference candidate, IPalReference incumbent)
    {
        var comparison = candidate.BreedingEffort.CompareTo(incumbent.BreedingEffort);
        return comparison != 0
            ? comparison
            : candidate.TotalCost.CompareTo(incumbent.TotalCost);
    }

    private bool CoversCandidate(IPalReference provider, IPalReference required) =>
        CoversCandidate(
            provider.AttackProfile, required.AttackProfile,
            provider.BaseSpecialCakes, required.BaseSpecialCakes
        );

    private bool CoversCandidate(
        AttackProfile provider, AttackProfile required,
        int providerCakes, int requiredCakes
    ) =>
        providerCakes <= requiredCakes &&
        (!hasRequiredAttacks || (provider.EntryTargetMasks & required.EntryTargetMasks) == required.EntryTargetMasks);

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

    private sealed class BreedingEffortComparer : IComparer<IPalReference>
    {
        public static BreedingEffortComparer Instance { get; } = new();

        public int Compare(IPalReference left, IPalReference right) =>
            left.BreedingEffort.CompareTo(right.BreedingEffort);
    }
}
