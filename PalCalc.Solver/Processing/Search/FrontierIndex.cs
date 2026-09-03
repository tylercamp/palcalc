using System.Numerics;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Search;

/// <summary>
/// Stores the candidates retained by the search frontier, indexed by effective properties.
/// </summary>
internal sealed class FrontierIndex(IEffectivePropertiesKeyProvider keyProvider)
{
    private readonly Dictionary<EffectivePropertiesKey, List<IPalReference>> content = [];

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

        if (group.Contains(reference))
            return;

        // Assessment stops at the first incumbent which proves a candidate
        // inferior. Keep only the most likely dominator first; sorting the rest
        // would add insertion work without improving that first comparison.
        if (group.Count != 0 && CompareIncumbentPriority(reference, group[0]) < 0)
        {
            group.Add(group[0]);
            group[0] = reference;
        }
        else
        {
            group.Add(reference);
        }
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

    public IReadOnlyList<IPalReference> this[EffectivePropertiesKey key] =>
        content.GetValueOrDefault(key);

    private static int CompareIncumbentPriority(
        IPalReference left,
        IPalReference right
    )
    {
        var comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
        if (comparison != 0) return comparison;

        comparison = right.AttackProfile.HasNoopAttack.CompareTo(
            left.AttackProfile.HasNoopAttack
        );
        if (comparison != 0) return comparison;

        comparison = BitOperations.PopCount(right.AttackProfile.StructurallyCoveredTargetMasks)
            .CompareTo(BitOperations.PopCount(left.AttackProfile.StructurallyCoveredTargetMasks));
        if (comparison != 0) return comparison;

        comparison = left.TotalCost.CompareTo(right.TotalCost);
        if (comparison != 0) return comparison;

        comparison = TotalMaxIV(right).CompareTo(TotalMaxIV(left));
        if (comparison != 0) return comparison;

        return TotalMinIV(right).CompareTo(TotalMinIV(left));
    }

    private static int TotalMaxIV(IPalReference candidate) =>
        ScoreOf(candidate.IVs.HP, maximum: true) +
        ScoreOf(candidate.IVs.Attack, maximum: true) +
        ScoreOf(candidate.IVs.Defense, maximum: true);

    private static int TotalMinIV(IPalReference candidate) =>
        ScoreOf(candidate.IVs.HP, maximum: false) +
        ScoreOf(candidate.IVs.Attack, maximum: false) +
        ScoreOf(candidate.IVs.Defense, maximum: false);

    private static int ScoreOf(IV_Value iv, bool maximum) =>
        iv == IV_Value.Random
            ? 0
            : maximum ? iv.Max : iv.Min;
}
