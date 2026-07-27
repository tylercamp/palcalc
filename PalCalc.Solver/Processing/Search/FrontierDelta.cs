using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Records which candidates one simplification pass added to and removed from
/// the frontier. The parent-pair schedule uses this change to find new work.
/// </summary>
internal sealed class FrontierDelta
{
    private readonly List<IPalReference> added;
    private readonly HashSet<IPalReference> removed;

    public FrontierDelta(
        bool changed,
        List<IPalReference> added,
        HashSet<IPalReference> removed
    )
    {
        Changed = changed;
        this.added = added;
        this.removed = removed;
    }

    public bool Changed { get; }

    public IReadOnlyList<IPalReference> Added => added;

    public IReadOnlySet<IPalReference> Removed => removed;

    internal List<IPalReference> AddedForScheduling => added;

    public static FrontierDelta None => new(false, [], []);
}
