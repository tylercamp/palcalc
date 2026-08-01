using PalCalc.Solver.PalReference;
using System.Collections.ObjectModel;

namespace PalCalc.Solver;

/// <summary>
/// The completed output of one solver invocation.
/// </summary>
public sealed class BreedingSolverResult
{
    internal BreedingSolverResult(
        IEnumerable<IPalReference> results,
        bool isCanceled
    )
    {
        Results = new ReadOnlyCollection<IPalReference>([.. results]);
        IsCanceled = isCanceled;
    }

    public IReadOnlyList<IPalReference> Results { get; }

    /// <summary>
    /// Indicates that cancellation was requested before the run completed.
    /// Partial results may be present but callers normally discard them.
    /// </summary>
    public bool IsCanceled { get; }
}
