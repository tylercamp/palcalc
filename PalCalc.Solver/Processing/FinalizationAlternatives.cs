using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Processing;

internal readonly record struct BreedingPathMetrics(
    TimeSpan BreedingEffort,
    int TotalSpecialCakes
)
{
    public bool Covers(BreedingPathMetrics other) =>
        BreedingEffort <= other.BreedingEffort && TotalSpecialCakes <= other.TotalSpecialCakes;

    public bool Dominates(BreedingPathMetrics other) =>
        Covers(other) && this != other;
}

/// <summary>Finalization-only comparisons; these never partition the search frontier.</summary>
internal sealed class FinalizationAlternatives<T>(int capacity = 0)
{
    private readonly FinalizationComparison comparison = new();
    private readonly Dictionary<FinalizationComparison.Key, List<(T Result, BreedingPathMetrics Metrics)>> groups = new(capacity);

    public IEnumerable<T> Results
    {
        get
        {
            foreach (var group in groups.Values)
                foreach (var entry in group)
                    yield return entry.Result;
        }
    }

    internal (int GroupCount, int GroupCapacity, int PassiveSetCount) GetTableStatistics() =>
        (groups.Count, groups.EnsureCapacity(0), comparison.PassiveSetCount);

    public bool DominatesLowerBounds(IPalReference source, BreedingPathMetrics bounds) =>
        groups.TryGetValue(comparison.KeyOf(source), out var group) &&
        group.Any(existing => existing.Metrics.Dominates(bounds));

    public void Add(IPalReference source, BreedingPathMetrics metrics, T result)
    {
        var key = comparison.KeyOf(source);
        if (!groups.TryGetValue(key, out var group))
            groups.Add(key, group = new(1));
        if (group.Any(existing => existing.Metrics.Dominates(metrics)))
            return;
        group.RemoveAll(existing => metrics.Dominates(existing.Metrics));
        // Equal metrics can still describe meaningfully different breeding paths.
        group.Add((result, metrics));
    }
}

internal sealed class FinalizationComparison
{
    internal readonly record struct Key(
        Pal Pal, PalGender Gender,
        int EffectivePassivesId, int ActualPassivesId,
        IV_Set IVs,
        int BreedingSteps, int Gold, int WildPals, int RequiredLevels,
        string Inputs
    );

    private readonly record struct InputChoice(object Origin, PalGender Gender, LevelRequirements Levels);
    private readonly Dictionary<PassiveSetKey, int> passiveSetIds = [];
    private readonly Dictionary<InputChoice, int> inputIds = [];
    private readonly Dictionary<IPalReference, int[]> inputs = new(ReferenceEqualityComparer.Instance);

    internal int PassiveSetCount => passiveSetIds.Count;

    // TODO - Would rather avoid this massive sort of combined key, if possible
    public Key KeyOf(IPalReference reference) => new(
        reference.Pal, reference.Gender,
        PassiveSetId(reference.EffectivePassives), PassiveSetId(reference.ActualPassives),
        reference.IVs,
        reference.NumTotalBreedingSteps, reference.TotalCost, reference.NumTotalWildPals, reference.TotalRequiredLevels,
        string.Join(",", InputsOf(reference))
    );

    // (a `PassiveSetKey` is relatively large, and worst-case there can be millions of interim records
    // during finalization. use a slimmer, intern'd ID instead of full `PassiveSetKey` for `Key`)
    private int PassiveSetId(IReadOnlyList<PassiveSkill> passives)
    {
        // Keep the full set in this shared table so each finalist only stores its ID.
        var key = new PassiveSetKey(passives);
        if (!passiveSetIds.TryGetValue(key, out var id))
            passiveSetIds.Add(key, id = passiveSetIds.Count);
        return id;
    }

    private int[] InputsOf(IPalReference reference)
    {
        if (inputs.TryGetValue(reference, out var cached))
            return cached;
        int[] result;
        if (reference is BredPalReference bred)
            result = MergeSortedInputs(InputsOf(bred.Parent1), InputsOf(bred.Parent2));
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

    private static int[] MergeSortedInputs(int[] first, int[] second)
    {
        var merged = new int[first.Length + second.Length];
        int i = 0, j = 0;
        while (i < first.Length && j < second.Length)
        {
            var index = i + j;
            merged[index] = first[i] <= second[j] ? first[i++] : second[j++];
        }

        first.AsSpan(i).CopyTo(merged.AsSpan(i + j));
        second.AsSpan(j).CopyTo(merged.AsSpan(first.Length + j));
        return merged;
    }
}
