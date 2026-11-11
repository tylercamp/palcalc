using System.Numerics;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Search;

/// <summary>Retains the cheapest and fastest paths for each exact attack mask, or one group without attack targets.</summary>
/// <param name="hasRequiredAttacks">Whether specific attacks have been requested, which affects the comparison metrics</param>
/// <param name="markReplacedOutdated">Whether to modify old candidates and mark them as "outdated" when they get replaced</param>
internal sealed class CandidateCostFrontier(bool hasRequiredAttacks, bool markReplacedOutdated)
{
    private readonly IPalReference[] cheapest = new IPalReference[hasRequiredAttacks ? AttackProfile.TargetMaskCount : 1];
    private readonly IPalReference[] fastest = new IPalReference[hasRequiredAttacks ? AttackProfile.TargetMaskCount : 1];
    private readonly Dictionary<IPalReference, int> counts = new(ReferenceEqualityComparer.Instance);

    public IEnumerable<IPalReference> Candidates => counts.Keys;

    public bool TryAdd(IPalReference candidate)
    {
        var improved = ImprovedMasks(
            candidate.AttackProfile.EntryTargetMasks,
            candidate.BreedingEffort, candidate.TotalCost, candidate.TotalRequiredLevels, candidate.BaseSpecialCakes,
            candidate.GetHashCode()
        );
        if ((improved.Cheapest | improved.Fastest) == 0) return false;
        Add(candidate, improved.Cheapest, cheapest);
        Add(candidate, improved.Fastest, fastest);
        return true;
    }

    public bool TryAdd(ref CandidateDraft candidate)
    {
        var improved = ImprovedMasks(
            candidate.AttackProfile.EntryTargetMasks,
            candidate.BreedingEffort, candidate.TotalCost, candidate.TotalRequiredLevels, candidate.BaseSpecialCakes,
            candidate.GetHashCode()
        );
        if ((improved.Cheapest | improved.Fastest) == 0) return false;
        var materialized = candidate.Materialize();
        Add(materialized, improved.Cheapest, cheapest);
        Add(materialized, improved.Fastest, fastest);
        return true;
    }

    private (ulong Cheapest, ulong Fastest) ImprovedMasks(
        ulong masks,
        TimeSpan effort, int gold, int levels, int cakes,
        int hash
    )
    {
        // A passive-only run has one comparison group and needs no attack profile.
        if (!hasRequiredAttacks) masks = 1;
        ulong improvedCheapest = 0, improvedFastest = 0;
        while (masks != 0)
        {
            var slot = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            if (cheapest[slot] is not { } cheap || ComparePreference(cheap, effort, gold, levels, cakes, hash, preferCakes: true) > 0)
                improvedCheapest |= 1UL << slot;
            if (fastest[slot] is not { } fast || ComparePreference(fast, effort, gold, levels, cakes, hash, preferCakes: false) > 0)
                improvedFastest |= 1UL << slot;
        }
        return (improvedCheapest, improvedFastest);
    }

    private void Add(IPalReference candidate, ulong masks, IPalReference[] champions)
    {
        while (masks != 0)
        {
            var slot = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            var previous = champions[slot];
            if (previous is not null && --counts[previous] == 0)
            {
                counts.Remove(previous);
                if (markReplacedOutdated) previous.IsOutdated = true;
            }
            champions[slot] = candidate;
            counts.TryGetValue(candidate, out var count);
            counts[candidate] = count + 1;
        }
    }

    // shortcut: we drop middle cake/time tradeoffs to bound parent-pair growth;
    // revisit this approximation if searches need those compromises.
    private static int ComparePreference(
        IPalReference left,
        TimeSpan effort, int gold, int levels, int cakes,
        int hash, bool preferCakes
    )
    {
        var comparison = preferCakes ? left.BaseSpecialCakes.CompareTo(cakes) : left.BreedingEffort.CompareTo(effort);
        if (comparison != 0) return comparison;
        comparison = preferCakes ? left.BreedingEffort.CompareTo(effort) : left.BaseSpecialCakes.CompareTo(cakes);
        if (comparison != 0) return comparison;
        comparison = left.TotalCost.CompareTo(gold);
        if (comparison != 0) return comparison;
        comparison = left.TotalRequiredLevels.CompareTo(levels);
        return comparison != 0 ? comparison : left.GetHashCode().CompareTo(hash);
    }
}
