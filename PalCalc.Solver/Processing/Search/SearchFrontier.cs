using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Utils;
using Serilog;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Keeps the best candidates found so far, provides methods for processing those pairs
/// of candidates, and collects completed results for a full solver run.
/// </summary>
internal sealed class SearchFrontier : ICandidateFrontierView
{
    private static readonly ILogger logger = Log.ForContext<SearchFrontier>();

    private readonly SolverStateController controller;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly FrontierIndex index;
    private readonly ResultAccumulator resultAccumulator;
    private ParentPairSchedule pairSchedule;

    private readonly int maxThreads;
    private readonly PalSpecifier target;
    private readonly AttackTargetContext attackTargets;

    public ResultAccumulator TerminalResults => resultAccumulator;

    public IEnumerable<IPalReference> CurrentContent => index.All;

    public SearchFrontier(
        PalSpecifier target,
        IEnumerable<IPalReference> initialContent,
        int maxThreads,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy,
        AttackTargetContext attackTargets
    )
    {
        ArgumentNullException.ThrowIfNull(selectionPolicy);

        this.target = target;
        this.attackTargets = attackTargets;
        this.controller = controller;
        this.selectionPolicy = selectionPolicy;

        var initialList = initialContent.ToList();
        index = new FrontierIndex(selectionPolicy);
        index.AddRange(initialList);

        resultAccumulator = new ResultAccumulator(target, selectionPolicy, attackTargets);
        resultAccumulator.Observe(index.All);
        pairSchedule = ParentPairSchedule.Initial(initialList);

        this.maxThreads = maxThreads <= 0
            ? Environment.ProcessorCount
            : maxThreads;
    }

    public FrontierCandidateAssessment AssessCandidate(IPalReference reference, EffectivePropertiesKey propertiesKey)
    {
        var incumbents = index[propertiesKey];
        if (incumbents is null || incumbents.Count == 0)
            return FrontierCandidateAssessment.PotentialImprovement;

        var guaranteedImprovement = true;
        for (int i = 0; i < incumbents.Count; i++)
        {
            // Direct `for` loop since `List+Enumerator` was still being allocated for some reason
            var incumbent = incumbents[i];
            var assessment = selectionPolicy.AssessAgainstFrontier(reference, incumbent);
            if (assessment == FrontierCandidateAssessment.Inferior)
                return FrontierCandidateAssessment.Inferior;
            if (assessment != FrontierCandidateAssessment.GuaranteedImprovement)
                guaranteedImprovement = false;
        }

        return guaranteedImprovement
            ? FrontierCandidateAssessment.GuaranteedImprovement
            : FrontierCandidateAssessment.PotentialImprovement;
    }

    internal FrontierCandidateAssessment AssessCandidate(
        in CandidateDraft candidate,
        EffectivePropertiesKey propertiesKey,
        DefaultCandidateSelectionPolicy policy
    )
    {
        var incumbents = index[propertiesKey];
        if (incumbents is null || incumbents.Count == 0)
            return FrontierCandidateAssessment.PotentialImprovement;

        var guaranteedImprovement = true;
        for (int i = 0; i < incumbents.Count; i++)
        {
            var assessment = policy.AssessAgainstFrontier(candidate, incumbents[i]);
            if (assessment == FrontierCandidateAssessment.Inferior)
                return FrontierCandidateAssessment.Inferior;
            if (assessment != FrontierCandidateAssessment.GuaranteedImprovement)
                guaranteedImprovement = false;
        }

        return guaranteedImprovement
            ? FrontierCandidateAssessment.GuaranteedImprovement
            : FrontierCandidateAssessment.PotentialImprovement;
    }

    public void ObserveTerminal(IEnumerable<IPalReference> candidates) =>
        resultAccumulator.Observe(candidates);

    /// <summary>
    /// Expands every pending parent pair and atomically advances the frontier
    /// and its pair schedule to the resulting delta.
    /// </summary>
    public FrontierDelta ExpandPairs(
        Func<
            ILazyCartesianProduct<IPalReference>,
            IEnumerable<IPalReference>
        > expand
    )
    {
        if (pairSchedule.Count == 0) return FrontierDelta.None;

        logger.Debug("beginning pairs processing");

        var newCandidates = expand(pairSchedule.Pending).ToList();
        var retainedExisting = index.All.OrderBy(p => p.Pal.Id).ToList();
        var delta = MergeCandidates(newCandidates);
        retainedExisting.RemoveAll(delta.Removed.Contains);

        pairSchedule = ParentPairSchedule.AfterPairMerge(
            selectionPolicy.OrderForExpansion(retainedExisting),
            delta
        );
        AddToIndex(delta.Added);

        return delta;
    }

    /// <summary>
    /// Applies a whole-frontier transformation, then advances the frontier and
    /// pair schedule to account for the resulting delta.
    /// </summary>
    public FrontierDelta ExpandSingles(
        Func<
            IEnumerable<IPalReference>,
            IEnumerable<IPalReference>
        > expand
    )
    {
        logger.Debug("beginning single-item processing");

        var newCandidates = expand(index.All).ToList();
        if (newCandidates.Count == 0) return FrontierDelta.None;

        var retainedExisting = selectionPolicy.OrderForExpansion(index.All);
        var delta = MergeCandidates(newCandidates);
        retainedExisting.RemoveAll(delta.Removed.Contains);

        pairSchedule = pairSchedule.AfterSingleMerge(
            retainedExisting,
            delta,
            controller.CancellationToken
        );
        AddToIndex(delta.Added);

        return delta;
    }

    private void AddToIndex(IEnumerable<IPalReference> additions)
    {
        foreach (var addition in additions.TakeUntilCancelled(controller.CancellationToken))
        {
            if (controller.IsPaused) controller.PauseIfRequested();
            index.Add(addition);
        }
    }

    // Keeps the breeding paths selected by the full simplification policy for
    // each group of candidates with the same effective properties.
    private IEnumerable<IPalReference> SelectCandidates(IEnumerable<IPalReference> candidates) =>
        candidates
            .TakeWhile(_ =>
            {
                if (controller.IsPaused) controller.PauseIfRequested();
                return !controller.CancellationToken.IsCancellationRequested;
            })
            .GroupBy(selectionPolicy.KeyOf)
            .SelectMany(selectionPolicy.SelectRetainedAlternatives);

    private FrontierDelta MergeCandidates(List<IPalReference> newCandidates)
    {
        var changed = false;
        var allAdded = new List<IPalReference>();
        var allRemoved = new List<IPalReference>();

        // Terminal results are accumulated before frontier selection because a
        // completed result need not remain useful as a future parent.
        resultAccumulator.Observe(
            newCandidates.TakeWhile(_ =>
            {
                if (controller.IsPaused) controller.PauseIfRequested();
                return !controller.CancellationToken.IsCancellationRequested;
            })
        );
        newCandidates.RemoveAll(candidate => candidate.IsOutdated);
        if (controller.CancellationToken.IsCancellationRequested)
            return FrontierDelta.None;

        // The full simplification pass is relatively expensive. Simplify
        // independent batches in parallel before combining their output.
        logger.Debug(
            "performing pre-prune on {count} items",
            newCandidates.Count
        );
        var selected = SelectCandidates(
            newCandidates
                .BatchedAsParallel()
                .WithCancellation(controller.CancellationToken)
                .WithDegreeOfParallelism(maxThreads)
                .SelectMany(batch => SelectCandidates(batch).ToList())
                .ToList()
        );
        if (controller.CancellationToken.IsCancellationRequested)
            return FrontierDelta.None;

        logger.Debug("merging");

        foreach (
            var newGroup in selected
                .GroupBy(selectionPolicy.KeyOf)
                .ToList()
        )
        {
            if (controller.CancellationToken.IsCancellationRequested)
                return new FrontierDelta(changed, [], []);
            if (controller.IsPaused) controller.PauseIfRequested();

            var newAlternatives = newGroup.ToList();
            var representative = newAlternatives.First();

            if (IsCompletedTerminal(representative))
                continue;

            var existingAlternatives = index[newGroup.Key];
            if (existingAlternatives?.FirstOrDefault() != null)
            {
                var selection = selectionPolicy.SelectRetainedAlternatives(
                    existingAlternatives.Concat(
                        newAlternatives.Except(existingAlternatives)
                    )
                );

                var added = newAlternatives
                    .Intersect(selection)
                    .Except(existingAlternatives);
                var removed = existingAlternatives.Except(selection);

                if (added.Any())
                {
                    allAdded.AddRange(added);
                    changed = true;
                }

                if (removed.Any())
                {
                    foreach (var reference in removed.ToList())
                    {
                        index.Remove(reference);
                        allRemoved.Add(reference);
                    }
                    changed = true;
                }
            }
            else
            {
                allAdded.AddRange(newAlternatives);
                changed = true;
            }
        }

        // Expand efficient additions first in the next scheduled pass.
        allAdded = selectionPolicy.OrderForExpansion(allAdded);

        return new FrontierDelta(
            changed,
            allAdded,
            new HashSet<IPalReference>(allRemoved)
        );
    }

    private bool IsCompletedTerminal(IPalReference reference)
    {
        if (
            reference is not BredPalReference &&
            reference is not SurgeryTablePalReference
        )
            return false;
        if (!(attackTargets?.Satisfies(reference) ?? target.IsSatisfiedBy(reference)))
            return false;

        return
            reference.EffectivePassives.Count(
                passive => passive is not RandomPassiveSkill
            ) == GameConstants.MaxTotalPassives ||
            !target.OptionalPassives
                .Except(reference.EffectivePassives)
                .Any();
    }
}
