using PalCalc.Solver.PalReference;

namespace PalCalc.Solver;

/// <summary>
/// Owns the parent pairs which have become relevant but have not yet been
/// expanded.
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

    public static ParentPairSchedule Initial(List<IPalReference> initialContent) =>
        new(new LazyCartesianProduct<IPalReference>(initialContent, initialContent));

    public static ParentPairSchedule AfterPairMerge(
        List<IPalReference> orderedRetainedExisting,
        FrontierDelta delta
    )
    =>
        new(
            new ConcatenatedLazyCartesianProduct<IPalReference>([
                (orderedRetainedExisting, delta.AddedForScheduling),
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
                pending.Where(
                    parent => !delta.Removed.Contains(parent),
                    cancellationToken
                ),
                new AntiDiagonalLazyCartesianProduct<IPalReference>(
                    delta.AddedForScheduling,
                    retainedExisting
                ),
                new AntiDiagonalLazyCartesianProduct<IPalReference>(
                    delta.AddedForScheduling,
                    delta.AddedForScheduling
                ),
            ])
        );
}
