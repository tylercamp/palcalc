using PalCalc.Model;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Numerics;

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
    private readonly AttackTargetContext attackTargets;
    private readonly TimeSpan maxEffort;
    private readonly BreedingSolverSettings settings;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly ICandidateFrontierView frontier;
    private readonly FrozenDictionary<
        PalId,
        ConcurrentDictionary<EffectivePropertiesKey, EarlyCandidateGroup>
    > earlyCandidatesByPalId;
    private readonly ConcurrentDictionary<EffectivePropertiesKey, IPalReference> terminalCandidates = new();

    public CandidatePreFilter(
        PalSpecifier target,
        TimeSpan maxEffort,
        ICandidateSelectionPolicy selectionPolicy,
        ICandidateFrontierView frontier,
        IEnumerable<PalId> palIds,
        AttackTargetContext attackTargets,
        BreedingSolverSettings settings
    )
    {
        this.target = target;
        this.attackTargets = attackTargets;
        this.maxEffort = maxEffort;
        this.settings = settings;
        this.selectionPolicy = selectionPolicy;
        this.frontier = frontier;
        earlyCandidatesByPalId = palIds.ToFrozenDictionary(
            id => id,
            _ => new ConcurrentDictionary<EffectivePropertiesKey, EarlyCandidateGroup>()
        );
    }

    public CandidatePreFilterResult TryAdd(IPalReference candidate)
    {
        if (candidate.BreedingEffort > maxEffort)
            return CandidatePreFilterResult.Rejected;

        var isTerminal = attackTargets?.Satisfies(candidate) ?? target.IsSatisfiedBy(candidate);
        // A completed result may be a poor future parent and be rejected below.
        // Preserve its best final path before applying frontier-oriented filters.
        if (isTerminal && attackTargets?.IsActive == true)
            RetainTerminal(candidate);

        var propertiesKey = selectionPolicy.KeyOf(candidate);
        var frontierAssessment = frontier.AssessCandidate(
            candidate,
            propertiesKey
        );
        if (
            frontierAssessment == FrontierCandidateAssessment.Inferior &&
            !isTerminal
        )
            return CandidatePreFilterResult.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var group = earlyCandidates.GetOrAdd(propertiesKey, _ => new());
        var accepted = group.TryAdd(candidate, selectionPolicy, attackTargets?.IsActive == true);

        return accepted
            ? new(
                PropertiesKey: propertiesKey,
                Accepted: true,
                IsGuaranteedImprovement: frontierAssessment == FrontierCandidateAssessment.GuaranteedImprovement
            )
            : CandidatePreFilterResult.Rejected;
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
            candidate.Pal,
            candidate.Gender,
            candidate.IVs,
            candidate.EffectivePassives
        ) && candidate.AttackProfile.Contains(attackTargets.FullTargetMask);
        if (isTerminal)
            RetainTerminal(candidate.Materialize());

        var propertiesKey = defaultPolicy.KeyOf(candidate);
        var frontierAssessment = searchFrontier.AssessCandidate(
            candidate,
            propertiesKey,
            defaultPolicy
        );
        if (
            frontierAssessment == FrontierCandidateAssessment.Inferior &&
            !isTerminal
        )
            return CandidatePreFilterResult.Rejected;

        var earlyCandidates = earlyCandidatesByPalId[candidate.Pal.Id];
        var group = earlyCandidates.GetOrAdd(propertiesKey, _ => new());
        if (!group.TryAdd(ref candidate))
            return CandidatePreFilterResult.Rejected;

        return new(
            PropertiesKey: propertiesKey,
            Accepted: true,
            IsGuaranteedImprovement:
                frontierAssessment == FrontierCandidateAssessment.GuaranteedImprovement
        );
    }

    public IReadOnlyList<IPalReference> TerminalCandidates => terminalCandidates.Values.ToArray();

    public List<IPalReference> RetainedAttackCandidates()
    {
        var retained = new List<IPalReference>();
        foreach (var candidatesByKey in earlyCandidatesByPalId.Values)
            foreach (var group in candidatesByKey.Values)
                group.AddRetainedAttackCandidatesTo(retained);
        return retained;
    }

    private void RetainTerminal(IPalReference candidate)
    {
        if (target.RequiredGender != PalGender.WILDCARD && candidate.Gender != target.RequiredGender)
        {
            if (candidate.Gender != PalGender.WILDCARD && !settings.UseGenderReversers)
                return;
            candidate = candidate.WithGuaranteedGender(
                settings.DB,
                target.RequiredGender,
                settings.UseGenderReversers
            );
        }

        var key = selectionPolicy.KeyOf(candidate);
        terminalCandidates.AddOrUpdate(
            key,
            candidate,
            (_, incumbent) => CompareTerminal(candidate, incumbent) < 0
                ? candidate
                : incumbent
        );
    }

    private int CompareTerminal(IPalReference left, IPalReference right)
    {
        var leftEntry = BestTerminalEntry(left);
        var rightEntry = BestTerminalEntry(right);
        var comparison = CompareAttackEntries(leftEntry, rightEntry);
        if (comparison != 0) return comparison;
        comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
        if (comparison != 0) return comparison;
        comparison = left.TotalCost.CompareTo(right.TotalCost);
        if (comparison != 0) return comparison;
        return left.GetHashCode().CompareTo(right.GetHashCode());
    }

    private AttackProfileEntry BestTerminalEntry(IPalReference candidate)
    {
        AttackProfileEntry best = default;
        var found = false;
        foreach (ref readonly var entry in candidate.AttackProfile.EntriesSpan)
        {
            if ((entry.LearnedTargetMask & attackTargets.FullTargetMask) != attackTargets.FullTargetMask)
                continue;
            if (!found || CompareAttackEntries(entry, best) < 0)
            {
                best = entry;
                found = true;
            }
        }
        return best;
    }

    public void Propagate(CandidatePreFilterResult result)
    {
        if (result.Accepted && result.IsGuaranteedImprovement)
        {
            frontier.MarkCandidatesOutdated(result.PropertiesKey);
        }
    }

    private sealed class EarlyCandidateGroup
    {
        private const int AttackSlotCount = AttackProfile.TargetMaskCount;

        private readonly IPalReference[] attackChampions = new IPalReference[AttackSlotCount];
        private readonly AttackProfileEntry[] attackEntries = new AttackProfileEntry[AttackSlotCount];
        private readonly Dictionary<IPalReference, int> championCounts =
            new(ReferenceEqualityComparer.Instance);
        private IPalReference ordinaryIncumbent;

        public void AddRetainedAttackCandidatesTo(List<IPalReference> destination)
        {
            lock (this)
                destination.AddRange(championCounts.Keys);
        }

        public bool TryAdd(
            IPalReference candidate,
            ICandidateSelectionPolicy selectionPolicy,
            bool attacksActive
        )
        {
            lock (this)
            {
                if (!attacksActive)
                    return TryAddOrdinary(candidate, selectionPolicy);

                var attackProfile = candidate.AttackProfile;

                Span<bool> improved = stackalloc bool[AttackSlotCount];
                var improvesAny = false;
                foreach (ref readonly var entry in attackProfile.EntriesSpan)
                {
                    var slot = entry.LearnedTargetMask;
                    if (attackChampions[slot] is not null &&
                        Compare(candidate, entry, attackChampions[slot], attackEntries[slot]) >= 0)
                        continue;

                    improved[slot] = true;
                    improvesAny = true;
                }

                if (!improvesAny)
                    return false;

                for (var slot = 0; slot < improved.Length; slot++)
                {
                    if (!improved[slot])
                        continue;

                    var previous = attackChampions[slot];
                    if (previous is not null && --championCounts[previous] == 0)
                    {
                        championCounts.Remove(previous);
                        previous.IsOutdated = true;
                    }

                    attackChampions[slot] = candidate;
                    attackEntries[slot] = BestEntryForSlot(candidate, slot);
                    championCounts.TryGetValue(candidate, out var count);
                    championCounts[candidate] = count + 1;
                }

                return true;
            }
        }

        public bool TryAdd(ref CandidateDraft candidate)
        {
            lock (this)
            {
                Span<bool> improved = stackalloc bool[AttackSlotCount];
                var improvesAny = false;
                var masks = candidate.AttackProfile.EntryTargetMasks;
                while (masks != 0)
                {
                    var slot = BitOperations.TrailingZeroCount(masks);
                    masks &= masks - 1;
                    var entry = candidate.AttackProfile.EntryForMask(slot);
                    if (attackChampions[slot] is not null &&
                        Compare(candidate, entry, attackChampions[slot], attackEntries[slot]) >= 0)
                        continue;

                    improved[slot] = true;
                    improvesAny = true;
                }

                if (!improvesAny)
                    return false;

                var materialized = candidate.Materialize();
                for (var slot = 0; slot < improved.Length; slot++)
                {
                    if (!improved[slot])
                        continue;

                    var previous = attackChampions[slot];
                    if (previous is not null && --championCounts[previous] == 0)
                    {
                        championCounts.Remove(previous);
                        previous.IsOutdated = true;
                    }

                    attackChampions[slot] = materialized;
                    attackEntries[slot] = candidate.AttackProfile.EntryForMask(slot);
                    championCounts.TryGetValue(materialized, out var count);
                    championCounts[materialized] = count + 1;
                }

                return true;
            }
        }

        private bool TryAddOrdinary(
            IPalReference candidate,
            ICandidateSelectionPolicy selectionPolicy
        )
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

        private static AttackProfileEntry BestEntryForSlot(IPalReference candidate, int slot)
        {
            var mask = (byte)slot;
            AttackProfileEntry best = default;
            var found = false;
            var attackProfile = candidate.AttackProfile;
            foreach (ref readonly var entry in attackProfile.EntriesSpan)
            {
                if (entry.LearnedTargetMask != mask)
                    continue;
                if (!found || CompareAttackEntries(entry, best) < 0)
                {
                    best = entry;
                    found = true;
                }
            }
            return best;
        }

        private static int Compare(
            IPalReference left,
            in AttackProfileEntry leftEntry,
            IPalReference right,
            in AttackProfileEntry rightEntry
        )
        {
            var comparison = CompareAttackEntries(leftEntry, rightEntry);
            if (comparison != 0) return comparison;
            comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
            if (comparison != 0) return comparison;
            comparison = left.TotalCost.CompareTo(right.TotalCost);
            if (comparison != 0) return comparison;
            return left.GetHashCode().CompareTo(right.GetHashCode());
        }

        private static int Compare(
            in CandidateDraft left,
            in AttackProfileEntry leftEntry,
            IPalReference right,
            in AttackProfileEntry rightEntry
        )
        {
            var comparison = CompareAttackEntries(leftEntry, rightEntry);
            if (comparison != 0) return comparison;
            comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
            if (comparison != 0) return comparison;
            comparison = left.TotalCost.CompareTo(right.TotalCost);
            if (comparison != 0) return comparison;
            return left.GetHashCode().CompareTo(right.GetHashCode());
        }
    }

    private static int CompareAttackEntries(
        in AttackProfileEntry left,
        in AttackProfileEntry right
    )
    {
        var comparison = left.TotalSpecialCakes.CompareTo(right.TotalSpecialCakes);
        return comparison != 0
            ? comparison
            : left.LearnedTargetMask.CompareTo(right.LearnedTargetMask);
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
