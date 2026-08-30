using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Retains every terminal candidate observed during a run, independently of
/// whether that candidate remains useful for further expansion.
/// </summary>
internal sealed class ResultAccumulator(
    PalSpecifier target,
    ICandidateSelectionPolicy selectionPolicy,
    AttackTargetContext attackTargets = null
)
{
    private readonly List<IPalReference> discovered = [];

    // Attack-profile effort is selected only after terminal gender adjustment,
    // so structural effort cannot group terminal candidates here.
    public IEnumerable<IPalReference> Results => discovered.Distinct();

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates) =>
        candidates
            .Distinct()
            .GroupBy(selectionPolicy.BreedingEffortGroupOf)
            .SelectMany(selectionPolicy.SelectRetainedAlternatives);

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(candidate =>
            attackTargets?.Satisfies(candidate) ?? target.IsSatisfiedBy(candidate)
        ));
    }
}
