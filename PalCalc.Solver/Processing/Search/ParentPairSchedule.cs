using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Tracks a set of remaining parent pairs to evaluate/expand.
/// 
/// Contains helpers to produce lists of work while avoiding redundant,
/// previously-computed work.
/// </summary>
internal sealed class ParentPairSchedule
{
    private readonly ILazyCartesianProduct<IPalReference> pending;

    private ParentPairSchedule(ILazyCartesianProduct<IPalReference> pending)
    {
        this.pending = pending;
    }

    public long Count => pending.Count;

    public ILazyCartesianProduct<IPalReference> Pending => pending;

    // (new content, all pairs must be evaluated)
    public static ParentPairSchedule Initial(List<IPalReference> initialContent) =>
        new(new LazyCartesianProduct<IPalReference>(initialContent, initialContent));

    public static ParentPairSchedule AfterPairMerge(
        List<IPalReference> orderedRetainedExisting,
        FrontierDelta delta
    )
    =>
        new(
            new ConcatenatedLazyCartesianProduct<IPalReference>([
                // pair the remaining frontier items with the newly-added items
                (orderedRetainedExisting, delta.AddedForScheduling),
                // pair the newly-added items with each other
                (delta.AddedForScheduling, delta.AddedForScheduling),
            ])
        );

    public ParentPairSchedule AfterSingleMerge(
        List<IPalReference> retainedExisting,
        FrontierDelta delta,
        CancellationToken cancellationToken
    ) =>
        new(
            new ConcatenatedLazyCartesianProduct<IPalReference>([
                // ignore pairs which are no longer present in the remaining frontier items
                pending.Where(
                    parent => !delta.Removed.Contains(parent),
                    cancellationToken
                ),
                // pair the newly-added items with what's left
                new AntiDiagonalLazyCartesianProduct<IPalReference>(
                    delta.AddedForScheduling,
                    retainedExisting
                ),
                // pair the newly-added items with each other
                new AntiDiagonalLazyCartesianProduct<IPalReference>(
                    delta.AddedForScheduling,
                    delta.AddedForScheduling
                ),
            ])
        );
}
