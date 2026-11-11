using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Retains every terminal candidate observed during a run, independently of
/// whether that candidate remains useful for further expansion.
/// </summary>
internal sealed class ResultAccumulator(PalSpecifier target)
{
    private readonly List<IPalReference> discovered = [];

    public IEnumerable<IPalReference> Results => discovered.Distinct();

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(target.IsSatisfiedBy));
    }
}
