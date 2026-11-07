using System.Numerics;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Search;

/// <summary>Retains effort and cake tradeoffs, partitioned by exact attack mask when needed.</summary>
internal sealed class CandidateCostFrontier(bool hasRequiredAttacks, bool markReplacedOutdated = false)
{
    private readonly List<IPalReference>[] slots = new List<IPalReference>[hasRequiredAttacks ? AttackProfile.TargetMaskCount : 1];
    private readonly Dictionary<IPalReference, int> counts = new(ReferenceEqualityComparer.Instance);

    public IEnumerable<IPalReference> Candidates => counts.Keys;

    public bool TryAdd(IPalReference candidate)
    {
        var improved = ImprovedMasks(
            candidate.AttackProfile.EntryTargetMasks,
            candidate.BreedingEffort, candidate.TotalCost, candidate.TotalRequiredLevels, candidate.BaseSpecialCakes,
            candidate.GetHashCode()
        );
        if (improved == 0) return false;
        Add(candidate, improved);
        return true;
    }

    public bool TryAdd(ref CandidateDraft candidate)
    {
        var improved = ImprovedMasks(
            candidate.AttackProfile.EntryTargetMasks,
            candidate.BreedingEffort, candidate.TotalCost, candidate.TotalRequiredLevels, candidate.BaseSpecialCakes,
            candidate.GetHashCode()
        );
        if (improved == 0) return false;
        Add(candidate.Materialize(), improved);
        return true;
    }

    private ulong ImprovedMasks(
        ulong masks,
        TimeSpan effort, int gold, int levels, int cakes,
        int hash
    )
    {
        // A passive-only run has one comparison group and needs no attack profile.
        if (!hasRequiredAttacks) masks = 1;
        ulong improved = 0;
        while (masks != 0)
        {
            var slot = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            if (slots[slot] is not { } existing || !IsCovered(existing, effort, gold, levels, cakes, hash))
                improved |= 1UL << slot;
        }
        return improved;
    }

    private static bool IsCovered(
        List<IPalReference> existing,
        TimeSpan effort, int gold, int levels, int cakes,
        int hash
    )
    {
        foreach (var candidate in existing)
        {
            if (candidate.BaseSpecialCakes <= cakes &&
                ComparePreference(
                    candidate,
                    effort, gold, levels,
                    hash, breakTies: candidate.BaseSpecialCakes == cakes
                ) <= 0
            )
                return true;
        }
        return false;
    }

    private void Add(IPalReference candidate, ulong masks)
    {
        var cakes = candidate.BaseSpecialCakes;
        while (masks != 0)
        {
            var slot = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            var existing = slots[slot] ??= [];
            for (var i = existing.Count - 1; i >= 0; i--)
            {
                var previous = existing[i];
                if (cakes > previous.BaseSpecialCakes ||
                    ComparePreference(
                        candidate,
                        previous.BreedingEffort, previous.TotalCost, previous.TotalRequiredLevels,
                        previous.GetHashCode(), breakTies: cakes == previous.BaseSpecialCakes
                    ) > 0
                )
                    continue;

                existing.RemoveAt(i);
                if (--counts[previous] == 0)
                {
                    counts.Remove(previous);
                    if (markReplacedOutdated) previous.IsOutdated = true;
                }
            }
            existing.Add(candidate);
            counts.TryGetValue(candidate, out var count);
            counts[candidate] = count + 1;
        }
    }

    // Keep the existing effort-first preferences within each cake tier, but don't
    // let a cheaper, slower tier erase a faster path before its children are tried.
    private static int ComparePreference(
        IPalReference left,
        TimeSpan effort, int gold, int levels,
        int hash, bool breakTies
    )
    {
        var comparison = left.BreedingEffort.CompareTo(effort);
        if (comparison != 0) return comparison;
        comparison = left.TotalCost.CompareTo(gold);
        if (comparison != 0) return comparison;
        comparison = left.TotalRequiredLevels.CompareTo(levels);
        return comparison != 0 || !breakTies ? comparison : left.GetHashCode().CompareTo(hash);
    }
}
