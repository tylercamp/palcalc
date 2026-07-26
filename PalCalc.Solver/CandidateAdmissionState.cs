using PalCalc.Model;
using PalCalc.Solver.PalReference;
using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace PalCalc.Solver;

/// <summary>
/// The narrow frontier operations required during candidate expansion.
/// </summary>
internal interface ICandidateFrontierView
{
    FrontierCandidateAssessment AssessCandidate(
        IPalReference candidate,
        BreedingStateKey stateKey
    );

    void MarkStateObsolete(BreedingStateKey stateKey);
}

internal readonly record struct CandidateAdmission(
    BreedingStateKey StateKey,
    bool Accepted,
    bool CanImmediatelyObsolete
)
{
    public static CandidateAdmission Rejected => default;
}

/// <summary>
/// Owns cheap, concurrent candidate admission for one solver iteration.
/// Authoritative selection remains the responsibility of the frontier.
/// </summary>
internal sealed class CandidateAdmissionState
{
    private readonly PalSpecifier target;
    private readonly TimeSpan maxEffort;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly ICandidateFrontierView frontier;
    private readonly FrozenDictionary<
        PalId,
        ConcurrentDictionary<BreedingStateKey, IPalReference>
    > earlyCandidatesByPalId;

    public CandidateAdmissionState(
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
            _ => new ConcurrentDictionary<
                BreedingStateKey,
                IPalReference
            >()
        );
    }

    public CandidateAdmission TryAdmit(IPalReference candidate)
    {
        if (candidate.BreedingEffort > maxEffort)
            return CandidateAdmission.Rejected;

        var stateKey = selectionPolicy.KeyOf(candidate);
        var frontierAssessment = frontier.AssessCandidate(
            candidate,
            stateKey
        );
        if (
            !frontierAssessment.IsImprovement &&
            !target.IsSatisfiedBy(candidate)
        )
            return CandidateAdmission.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var accepted = earlyCandidates.TryAdd(stateKey, candidate);
        while (!accepted)
        {
            var incumbent = earlyCandidates[stateKey];
            switch (
                selectionPolicy.SelectEarlyCandidate(
                    candidate,
                    incumbent
                )
            )
            {
                case EarlyCandidateSelection.RejectCandidate:
                    break;

                case EarlyCandidateSelection.KeepBoth:
                    accepted = true;
                    break;

                case EarlyCandidateSelection.ReplaceIncumbent:
                    accepted = earlyCandidates.TryUpdate(
                        stateKey,
                        candidate,
                        incumbent
                    );
                    if (!accepted)
                        continue;
                    break;
            }

            break;
        }

        return accepted
            ? new(
                StateKey: stateKey,
                Accepted: true,
                CanImmediatelyObsolete:
                    frontierAssessment.CanImmediatelyObsolete
            )
            : CandidateAdmission.Rejected;
    }

    public void Complete(CandidateAdmission admission)
    {
        if (
            admission.Accepted &&
            admission.CanImmediatelyObsolete
        )
        {
            frontier.MarkStateObsolete(admission.StateKey);
        }
    }
}

/// <summary>
/// Immutable shared inputs for candidate expansion during one solver step.
/// </summary>
internal sealed record CandidateExpansionContext(
    int StepIndex,
    PalSpecifier Target,
    CandidateAdmissionState Admissions
);
