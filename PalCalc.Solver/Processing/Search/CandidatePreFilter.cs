using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// The narrow frontier operations required during candidate expansion.
/// </summary>
internal interface ICandidateFrontierView
{
    FrontierCandidateAssessment AssessCandidate(
        IPalReference candidate,
        EffectivePropertiesKey propertiesKey
    );

    void MarkCandidatesOutdated(EffectivePropertiesKey propertiesKey);
}

internal readonly record struct CandidatePreFilterResult(
    EffectivePropertiesKey PropertiesKey,
    bool Accepted,
    bool IsGuaranteedImprovement
)
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
    private readonly TimeSpan maxEffort;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly ICandidateFrontierView frontier;
    private readonly FrozenDictionary<
        PalId,
        ConcurrentDictionary<EffectivePropertiesKey, IPalReference>
    > earlyCandidatesByPalId;

    public CandidatePreFilter(
        PalSpecifier target,
        TimeSpan maxEffort,
        ICandidateSelectionPolicy selectionPolicy,
        ICandidateFrontierView frontier,
        IEnumerable<PalId> palIds
    )
    {
        this.target = target;
        this.maxEffort = maxEffort;
        this.selectionPolicy = selectionPolicy;
        this.frontier = frontier;
        earlyCandidatesByPalId = palIds.ToFrozenDictionary(
            id => id,
            _ => new ConcurrentDictionary<EffectivePropertiesKey, IPalReference>()
        );
    }

    public CandidatePreFilterResult TryAdd(IPalReference candidate)
    {
        if (candidate.BreedingEffort > maxEffort)
            return CandidatePreFilterResult.Rejected;

        var propertiesKey = selectionPolicy.KeyOf(candidate);
        var frontierAssessment = frontier.AssessCandidate(
            candidate,
            propertiesKey
        );
        if (
            frontierAssessment == FrontierCandidateAssessment.Inferior &&
            !target.IsSatisfiedBy(candidate)
        )
            return CandidatePreFilterResult.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var accepted = earlyCandidates.TryAdd(propertiesKey, candidate);
        while (!accepted)
        {
            var incumbent = earlyCandidates[propertiesKey];
            switch (selectionPolicy.SelectEarlyCandidate(candidate, incumbent))
            {
                case EarlyCandidateSelection.RejectCandidate:
                    break;

                case EarlyCandidateSelection.KeepBoth:
                    accepted = true;
                    break;

                case EarlyCandidateSelection.ReplaceIncumbent:
                    accepted = earlyCandidates.TryUpdate(propertiesKey, candidate, incumbent);
                    if (!accepted)
                        continue;
                    break;
            }

            break;
        }

        return accepted
            ? new(
                PropertiesKey: propertiesKey,
                Accepted: true,
                IsGuaranteedImprovement: frontierAssessment == FrontierCandidateAssessment.GuaranteedImprovement
            )
            : CandidatePreFilterResult.Rejected;
    }

    public void Propagate(CandidatePreFilterResult result)
    {
        if (result.Accepted && result.IsGuaranteedImprovement)
        {
            frontier.MarkCandidatesOutdated(result.PropertiesKey);
        }
    }
}

/// <summary>
/// Immutable shared inputs for candidate expansion during one solver step.
/// </summary>
internal sealed record CandidateExpansionContext(
    int StepIndex,
    PalSpecifier Target,
    CandidatePreFilter PreFilter
);
