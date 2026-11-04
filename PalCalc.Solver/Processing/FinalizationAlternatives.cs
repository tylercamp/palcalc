using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Processing;

/// <summary>Finalization-only comparisons; these never partition the search frontier.</summary>
internal sealed class FinalizationAlternatives
{
    private readonly FinalizationComparison comparison = new();
    private readonly Dictionary<FinalizationComparison.Key, List<EvaluatedAttackResult>> groups = [];

    public IEnumerable<EvaluatedAttackResult> Results => groups.Values.SelectMany(group => group);

    public bool DominatesLowerBounds(IPalReference source, AttackMaterializationMetrics bounds) =>
        groups.TryGetValue(comparison.KeyOf(source), out var group) &&
        group.Any(existing => existing.Metrics.Dominates(bounds));

    public void Add(EvaluatedAttackResult candidate)
    {
        var key = comparison.KeyOf(candidate.Source);
        if (!groups.TryGetValue(key, out var group))
            groups.Add(key, group = []);
        if (group.Any(existing => existing.Metrics.Dominates(candidate.Metrics)))
            return;
        group.RemoveAll(existing => candidate.Metrics.Dominates(existing.Metrics));
        // Equal metrics can still describe meaningfully different breeding paths.
        group.Add(candidate);
    }
}

internal sealed class FinalizationComparison
{
    internal readonly record struct Key(
        Pal Pal, PalGender Gender, PassiveSetKey Passives, PassiveSetKey ActualPassives,
        IV_Set IVs, int BreedingSteps, int Gold, int WildPals, int RequiredLevels, string Inputs);

    private readonly record struct InputChoice(object Origin, PalGender Gender, LevelRequirements Levels);
    private readonly Dictionary<InputChoice, int> inputIds = [];
    private readonly Dictionary<IPalReference, int[]> inputs = new(ReferenceEqualityComparer.Instance);

    // TODO - Would rather avoid this massive sort of combined key, if possible
    public Key KeyOf(IPalReference reference) => new(
        reference.Pal, reference.Gender, new(reference.EffectivePassives),
        new(reference.ActualPassives), reference.IVs, reference.NumTotalBreedingSteps,
        reference.TotalCost, reference.NumTotalWildPals, reference.TotalRequiredLevels,
        string.Join(",", InputsOf(reference))
    );

    private int[] InputsOf(IPalReference reference)
    {
        if (inputs.TryGetValue(reference, out var cached))
            return cached;
        int[] result;
        if (reference is BredPalReference bred)
            result = InputsOf(bred.Parent1).Concat(InputsOf(bred.Parent2)).Order().ToArray();
        else if (reference is SurgeryTablePalReference surgery)
            result = InputsOf(surgery.Input);
        else
        {
            // Ignore attack loadouts, but preserve concrete input choices and training.
            object origin = reference switch
            {
                OwnedPalReference owned => owned.UnderlyingInstance,
                CompositeOwnedPalReference composite =>
                    (composite.Male.UnderlyingInstance, composite.Female.UnderlyingInstance),
                WildPalReference wild => (wild.Pal, new PassiveSetKey(wild.EffectivePassives),
                    wild.IVs, wild.BreedingEffort),
                _ => reference,
            };
            var choice = new InputChoice(origin, reference.Gender, reference.LevelRequirements);
            if (!inputIds.TryGetValue(choice, out var id))
                inputIds.Add(choice, id = inputIds.Count);
            result = [id];
        }
        inputs.Add(reference, result);
        return result;
    }
}
