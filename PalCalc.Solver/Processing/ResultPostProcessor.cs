using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.Utils;
using System.Diagnostics;

namespace PalCalc.Solver.Processing;

/// <summary>
/// Applies the deliberately post-search transformations and final output
/// constraints for one solver run.
/// </summary>
internal sealed class ResultPostProcessor(
    PalSpecifier target,
    BreedingSolverSettings settings,
    SolverStateController controller
)
{
    public void ApplySurgery(SearchFrontier frontier)
    {
        var surgeryCompatiblePassives = target
            .DesiredPassives
            .Where(passive => passive.SupportsSurgery)
            .Where(settings.SurgeryPassives.Contains)
            .ToList();
        if (
            surgeryCompatiblePassives.Count == 0 ||
            controller.CancellationToken.IsCancellationRequested
        )
            return;

        // Surgery runs once after breeding. Applying it during every iteration
        // would model more combinations, but would materially expand the
        // frontier and increase search cost.
        frontier.ExpandSingles(palReferences =>
            palReferences
                .Where(reference => reference.Pal == target.Pal)
                .Where(reference =>
                    reference.EffectivePassives.Count < GameConstants.MaxTotalPassives ||
                    reference.EffectivePassives.Any(passive => passive is RandomPassiveSkill) ||
                    reference.EffectivePassives.Any(target.OptionalPassives.Contains)
                )
                .TakeUntilCancelled(controller.CancellationToken)
                .Select(reference =>
                    AddRequiredPassives(
                        reference,
                        surgeryCompatiblePassives
                    )
                )
                .SelectMany(reference =>
                    AddOptionalPassives(
                        reference,
                        surgeryCompatiblePassives
                    )
                )
        );
    }

    public List<IPalReference> Finalize(ResultAccumulator terminalResults) =>
        terminalResults
            .Results
            // Bred candidates are constrained in the expansion kernel. Apply
            // the same output constraint to owned candidates which already
            // satisfy the target.
            .Where(reference =>
                reference.ActualPassives
                    .Except(target.DesiredPassives)
                    .Count() <=
                settings.MaxBredIrrelevantPassives
            )
            .SelectMany(EnforceRequiredGender)
            .Distinct()
            .ToList();

    private IPalReference AddRequiredPassives(
        IPalReference reference,
        IReadOnlyList<PassiveSkill> surgeryCompatiblePassives
    )
    {
        var missingRequiredPassives = surgeryCompatiblePassives
            .Where(target.RequiredPassives.Contains)
            .Except(reference.EffectivePassives)
            .ToList();
        if (
            missingRequiredPassives.Count == 0 ||
            missingRequiredPassives.Sum(passive => passive.SurgeryCost) + reference.TotalCost > settings.MaxSurgeryCost
        )
            return reference;

        var removablePassives = new Queue<PassiveSkill>(reference.EffectivePassives.OfType<RandomPassiveSkill>());
        foreach (
            var optional in reference.EffectivePassives.Where(
                target.OptionalPassives.Contains
            )
        )
        {
            removablePassives.Enqueue(optional);
        }

        var operations = new List<ISurgeryOperation>();
        var modifiedPassives = new List<PassiveSkill>(reference.EffectivePassives);

        foreach (var toAdd in missingRequiredPassives)
        {
            if (modifiedPassives.Count < GameConstants.MaxTotalPassives)
            {
                modifiedPassives.Add(toAdd);
                operations.Add(AddPassiveSurgeryOperation.NewCached(toAdd));
            }
            else if (removablePassives.TryDequeue(out var toRemove))
            {
                modifiedPassives.Remove(toRemove);
                modifiedPassives.Add(toAdd);
                operations.Add(ReplacePassiveSurgeryOperation.NewCached(toRemove, toAdd));
            }
        }

        return new SurgeryTablePalReference(reference, operations);
    }

    private IEnumerable<IPalReference> AddOptionalPassives(
        IPalReference reference,
        IReadOnlyList<PassiveSkill> surgeryCompatiblePassives
    )
    {
        var missingOptionalPassives = surgeryCompatiblePassives
            .Where(target.OptionalPassives.Contains)
            .Except(reference.EffectivePassives)
            .ToList();

        var numUsedSlots = reference.EffectivePassives.Count(passive => passive is not RandomPassiveSkill);
        var numAddablePassives = GameConstants.MaxTotalPassives - numUsedSlots;

        // Required surgery candidates were produced in this same frontier pass,
        // so include the input reference in the returned alternatives.
        var results = new List<IPalReference> { reference };

        foreach (
            var passives in missingOptionalPassives
                .Combinations(numAddablePassives, null)
                .Where(passives =>
                    reference.TotalCost + passives.Sum(passive => passive.SurgeryCost) <= settings.MaxSurgeryCost
                )
                .Where(passives => passives.Any())
                .Select(passives => passives.ToList())
        )
        {
            var removablePassives = new Queue<PassiveSkill>(reference.EffectivePassives.OfType<RandomPassiveSkill>());
            var modifiedPassives = new List<PassiveSkill>(reference.EffectivePassives);
            var operations = new List<ISurgeryOperation>();

            foreach (var toAdd in passives)
            {
                if (modifiedPassives.Count < GameConstants.MaxTotalPassives
                )
                {
                    modifiedPassives.Add(toAdd);
                    operations.Add(AddPassiveSurgeryOperation.NewCached(toAdd));
                }
                else if (removablePassives.TryDequeue(out var toRemove))
                {
                    modifiedPassives.Remove(toRemove);
                    modifiedPassives.Add(toAdd);
                    operations.Add(ReplacePassiveSurgeryOperation.NewCached(toRemove, toAdd));
                }
                else
                {
#if DEBUG_CHECKS
                    Debugger.Break();
#endif
                }
            }

            results.Add(new SurgeryTablePalReference(reference, operations));
        }

        return results;
    }

    private IEnumerable<IPalReference> EnforceRequiredGender(IPalReference input)
    {
        if (target.RequiredGender != PalGender.WILDCARD && input.Gender != target.RequiredGender)
        {
            if (input.Gender == PalGender.WILDCARD || settings.UseGenderReversers)
            {
                yield return input.WithGuaranteedGender(
                    settings.DB,
                    target.RequiredGender,
                    settings.UseGenderReversers
                );
            }

            yield break;
        }

        yield return input;
    }
}
