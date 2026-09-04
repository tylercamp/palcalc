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
    AttackTargetContext attackTargets
)
{
    private readonly List<IPalReference> discovered = [];

    public IEnumerable<IPalReference> Results => discovered.Distinct();

    /// <summary>
    /// Applies the existing candidate-selection policy to coarse attack
    /// finalists before any inheritance tree is materialized.
    /// </summary>
    public IEnumerable<IPalReference> SelectSearchFinalists(
        IEnumerable<IPalReference> candidates
    ) => selectionPolicy.SelectRetainedAlternatives(candidates.Distinct());

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates)
    {
        var distinct = candidates.Distinct().ToList();
        if (attackTargets?.IsActive != true)
        {
            return distinct
                .GroupBy(selectionPolicy.BreedingEffortGroupOf)
                .SelectMany(selectionPolicy.SelectRetainedAlternatives);
        }

        // Search applied only the estimated cake tier. Gender adjustment and
        // recursively selected witnesses can change the concrete total, so
        // restore the cake-first objective using the materialized values.
        var minimumCakes = distinct.Count == 0
            ? 0
            : distinct.Min(reference =>
                reference.AttackProfile.EntriesSpan[0].TotalSpecialCakes
            );
        return selectionPolicy.SelectRetainedAlternatives(
            distinct.Where(reference =>
                reference.AttackProfile.EntriesSpan[0].TotalSpecialCakes == minimumCakes
            )
        );
    }

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(candidate =>
            attackTargets?.Satisfies(candidate) ?? target.IsSatisfiedBy(candidate)
        ));
    }
}
