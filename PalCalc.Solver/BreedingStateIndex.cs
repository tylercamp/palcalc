using PalCalc.Solver.PalReference;

namespace PalCalc.Solver;

internal sealed class BreedingStateIndex(IBreedingStateKeyProvider keyProvider)
{
    private readonly Dictionary<BreedingStateKey, List<IPalReference>> content = [];

    public IEnumerable<IPalReference> All =>
        content.Values.SelectMany(group => group);

    public void Add(IPalReference reference)
    {
        var key = keyProvider.KeyOf(reference);
        if (!content.TryGetValue(key, out var group))
        {
            group = [];
            content.Add(key, group);
        }

        if (!group.Contains(reference))
            group.Add(reference);
    }

    public void AddRange(IEnumerable<IPalReference> references)
    {
        foreach (var reference in references)
            Add(reference);
    }

    public void Remove(IPalReference reference)
    {
        if (content.TryGetValue(keyProvider.KeyOf(reference), out var group))
            group.Remove(reference);
    }

    public IReadOnlyList<IPalReference> this[IPalReference reference] =>
        this[keyProvider.KeyOf(reference)];

    public IReadOnlyList<IPalReference> this[BreedingStateKey key] =>
        content.GetValueOrDefault(key);
}
