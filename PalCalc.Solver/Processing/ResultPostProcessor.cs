using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.Utils;
using Serilog;
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
    AttackTargetContext attackTargets,
    ICandidateSelectionPolicy selectionPolicy
)
{
    private static ILogger logger = Log.ForContext<ResultPostProcessor>();

    /// <summary>
    /// Updates the `frontier` by applying surgery operations to its contents. Pals
    /// in `extraResults` can be provided to maintain compatibility with `ResultAccumulator`:
    /// 
    /// The `ResultAccumulator` is used to preserve alternate, potentially less-optimal
    /// results and present more choices in the final list. This check only applies to
    /// Pals which fully satisfy the `PalSpecifier` target, and would skip Pals which only
    /// satisfy the target after surgery.
    /// 
    /// The `SurgeryFinalistAccumulator` can be used to collect these potential results,
    /// and those results can be used as `extraResults`.
    /// </summary>
    public void ApplySurgery(
        SearchFrontier frontier,
        IEnumerable<IPalReference> extraResults
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

        // Surgery should run once after breeding. It's more accurate to apply at
        // every solver step, but it drastically increases the search space for
        // little gain.
        var retained = extraResults?.ToArray() ?? [];
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
        logger.Debug("Finalizing results");
        var candidates = terminalResults
            .Results
            .SelectMany(EnforceRequiredGender)
            .Where(target.IsSatisfiedBy)
            .ToList();

        if (attackTargets?.HasRequiredAttacks != true)
        {
            if (settings.SpecialCakePolicy != SpecialCakePolicy.AttacksAndPassives)
                return selectionPolicy.SelectFinalResults(candidates).ToList();

            var paths = new FinalizationAlternatives<IPalReference>();
            foreach (var candidate in candidates)
            {
                controller.PauseIfRequested();
                if (controller.CancellationToken.IsCancellationRequested)
                    break;
                if (candidate.BreedingEffort <= settings.MaxEffort)
                    paths.Add(candidate, new(candidate.BreedingEffort, candidate.ExpectedSpecialCakes), candidate);
            }
            return selectionPolicy.SelectFinalResults(paths.Results).ToList();
        }

        candidates.Sort((left, right) => left.BaseSpecialCakes.CompareTo(right.BaseSpecialCakes));

        var materializer = new AttackResultMaterializer(attackTargets, settings, controller.CancellationToken);
        var alternatives = new FinalizationAlternatives<EvaluatedAttackResult>();
        foreach (var finalist in candidates)
        {
            controller.PauseIfRequested();
            if (controller.CancellationToken.IsCancellationRequested)
                break;

            var bounds = materializer.LowerBounds(finalist);
            if (alternatives.DominatesLowerBounds(finalist, bounds))
                continue;

            foreach (var evaluation in materializer.Evaluate(finalist, attackTargets.FullTargetMask))
            {
                if (!SatisfiesEvaluatedConstraints(evaluation.Metrics))
                {
                    continue;
                }
                alternatives.Add(finalist, evaluation.Metrics, evaluation);
            }
        }

        var materialized = alternatives.Results
            .Select(materializer.Materialize)
            .Where(SatisfiesMaterializedConstraints)
            .ToList();
        logger.Debug("Attack materialization: {Finalists} alternatives, {Nodes} cached nodes", materialized.Count, materializer.MaterializedCount);

        var res = selectionPolicy.SelectFinalResults(materialized).ToList();
        logger.Debug("Result finalization complete with {FinalCount} results", res.Count);
        return res;
    }

    // Search deliberately used estimated cake costs and structural effort.
    // Materialization has now reconstructed the exact probability, effort,
    // gender-adjusted attempts, and cake total. Cake totals let us compare result
    // tradeoffs; this final check enforces the target and effort limit.
    private bool SatisfiesMaterializedConstraints(IPalReference reference) =>
        System.Numerics.BitOperations.PopCount(reference.AttackProfile.EntryTargetMasks) == 1 &&
        target.IsSatisfiedBy(reference) &&
        reference.BreedingEffort <= settings.MaxEffort;

    private bool SatisfiesEvaluatedConstraints(BreedingPathMetrics result) =>
        result.BreedingEffort <= settings.MaxEffort;

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

        var requiredReference = AddRequiredPassives(reference, surgeryCompatiblePassives);
        foreach (var result in AddOptionalPassives(requiredReference, surgeryCompatiblePassives))
        {
            if (
                includeUnmodifiedReference ||
                (result is SurgeryTablePalReference surgery && surgery.Operations.Count > 0)
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
            (missingRequiredPassives.Sum(passive => passive.SurgeryCost) + reference.TotalCost > settings.MaxSurgeryCost)
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

    private IEnumerable<IPalReference> AddOptionalPassives(IPalReference reference, IReadOnlyList<PassiveSkill> surgeryCompatiblePassives)
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
                .Where(passives => reference.TotalCost + passives.Sum(passive => passive.SurgeryCost) <= settings.MaxSurgeryCost)
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

    private IEnumerable<PassiveSkill> RemovablePassives(IPalReference reference, bool preserveOptionalPassives)
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

    private static void RemovePassiveSlot(List<PassiveSkill> passives, PassiveSkill passive)
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
