using PalCalc.Model;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.PalReference;
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// The narrow frontier operations required during candidate expansion.
/// </summary>
internal interface ICandidateFrontierView
{
    FrontierCandidateAssessment AssessCandidate(IPalReference candidate, EffectivePropertiesKey propertiesKey);
}

internal readonly record struct CandidatePreFilterResult(bool Accepted, bool IsRetained)
{
    public static CandidatePreFilterResult Rejected => default;
}

/// <summary>
/// Quickly filters candidates produced by parallel workers during one solver
/// iteration. The frontier later runs the full simplification pass.
/// </summary>
internal sealed class CandidatePreFilter
{
    private readonly PalSpecifier target;
    private readonly bool hasRequiredAttacks;
    private readonly TimeSpan maxEffort;
    private readonly BreedingSolverSettings settings;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly ICandidateFrontierView frontier;
    private readonly SurgeryFinalistAccumulator surgeryFinalists;
    private readonly FrozenDictionary<
        PalId,
        ConcurrentDictionary<EffectivePropertiesKey, EarlyCandidateGroup>
    > earlyCandidatesByPalId;
    private readonly ConcurrentDictionary<IPalReference, byte> terminalCandidates = new();

    public bool RetainsCostAlternatives { get; }

    public CandidatePreFilter(
        PalSpecifier target,
        TimeSpan maxEffort,
        ICandidateSelectionPolicy selectionPolicy,
        ICandidateFrontierView frontier,
        IEnumerable<PalId> palIds,
        AttackTargetContext attackTargets,
        BreedingSolverSettings settings,
        SurgeryFinalistAccumulator surgeryFinalists = null
    )
    {
        this.target = target;
        this.maxEffort = maxEffort;
        this.settings = settings;
        this.selectionPolicy = selectionPolicy;
        this.frontier = frontier;
        this.surgeryFinalists = surgeryFinalists;

        hasRequiredAttacks = attackTargets?.HasRequiredAttacks == true;
        RetainsCostAlternatives = hasRequiredAttacks || settings.SpecialCakePolicy == SpecialCakePolicy.AttacksAndPassives;

        earlyCandidatesByPalId = palIds.ToFrozenDictionary(
            id => id,
            _ => new ConcurrentDictionary<EffectivePropertiesKey, EarlyCandidateGroup>()
        );
    }

    public CandidatePreFilterResult TryAdd(IPalReference candidate)
    {
        if (candidate.BreedingEffort > maxEffort)
            return CandidatePreFilterResult.Rejected;

        var isTerminal = target.IsSatisfiedBy(candidate);
        // A completed result may be a poor future parent and be rejected below.
        // Preserve its best final path before applying frontier-oriented filters.
        var terminalRetained = isTerminal && RetainsCostAlternatives && RetainTerminal(candidate);
        var surgeryRetained = surgeryFinalists?.TryRetain(candidate) == true;

        var propertiesKey = selectionPolicy.KeyOf(candidate);
        var frontierAssessment = frontier.AssessCandidate(candidate, propertiesKey);
        if (frontierAssessment == FrontierCandidateAssessment.Inferior && !isTerminal)
            return surgeryRetained
                ? new(Accepted: false, IsRetained: true)
                : CandidatePreFilterResult.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var group = earlyCandidates.GetOrAdd(propertiesKey, _ => new(hasRequiredAttacks));
        var accepted = group.TryAdd(candidate, selectionPolicy, RetainsCostAlternatives);

        return accepted
            ? new(Accepted: true, IsRetained: true)
            : new(Accepted: false, IsRetained: terminalRetained || surgeryRetained);
    }

    public CandidatePreFilterResult TryAdd(ref CandidateDraft candidate)
    {
        if (
            selectionPolicy is not DefaultCandidateSelectionPolicy defaultPolicy ||
            !defaultPolicy.SupportsCandidateDrafts ||
            frontier is not SearchFrontier searchFrontier
        )
            return TryAdd(candidate.Materialize());

        if (candidate.BreedingEffort > maxEffort)
            return CandidatePreFilterResult.Rejected;

        var isTerminal = target.IsSatisfiedByIgnoringAttacks(
            candidate.Pal, candidate.Gender,
            candidate.IVs, candidate.EffectivePassives
        ) && target.SatisfiesAttackRequirement(candidate.AttackProfile);
        var terminalRetained = isTerminal && RetainTerminal(candidate.Materialize());
        var surgeryRetained = surgeryFinalists?.TryRetain(ref candidate) == true;

        var propertiesKey = defaultPolicy.KeyOf(candidate);
        var frontierAssessment = searchFrontier.AssessCandidate(candidate, propertiesKey, defaultPolicy);
        if (frontierAssessment == FrontierCandidateAssessment.Inferior && !isTerminal)
            return surgeryRetained
                ? new(Accepted: false, IsRetained: true)
                : CandidatePreFilterResult.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var group = earlyCandidates.GetOrAdd(propertiesKey, _ => new(hasRequiredAttacks));
        if (!group.TryAdd(ref candidate))
            return new(
                Accepted: false,
                IsRetained: terminalRetained || surgeryRetained
            );

        return new(Accepted: true, IsRetained: true);
    }

    public IReadOnlyList<IPalReference> TerminalCandidates => terminalCandidates.Keys.ToArray();

    public List<IPalReference> RetainedCostCandidates()
    {
        var retained = new List<IPalReference>();
        foreach (var candidatesByKey in earlyCandidatesByPalId.Values)
            foreach (var group in candidatesByKey.Values)
                group.AddRetainedCostCandidatesTo(retained);
        return retained;
    }

    private bool RetainTerminal(IPalReference candidate)
    {
        if (target.RequiredGender != PalGender.WILDCARD && candidate.Gender != target.RequiredGender)
        {
            if (candidate.Gender != PalGender.WILDCARD && !settings.UseGenderReversers)
                return false;
            candidate = candidate.WithGuaranteedGender(settings.DB, target.RequiredGender, settings.UseGenderReversers);
        }

        return terminalCandidates.TryAdd(candidate, 0);
    }

    private sealed class EarlyCandidateGroup(bool hasRequiredAttacks)
    {
        private readonly CandidateCostFrontier alternatives = new(hasRequiredAttacks, markReplacedOutdated: true);
        private IPalReference ordinaryIncumbent;

        public void AddRetainedCostCandidatesTo(List<IPalReference> destination)
        {
            lock (this)
                destination.AddRange(alternatives.Candidates);
        }

        public bool TryAdd(IPalReference candidate, ICandidateSelectionPolicy selectionPolicy, bool retainCostAlternatives)
        {
            lock (this)
                return retainCostAlternatives
                    ? alternatives.TryAdd(candidate)
                    : TryAddOrdinary(candidate, selectionPolicy);
        }

        public bool TryAdd(ref CandidateDraft candidate)
        {
            lock (this)
                return alternatives.TryAdd(ref candidate);
        }

        private bool TryAddOrdinary(IPalReference candidate, ICandidateSelectionPolicy selectionPolicy)
        {
            if (ordinaryIncumbent is null)
            {
                ordinaryIncumbent = candidate;
                return true;
            }

            switch (selectionPolicy.SelectEarlyCandidate(candidate, ordinaryIncumbent))
            {
                case EarlyCandidateSelection.RejectCandidate:
                    return false;
                case EarlyCandidateSelection.ReplaceIncumbent:
                    ordinaryIncumbent = candidate;
                    return true;
                default:
                    return true;
            }
        }

    }
}

/// <summary>
/// Immutable shared inputs for candidate expansion during one solver step.
/// </summary>
internal sealed record CandidateExpansionContext(
    int StepIndex,
    PalSpecifier Target,
    CandidatePreFilter PreFilter,
    AttackTargetContext AttackTargets
);
