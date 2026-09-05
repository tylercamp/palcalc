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

    public IEnumerable<IPalReference> SelectFinalResults(IEnumerable<IPalReference> candidates)
    {
        var distinct = candidates.Distinct().ToList();
        IEnumerable<IPalReference> preferredCakeTier = distinct;
        if (attackTargets?.IsActive == true && distinct.Count != 0)
        {
            var minimumCakes = distinct.Min(reference =>
                reference.AttackProfile.EntriesSpan[0].TotalSpecialCakes
            );
            preferredCakeTier = distinct.Where(reference =>
                reference.AttackProfile.EntriesSpan[0].TotalSpecialCakes == minimumCakes
            );
        }

        return preferredCakeTier
            .GroupBy(selectionPolicy.BreedingEffortGroupOf)
            .SelectMany(selectionPolicy.SelectRetainedAlternatives);
    }

    public void Observe(IEnumerable<IPalReference> candidates)
    {
        discovered.AddRange(candidates.Where(candidate =>
            attackTargets?.Satisfies(candidate) ?? target.IsSatisfiedBy(candidate)
        ));
    }
}
