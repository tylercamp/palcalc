using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Retains every terminal candidate observed during a run, independently of
/// whether that candidate remains useful for further expansion.
/// </summary>
internal sealed class ResultAccumulator(
    PalSpecifier target,
    ICandidateSelectionPolicy selectionPolicy,
    AttackTargetContext attackTargets
)
{
    private readonly List<IPalReference> discovered = [];

    public IEnumerable<IPalReference> Results => discovered.Distinct();

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates)
    {
        var distinct = candidates.Distinct().ToList();
        if (attackTargets?.IsActive != true)
            return distinct
                .GroupBy(selectionPolicy.BreedingEffortGroupOf)
                .SelectMany(selectionPolicy.SelectRetainedAlternatives);

        var comparison = new FinalizationComparison();
        return distinct
            .GroupBy(reference => (
                Outcome: comparison.KeyOf(reference),
                Effort: reference.BreedingEffort,
                Cakes: PalResultProperty.SpecialCakes(reference)
            ))
            .SelectMany(selectionPolicy.SelectRetainedAlternatives);
    }

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(candidate =>
            attackTargets?.Satisfies(candidate) ?? target.IsSatisfiedBy(candidate)
        ));
    }
}
