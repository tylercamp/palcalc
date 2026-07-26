using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver;

internal enum EarlyCandidateSelection
{
    RejectCandidate,
    ReplaceIncumbent,
    KeepBoth,
}

internal readonly record struct FrontierCandidateAssessment(
    bool IsImprovement,
    bool CanImmediatelyObsolete
);

internal readonly record struct ResultTierKey(long PrimaryValue);

internal interface ICandidateSelectionPolicy : IBreedingStateKeyProvider
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

    ResultTierKey ResultTierOf(IPalReference candidate);

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

internal sealed class DefaultCandidateSelectionPolicy : ICandidateSelectionPolicy
{
    private readonly ResultPruningRule retainedAlternativeSelection;
    private readonly IBreedingStateKeyProvider stateKeyProvider;

    public DefaultCandidateSelectionPolicy(
        ResultPruningPolicy resultPruning,
        CancellationToken cancellationToken,
        IBreedingStateKeyProvider stateKeyProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(resultPruning);

        this.stateKeyProvider =
            stateKeyProvider ??
            DefaultBreedingStateKeyProvider.Instance;
        retainedAlternativeSelection =
            resultPruning.Create(cancellationToken);
    }

    public IComparer<IPalReference> ExpansionPriorityComparer { get; } =
        BreedingEffortComparer.Instance;

    public BreedingStateKey KeyOf(IPalReference reference) =>
        stateKeyProvider.KeyOf(reference);

    /// <summary>
    /// Performs the cheap comparison used by workers within one iteration.
    ///
    /// Lower breeding effort is a safe primary-objective dominance decision.
    /// The cost tie-break and replacement of exact ties preserve the existing
    /// heuristic; cost is later than other rules in authoritative selection,
    /// so this comparison is an admission optimization rather than a proof of
    /// full dominance.
    /// </summary>
    public EarlyCandidateSelection SelectEarlyCandidate(
        IPalReference candidate,
        IPalReference incumbent
    )
    {
        var comparison = candidate.BreedingEffort.CompareTo(
            incumbent.BreedingEffort
        );
        if (comparison > 0)
            return EarlyCandidateSelection.RejectCandidate;
        if (comparison < 0)
            return EarlyCandidateSelection.ReplaceIncumbent;

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        return comparison > 0
            ? EarlyCandidateSelection.RejectCandidate
            : EarlyCandidateSelection.ReplaceIncumbent;
    }

    /// <summary>
    /// Performs the cheap comparison against the retained frontier.
    ///
    /// Cost and IV comparisons preserve the existing admission heuristic, but
    /// only strict improvement in the primary objective is safe to use for
    /// immediate obsolescence.
    /// </summary>
    public FrontierCandidateAssessment AssessAgainstFrontier(
        IPalReference candidate,
        IPalReference incumbent
    )
    {
        var comparison = candidate.BreedingEffort.CompareTo(
            incumbent.BreedingEffort
        );
        if (comparison != 0)
            return new(
                IsImprovement: comparison < 0,
                CanImmediatelyObsolete: comparison < 0
            );

        comparison = candidate.TotalCost.CompareTo(incumbent.TotalCost);
        if (comparison != 0)
            return new(
                IsImprovement: comparison < 0,
                CanImmediatelyObsolete: false
            );

        comparison = TotalMaxIV(candidate).CompareTo(TotalMaxIV(incumbent));
        if (comparison != 0)
            return new(
                IsImprovement: comparison > 0,
                CanImmediatelyObsolete: false
            );

        return new(
            IsImprovement:
                TotalMinIV(candidate) >
                TotalMinIV(incumbent),
            CanImmediatelyObsolete: false
        );
    }

    public IReadOnlyList<IPalReference> SelectRetainedAlternatives(
        IEnumerable<IPalReference> candidates
    )
    {
        var distinctCandidates = candidates.Distinct().ToList();
        if (distinctCandidates.Count == 0)
            return distinctCandidates;

        return retainedAlternativeSelection
            .Apply(
                distinctCandidates,
                new CachedResultData(distinctCandidates)
            )
            .ToList();
    }

    public ResultTierKey ResultTierOf(IPalReference candidate) =>
        new(candidate.BreedingEffort.Ticks);

    private static int TotalMaxIV(IPalReference candidate) =>
        candidate.IVs.HP.Max +
        candidate.IVs.Attack.Max +
        candidate.IVs.Defense.Max;

    private static int TotalMinIV(IPalReference candidate) =>
        candidate.IVs.HP.Min +
        candidate.IVs.Attack.Min +
        candidate.IVs.Defense.Min;

    private sealed class BreedingEffortComparer : IComparer<IPalReference>
    {
        public static BreedingEffortComparer Instance { get; } = new();

        public int Compare(IPalReference left, IPalReference right) =>
            left.BreedingEffort.CompareTo(right.BreedingEffort);
    }
}
