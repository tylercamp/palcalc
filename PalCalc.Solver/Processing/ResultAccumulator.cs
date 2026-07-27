using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Retains every terminal candidate observed during a run, independently of
/// whether that candidate remains useful for further expansion.
/// </summary>
internal sealed class ResultAccumulator(
    PalSpecifier target,
    ICandidateSelectionPolicy selectionPolicy
)
{
    private readonly List<IPalReference> discovered = [];

    public IEnumerable<IPalReference> Results =>
        discovered
            .Distinct()
            .GroupBy(selectionPolicy.BreedingEffortGroupOf)
            .SelectMany(selectionPolicy.SelectRetainedAlternatives);

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(target.IsSatisfiedBy));
    }
}
