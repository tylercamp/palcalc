using PalCalc.Model;
using PalCalc.Solver.PalReference;
using Serilog;

namespace PalCalc.Solver;

/// <summary>
/// Owns the retained breeding states, pending parent-pair work, and terminal
/// results for one solver run.
/// </summary>
internal sealed class SearchFrontier
{
    private static readonly ILogger logger = Log.ForContext<SearchFrontier>();

    private readonly SolverStateController controller;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly FrontierIndex index;
    private readonly ResultAccumulator resultAccumulator;
    private ParentPairSchedule pairSchedule;

    private readonly int maxThreads;
    private readonly PalSpecifier target;

    public IEnumerable<IPalReference> Results => resultAccumulator.Results;

    public IEnumerable<IPalReference> CurrentContent => index.All;

    public SearchFrontier(
        PalSpecifier target,
        IEnumerable<IPalReference> initialContent,
        int maxThreads,
        SolverStateController controller,
        ICandidateSelectionPolicy selectionPolicy
    )
    {
        ArgumentNullException.ThrowIfNull(selectionPolicy);

        this.target = target;
        this.controller = controller;
        this.selectionPolicy = selectionPolicy;

        var initialList = initialContent.ToList();
        index = new FrontierIndex(selectionPolicy);
        index.AddRange(initialList);

        resultAccumulator = new ResultAccumulator(target, selectionPolicy);
        resultAccumulator.Observe(index.All);
        pairSchedule = ParentPairSchedule.Initial(initialList);

        this.maxThreads = maxThreads <= 0
            ? Environment.ProcessorCount
            : maxThreads;
    }

    public bool IsImprovement(
        IPalReference reference,
        BreedingStateKey stateKey
    )
    {
        var incumbent = index[stateKey]?.FirstOrDefault();
        return incumbent == null ||
            selectionPolicy.IsFrontierImprovement(reference, incumbent);
    }

    /// <summary>
    /// Marks the currently retained alternatives for a state as ineligible for
    /// further expansion. The authoritative merge still decides whether the
    /// candidate which prompted this transition is retained.
    /// </summary>
    public void MarkStateObsolete(BreedingStateKey stateKey)
    {
        var alternatives = index[stateKey];
        if (alternatives == null) return;

        foreach (var alternative in alternatives)
            alternative.IsOutdated = true;
    }

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
        foreach (
            var addition in additions.TakeUntilCancelled(
                controller.CancellationToken
            )
        )
        {
            if (controller.IsPaused) controller.PauseIfRequested();
            index.Add(addition);
        }
    }

    // Gives a new, reduced collection which only includes the alternatives
    // retained by the authoritative policy for each breeding state.
    private IEnumerable<IPalReference> SelectCandidates(
        IEnumerable<IPalReference> candidates
    ) =>
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
        if (controller.CancellationToken.IsCancellationRequested)
            return FrontierDelta.None;

        // Authoritative selection is relatively heavy. Reduce independent
        // batches in parallel before selecting from their combined output.
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

        // Efficient additions are expanded first so they can invalidate less
        // efficient parents while the following batch is still running.
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
        if (!target.IsSatisfiedBy(reference))
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
