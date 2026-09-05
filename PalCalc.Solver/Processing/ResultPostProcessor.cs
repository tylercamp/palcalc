using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
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
    SolverStateController controller,
    AttackTargetContext attackTargets
)
{
    public void ApplySurgery(
        SearchFrontier frontier,
        IEnumerable<IPalReference> retainedSurgeryFinalists = null
    )
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
        var retained = retainedSurgeryFinalists?.ToArray() ?? [];
        frontier.ExpandSingles(palReferences =>
            palReferences
                .Concat(retained)
                .Distinct()
                .Where(reference => reference.Pal == target.Pal)
                .SelectMany(reference =>
                    reference is CompositeOwnedPalReference composite
                        ? ExpandSurgeryCandidates(
                            composite.Male,
                            surgeryCompatiblePassives,
                            includeUnmodifiedReference: false
                        ).Concat(
                            ExpandSurgeryCandidates(
                                composite.Female,
                                surgeryCompatiblePassives,
                                includeUnmodifiedReference: false
                            )
                        )
                        : ExpandSurgeryCandidates(
                            reference,
                            surgeryCompatiblePassives,
                            includeUnmodifiedReference: true
                        )
                )
                .TakeUntilCancelled(controller.CancellationToken)
        );
    }

    public List<IPalReference> Finalize(ResultAccumulator terminalResults)
    {
        var candidates = terminalResults
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
            .Where(SatisfiesTerminalTarget)
            .ToList();

        if (attackTargets?.IsActive != true)
            return terminalResults.SelectFinalResults(candidates).ToList();

        var finalists = candidates
            .Select(reference => (Reference: reference, Entry: SelectRootEntry(reference)))
            .Where(result => result.Entry is not null)
            .ToList();
        var materializer = new AttackResultMaterializer(attackTargets, settings);
        var materialized = new List<IPalReference>(finalists.Count);
        foreach (var finalist in finalists)
        {
            var result = materializer.Materialize(
                finalist.Reference,
                finalist.Entry!.Value
            );
            if (!SatisfiesMaterializedConstraints(result))
                continue;

            materialized.Add(result);
        }

        return terminalResults.SelectFinalResults(materialized).ToList();
    }

    private bool SatisfiesTerminalTarget(IPalReference reference) =>
        attackTargets?.Satisfies(reference) ?? target.IsSatisfiedBy(reference);

    private AttackProfileEntry? SelectRootEntry(IPalReference reference)
    {
        AttackProfileEntry? best = null;
        foreach (ref readonly var entry in reference.AttackProfile.EntriesSpan)
        {
            if ((entry.LearnedTargetMask & attackTargets.FullTargetMask) !=
                    attackTargets.FullTargetMask ||
                settings.MaxSpecialCakes is int maxCakes &&
                    entry.TotalSpecialCakes > maxCakes)
                continue;

            if (best is null || entry.TotalSpecialCakes < best.Value.TotalSpecialCakes)
                best = entry;
        }

        return best;
    }

    private bool SatisfiesMaterializedConstraints(IPalReference reference)
    {
        // Search deliberately used estimated cake costs and structural effort.
        // Materialization has now reconstructed the exact probability, effort,
        // gender-adjusted attempts, and cake total for the final constraint check.
        var entries = reference.AttackProfile.EntriesSpan;
        if (entries.Length != 1 || !SatisfiesTerminalTarget(reference))
            return false;

        if (reference.BreedingEffort > settings.MaxEffort)
            return false;

        return settings.MaxSpecialCakes is not int maxCakes ||
            entries[0].TotalSpecialCakes <= maxCakes;
    }

    private IEnumerable<IPalReference> ExpandSurgeryCandidates(
        IPalReference reference,
        IReadOnlyList<PassiveSkill> surgeryCompatiblePassives,
        bool includeUnmodifiedReference
    )
    {
        if (
            reference.EffectivePassives.Count >= GameConstants.MaxTotalPassives &&
            !RemovablePassives(reference, preserveOptionalPassives: false).Any()
        )
            yield break;

        var requiredReference = AddRequiredPassives(
            reference,
            surgeryCompatiblePassives
        );
        foreach (var result in AddOptionalPassives(
            requiredReference,
            surgeryCompatiblePassives
        ))
        {
            if (
                includeUnmodifiedReference ||
                result is SurgeryTablePalReference surgery && surgery.Operations.Count > 0
            )
                yield return result;
        }
    }

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

        var removablePassives = new Queue<PassiveSkill>(
            RemovablePassives(reference, preserveOptionalPassives: false)
        );

        var operations = new List<ISurgeryOperation>();
        var modifiedPassives = new List<PassiveSkill>(reference.ActualPassives);

        foreach (var toAdd in missingRequiredPassives)
        {
            if (modifiedPassives.Count < GameConstants.MaxTotalPassives)
            {
                modifiedPassives.Add(toAdd);
                operations.Add(new AddPassiveSurgeryOperation(toAdd));
            }
            else if (removablePassives.TryDequeue(out var toRemove))
            {
                RemovePassiveSlot(modifiedPassives, toRemove);
                modifiedPassives.Add(toAdd);
                operations.Add(new ReplacePassiveSurgeryOperation(toRemove, toAdd));
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

        var numUsedSlots = reference.ActualPassives.Count(
            passive =>
                passive is not RandomPassiveSkill &&
                target.DesiredPassives.Contains(passive)
        );
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
            var removablePassives = new Queue<PassiveSkill>(
                RemovablePassives(reference, preserveOptionalPassives: true)
            );
            var modifiedPassives = new List<PassiveSkill>(reference.ActualPassives);
            var operations = new List<ISurgeryOperation>();

            foreach (var toAdd in passives)
            {
                if (modifiedPassives.Count < GameConstants.MaxTotalPassives
                )
                {
                    modifiedPassives.Add(toAdd);
                    operations.Add(new AddPassiveSurgeryOperation(toAdd));
                }
                else if (removablePassives.TryDequeue(out var toRemove))
                {
                    RemovePassiveSlot(modifiedPassives, toRemove);
                    modifiedPassives.Add(toAdd);
                    operations.Add(new ReplacePassiveSurgeryOperation(toRemove, toAdd));
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

    private IEnumerable<PassiveSkill> RemovablePassives(
        IPalReference reference,
        bool preserveOptionalPassives
    )
    {
        return reference.ActualPassives
            .Where(p => !target.RequiredPassives.Contains(p))
            .OrderBy(p =>
            {
                if (p is RandomPassiveSkill) return 1; // Replace random passives first
                else if (!target.OptionalPassives.Contains(p)) return 2; // Then any specific irrelevant passives
                else return 3; // Then optionally-desired passives
            })
            .Where(p =>
            {
                if (preserveOptionalPassives)
                    return !target.OptionalPassives.Contains(p);
                else
                    return true;
            });
    }

    private static void RemovePassiveSlot(
        List<PassiveSkill> passives,
        PassiveSkill passive
    )
    {
        var index = passives.IndexOf(passive);
        if (index < 0)
            index = passives.FindIndex(candidate => candidate is RandomPassiveSkill);

        if (index >= 0)
            passives.RemoveAt(index);
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
