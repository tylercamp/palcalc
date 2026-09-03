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
    private const int AssessmentSampleMask = 0xff;

    [ThreadStatic]
    private static int assessmentSampleCounter;

    private readonly SolverStateController controller;
    private readonly ICandidateSelectionPolicy selectionPolicy;
    private readonly FrontierIndex index;
    private readonly ResultAccumulator resultAccumulator;
    private ParentPairSchedule pairSchedule;

    private readonly int maxThreads;
    private readonly PalSpecifier target;
    private readonly AttackTargetContext attackTargets;

    private long assessmentSamples;
    private long noIncumbentSamples;
    private long inferiorSamples;
    private long potentialSamples;
    private long guaranteedSamples;
    private long availableIncumbents;
    private long visitedIncumbents;
    private long attackEntryPairUpperBound;
    private long singleIncumbentSamples;
    private long twoToThreeIncumbentSamples;
    private long fourToSevenIncumbentSamples;
    private long eightToFifteenIncumbentSamples;
    private long sixteenPlusIncumbentSamples;
    private int maxAvailableIncumbents;
    private int maxVisitedIncumbents;
    private int maxCandidateProfileEntries;
    private int maxVisitedIncumbentProfileEntries;

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
        var sample = attackTargets?.IsActive == true &&
            ((++assessmentSampleCounter & AssessmentSampleMask) == 0);
        if (incumbents is null || incumbents.Count == 0)
        {
            if (sample)
                RecordAssessment(FrontierCandidateAssessment.PotentialImprovement, 0, 0, 0, 0, 0);
            return FrontierCandidateAssessment.PotentialImprovement;
        }

        var guaranteedImprovement = true;
        var visited = 0;
        var candidateProfileEntries = sample
            ? reference.AttackProfile.EntriesSpan.Length
            : 0;
        var visitedProfileEntries = 0;
        long entryPairs = 0;
        foreach (var incumbent in incumbents)
        {
            if (sample)
            {
                visited++;
                var incumbentProfileEntries = incumbent.AttackProfile.EntriesSpan.Length;
                visitedProfileEntries += incumbentProfileEntries;
                entryPairs += (long)candidateProfileEntries * incumbentProfileEntries;
            }

            var assessment = selectionPolicy.AssessAgainstFrontier(reference, incumbent);
            if (assessment == FrontierCandidateAssessment.Inferior)
            {
                if (sample)
                    RecordAssessment(
                        assessment,
                        incumbents.Count,
                        visited,
                        candidateProfileEntries,
                        visitedProfileEntries,
                        entryPairs
                    );
                return FrontierCandidateAssessment.Inferior;
            }
            if (assessment != FrontierCandidateAssessment.GuaranteedImprovement)
                guaranteedImprovement = false;
        }

        var result = guaranteedImprovement
            ? FrontierCandidateAssessment.GuaranteedImprovement
            : FrontierCandidateAssessment.PotentialImprovement;
        if (sample)
            RecordAssessment(
                result,
                incumbents.Count,
                visited,
                candidateProfileEntries,
                visitedProfileEntries,
                entryPairs
            );
        return result;
    }

    /// <summary>
    /// Logs a small sample of frontier scans. Sampling keeps diagnostics out of
    /// the comparison hot path while still exposing growth between iterations.
    /// </summary>
    public void LogAssessmentDiagnostics(int step)
    {
        if (attackTargets?.IsActive != true)
            return;

        var samples = Interlocked.Exchange(ref assessmentSamples, 0);
        if (samples == 0)
            return;

        logger.Debug(
            "Attack frontier profile: step={Step}, sampleRate=1/{SampleRate}, samples={Samples}, outcomes={NoIncumbent}+{Inferior}+{Potential}+{Guaranteed}, incumbents={AvailableIncumbents}->{VisitedIncumbents}, attackEntryPairUpperBound={AttackEntryPairUpperBound}, incumbentBuckets={One}+{TwoToThree}+{FourToSeven}+{EightToFifteen}+{SixteenPlus}, maxIncumbents={MaxAvailable}->{MaxVisited}, maxProfileEntries={MaxCandidate}+{MaxVisitedIncumbents}",
            step,
            AssessmentSampleMask + 1,
            samples,
            Interlocked.Exchange(ref noIncumbentSamples, 0),
            Interlocked.Exchange(ref inferiorSamples, 0),
            Interlocked.Exchange(ref potentialSamples, 0),
            Interlocked.Exchange(ref guaranteedSamples, 0),
            Interlocked.Exchange(ref availableIncumbents, 0),
            Interlocked.Exchange(ref visitedIncumbents, 0),
            Interlocked.Exchange(ref attackEntryPairUpperBound, 0),
            Interlocked.Exchange(ref singleIncumbentSamples, 0),
            Interlocked.Exchange(ref twoToThreeIncumbentSamples, 0),
            Interlocked.Exchange(ref fourToSevenIncumbentSamples, 0),
            Interlocked.Exchange(ref eightToFifteenIncumbentSamples, 0),
            Interlocked.Exchange(ref sixteenPlusIncumbentSamples, 0),
            Interlocked.Exchange(ref maxAvailableIncumbents, 0),
            Interlocked.Exchange(ref maxVisitedIncumbents, 0),
            Interlocked.Exchange(ref maxCandidateProfileEntries, 0),
            Interlocked.Exchange(ref maxVisitedIncumbentProfileEntries, 0)
        );
    }

    private void RecordAssessment(
        FrontierCandidateAssessment assessment,
        int available,
        int visited,
        int candidateProfileEntries,
        int visitedProfileEntries,
        long entryPairs
    )
    {
        Interlocked.Increment(ref assessmentSamples);
        Interlocked.Add(ref availableIncumbents, available);
        Interlocked.Add(ref visitedIncumbents, visited);
        Interlocked.Add(ref attackEntryPairUpperBound, entryPairs);

        if (available == 0)
            Interlocked.Increment(ref noIncumbentSamples);
        else
        {
            switch (assessment)
            {
                case FrontierCandidateAssessment.Inferior:
                    Interlocked.Increment(ref inferiorSamples);
                    break;
                case FrontierCandidateAssessment.GuaranteedImprovement:
                    Interlocked.Increment(ref guaranteedSamples);
                    break;
                default:
                    Interlocked.Increment(ref potentialSamples);
                    break;
            }

            if (available == 1)
                Interlocked.Increment(ref singleIncumbentSamples);
            else if (available <= 3)
                Interlocked.Increment(ref twoToThreeIncumbentSamples);
            else if (available <= 7)
                Interlocked.Increment(ref fourToSevenIncumbentSamples);
            else if (available <= 15)
                Interlocked.Increment(ref eightToFifteenIncumbentSamples);
            else
                Interlocked.Increment(ref sixteenPlusIncumbentSamples);
        }

        UpdateMax(ref maxAvailableIncumbents, available);
        UpdateMax(ref maxVisitedIncumbents, visited);
        UpdateMax(ref maxCandidateProfileEntries, candidateProfileEntries);
        UpdateMax(ref maxVisitedIncumbentProfileEntries, visitedProfileEntries);
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

    public void ObserveTerminal(IEnumerable<IPalReference> candidates) =>
        resultAccumulator.Observe(candidates);

    /// <summary>
    /// Marks candidates with the same effective properties as ineligible for
    /// further breeding. This allows us to skip older, less efficient candidates
    /// when a better one is found in the middle of a search.
    /// 
    /// The full simplification pass still decides whether the candidate that
    /// prompted this change is retained.
    /// </summary>
    public void MarkCandidatesOutdated(EffectivePropertiesKey propertiesKey)
    {
        var alternatives = index[propertiesKey];
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
        if (attackTargets?.IsActive != true)
        {
            resultAccumulator.Observe(
                newCandidates.TakeWhile(_ =>
                {
                    if (controller.IsPaused) controller.PauseIfRequested();
                    return !controller.CancellationToken.IsCancellationRequested;
                })
            );
        }
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
