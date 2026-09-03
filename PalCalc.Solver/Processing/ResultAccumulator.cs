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

    // Attack-profile effort is selected only after terminal gender adjustment,
    // so structural effort cannot group terminal candidates here.
    public IEnumerable<IPalReference> Results => discovered.Distinct();

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates)
    {
        var distinct = candidates.Distinct().ToList();
        if (attackTargets?.IsActive != true)
        {
            return distinct
                .GroupBy(selectionPolicy.BreedingEffortGroupOf)
                .SelectMany(selectionPolicy.SelectRetainedAlternatives);
        }

        // Materialized attack results have already had required gender applied.
        // Keep only the minimum-cake tier before ordinary result pruning; this is
        // the accepted trade-off that makes cake use the primary optimization goal.
        var minimumCakes = distinct.Count == 0
            ? 0
            : distinct.Min(reference => reference.AttackProfile.EntriesSpan[0].TotalSpecialCakes);
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
