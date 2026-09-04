using System.Numerics;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Allocation-free view of the composer's current accumulator. A non-cached
/// value must be consumed or materialized before the next call to <see cref="AttackProfileComposer.Prepare"/>.
/// </summary>
internal readonly struct PreparedAttackProfile
{
    private readonly AttackProfileComposer composer;
    private readonly AttackProfile cached;
    private readonly int cacheKey;
    private readonly bool isCached;
    private readonly bool isCacheable;

    internal PreparedAttackProfile(
        AttackProfileComposer composer,
        int cacheKey,
        bool isCacheable
    )
    {
        this.composer = composer;
        this.cacheKey = cacheKey;
        this.isCacheable = isCacheable;
        cached = default;
        isCached = false;
    }

    internal PreparedAttackProfile(AttackProfile cached)
    {
        this.cached = cached;
        composer = null;
        cacheKey = 0;
        isCached = true;
        isCacheable = false;
    }

    public ulong EntryTargetMasks => isCached
        ? cached.EntryTargetMasks
        : composer.PreparedEntryTargetMasks;

    public bool Contains(byte requiredMask)
    {
        var masks = EntryTargetMasks;
        while (masks != 0)
        {
            var mask = BitOperations.TrailingZeroCount(masks);
            masks &= masks - 1;
            if ((mask & requiredMask) == requiredMask)
                return true;
        }
        return false;
    }

    public AttackProfileEntry EntryForMask(int mask)
    {
        if (!isCached)
            return composer.PreparedEntryForMask(mask);

        foreach (ref readonly var entry in cached.EntriesSpan)
            if (entry.LearnedTargetMask == mask)
                return entry;
        throw new ArgumentOutOfRangeException(nameof(mask));
    }

    public AttackProfile Materialize() => isCached
        ? cached
        : composer.MaterializePrepared(cacheKey, isCacheable);

    public override int GetHashCode() => isCached
        ? cached.GetHashCode()
        : composer.PreparedHashCode;
}

/// <summary>
/// Combines two parent profiles into the abstract outcomes used during search.
/// Concrete inheritance choices are reconstructed later by <see cref="AttackResultMaterializer"/>.
/// </summary>
internal sealed class AttackProfileComposer(
    AttackTargetContext targets,
    BreedingSolverSettings settings
)
{
    private const int TargetMaskBitCount = PalSpecifier.MaxRequiredAttacks;
    private const int TargetMaskCount = AttackProfile.TargetMaskCount;
    private const int MaxEquippedAttacksPerParent = 3;
    private const int PackedParentLoadoutShift = 8;

    private readonly AttackProfileReducer.Accumulator accumulator = new();
    private readonly Dictionary<int, AttackProfile> cachedProfiles = [];
    private Pal cachedChild;
    private IPalReference cachedParent1;
    private IPalReference cachedParent2;

    /// <summary>
    /// Calculates the minimum estimated Special Cake cost for each available
    /// exact attack mask. Attack probability and effort are reconstructed later.
    /// </summary>
    public AttackProfile Compose(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability
    )
        => Prepare(
            child,
            parent1,
            parent2,
            passivesProbability,
            ivsProbability
        ).Materialize();

    internal PreparedAttackProfile Prepare(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability
    )
    {
        if (!targets.IsActive)
            return new(AttackProfile.Inactive);

        var baseProbability = passivesProbability * ivsProbability;
        if (
            !ReferenceEquals(child, cachedChild) ||
            !ReferenceEquals(parent1, cachedParent1) ||
            !ReferenceEquals(parent2, cachedParent2)
        )
        {
            cachedProfiles.Clear();
            cachedChild = child;
            cachedParent1 = parent1;
            cachedParent2 = parent2;
        }

        if (baseProbability > 0)
        {
            // For fixed parents and child, composition depends on probability only
            // through this rounded Special Cake cost. With cakes disabled it does
            // not depend on probability at all.
            var cacheKey = settings.MaxSpecialCakes == 0
                ? 0
                : (int)Math.Ceiling(1f / baseProbability);
            if (cachedProfiles.TryGetValue(cacheKey, out var cached))
                return new(cached);

            accumulator.Reset(targets.StateOf(child).HasNooplLevel1Attack);
            Enumerate(
                child,
                parent1,
                parent2,
                passivesProbability,
                ivsProbability,
                accumulator
            );
            return new(this, cacheKey, isCacheable: true);
        }

        accumulator.Reset(targets.StateOf(child).HasNooplLevel1Attack);
        Enumerate(
            child,
            parent1,
            parent2,
            passivesProbability,
            ivsProbability,
            accumulator
        );
        return new(this, cacheKey: 0, isCacheable: false);
    }

    internal ulong PreparedEntryTargetMasks => accumulator.OccupiedMasks;
    internal int PreparedHashCode => accumulator.CalculateHashCode();
    internal AttackProfileEntry PreparedEntryForMask(int mask) =>
        accumulator.EntryForMask(mask);

    internal AttackProfile MaterializePrepared(int cacheKey, bool isCacheable)
    {
        var profile = accumulator.Build();
        if (isCacheable)
            cachedProfiles.Add(cacheKey, profile);
        return profile;
    }

    private void Enumerate(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability,
        AttackProfileReducer.Accumulator entries
    )
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);

        var parent1Profile = parent1.AttackProfile;
        var parent2Profile = parent2.AttackProfile;
        var inheritableTargetMask = targets.InheritableTargetMask;
        var maxSpecialCakes = settings.MaxSpecialCakes;
        var baseProbability = passivesProbability * ivsProbability;
        if (baseProbability <= 0)
            return;

        var innateMask = targets.StateOf(child).Level1TargetMask;

        var parent1Entries = parent1Profile.EntriesSpan;
        var parent2Entries = parent2Profile.EntriesSpan;
        // Normal inheritance transfers at most one target attack. For each parent,
        // the categories below retain its cheapest unrestricted entry plus its
        // cheapest entry with and without each of the six target bits. Those
        // thirteen champions are sufficient to evaluate the baseline and the
        // three parent-presence combinations which can transfer each target,
        // without evaluating the full parent-profile Cartesian product.
        const int normalCategoryCount = TargetMaskBitCount * 2;
        const int anyCategory = normalCategoryCount;
        Span<int> parent1CategoryCakes = stackalloc int[normalCategoryCount + 1];
        Span<int> parent2CategoryCakes = stackalloc int[normalCategoryCount + 1];
        parent1CategoryCakes.Fill(int.MaxValue);
        parent2CategoryCakes.Fill(int.MaxValue);

        BuildCategoryCakes(parent1Entries, parent1CategoryCakes);
        BuildCategoryCakes(parent2Entries, parent2CategoryCakes);

        EmitBestNormal(
            parent1CategoryCakes,
            parent2CategoryCakes,
            anyCategory,
            anyCategory,
            innateMask
        );

        var normalTargetBits = (byte)(inheritableTargetMask & ~innateMask);
        while (normalTargetBits != 0)
        {
            var bit = (byte)(normalTargetBits & -normalTargetBits);
            normalTargetBits &= (byte)~bit;
            var bitIndex = BitOperations.TrailingZeroCount((uint)bit);
            var withoutAttack = bitIndex << 1;
            var withAttack = withoutAttack + 1;

            // At least one parent must carry the target. These are the only
            // three presence combinations which can produce it.
            EmitBestNormal(
                parent1CategoryCakes, parent2CategoryCakes,
                withAttack, withoutAttack, (byte)(innateMask | bit)
            );
            EmitBestNormal(
                parent1CategoryCakes, parent2CategoryCakes,
                withoutAttack, withAttack, (byte)(innateMask | bit)
            );
            EmitBestNormal(
                parent1CategoryCakes, parent2CategoryCakes,
                withAttack, withAttack, (byte)(innateMask | bit)
            );
        }

        if (maxSpecialCakes == 0)
            return;

        var edgeSpecialCakes = (int)Math.Ceiling(1f / baseProbability);

        foreach (var parent1Entry in parent1Entries)
            foreach (var parent2Entry in parent2Entries)
            {
                var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;
                var totalCakes = parentCakes + edgeSpecialCakes;
                if (maxSpecialCakes is int maximumSpecialCakes && totalCakes > maximumSpecialCakes)
                    continue;

                var parent1Mask = (byte)(parent1Entry.LearnedTargetMask & inheritableTargetMask);
                var parent2Mask = (byte)(parent2Entry.LearnedTargetMask & inheritableTargetMask);
                var cakeLoadouts = CakeMaskCache.Values[
                    (parent1Mask << TargetMaskBitCount) | parent2Mask
                ];
                for (var i = 0; i < cakeLoadouts.Length; i++)
                {
                    var loadouts = cakeLoadouts[i];
                    var parent1Loadout = (byte)(loadouts >> PackedParentLoadoutShift);
                    var parent2Loadout = (byte)loadouts;
                    Emit(
                        totalCakes,
                        (byte)(innateMask | parent1Loadout | parent2Loadout)
                    );
                }
            }

        void BuildCategoryCakes(
            ReadOnlySpan<AttackProfileEntry> profileEntries,
            Span<int> minimumCakes
        )
        {
            foreach (ref readonly var entry in profileEntries)
            {
                minimumCakes[anyCategory] = Math.Min(
                    minimumCakes[anyCategory],
                    entry.TotalSpecialCakes
                );

                var entryMask = (byte)(entry.LearnedTargetMask & inheritableTargetMask);
                for (var bitIndex = 0; bitIndex < TargetMaskBitCount; bitIndex++)
                {
                    var category = (bitIndex << 1) + ((entryMask >> bitIndex) & 1);
                    minimumCakes[category] = Math.Min(
                        minimumCakes[category],
                        entry.TotalSpecialCakes
                    );
                }
            }
        }

        void EmitBestNormal(
            ReadOnlySpan<int> firstMinimumCakes,
            ReadOnlySpan<int> secondMinimumCakes,
            int parent1Category,
            int parent2Category,
            byte childMask
        )
        {
            var parent1Cakes = firstMinimumCakes[parent1Category];
            var parent2Cakes = secondMinimumCakes[parent2Category];
            if (parent1Cakes == int.MaxValue || parent2Cakes == int.MaxValue)
                return;

            Emit(
                parent1Cakes + parent2Cakes,
                childMask
            );
        }

        void Emit(
            int totalCakes,
            byte childMask
        )
        {
            if (maxSpecialCakes is int maximumSpecialCakes && totalCakes > maximumSpecialCakes)
                return;

            if (!entries.CouldImprove(childMask, totalCakes))
                return;

            entries.Add(new AttackProfileEntry(childMask, totalCakes));
        }
    }

    /// <summary>
    /// Writes the inclusion-maximal attack unions attainable with at most three
    /// attacks equipped by each parent. Each packed result contains one legal
    /// parent-loadout witness: parent 1 in the high byte and parent 2 in the low byte.
    /// </summary>
    internal static int EnumerateCakeMasks(
        byte parent1Mask,
        byte parent2Mask,
        Span<ushort> destination
    )
    {
        var count = 0;
        var availableMask = (byte)(parent1Mask | parent2Mask);
        for (var candidate = 0; candidate < TargetMaskCount; candidate++)
        {
            var childMask = (byte)candidate;
            if ((childMask & ~availableMask) != 0 ||
                !IsCakeMaskFeasible(parent1Mask, parent2Mask, childMask))
                continue;

            var isMaximal = true;
            var missingMask = (byte)(availableMask & ~childMask);
            for (var bit = 1; bit < TargetMaskCount; bit <<= 1)
            {
                if ((missingMask & bit) != 0 && IsCakeMaskFeasible(
                    parent1Mask, parent2Mask, (byte)(childMask | bit)
                ))
                {
                    isMaximal = false;
                    break;
                }
            }

            if (!isMaximal)
                continue;
            if (count == destination.Length)
                throw new ArgumentException("The destination is too small.", nameof(destination));

            destination[count++] = CreateCakeLoadouts(parent1Mask, parent2Mask, childMask);
        }

        return count;
    }

    // Attacks available from only one parent must fit that parent's three equipped
    // slots. Shared attacks may be assigned to either parent; the six-total check
    // guarantees the remaining shared attacks can be split between them.
    private static bool IsCakeMaskFeasible(byte parent1Mask, byte parent2Mask, byte childMask) =>
        BitOperations.PopCount((uint)childMask) <= TargetMaskBitCount &&
        BitOperations.PopCount((uint)(childMask & ~parent2Mask)) <= MaxEquippedAttacksPerParent &&
        BitOperations.PopCount((uint)(childMask & ~parent1Mask)) <= MaxEquippedAttacksPerParent;

    private static ushort CreateCakeLoadouts(byte parent1Mask, byte parent2Mask, byte childMask)
    {
        var parent1Loadout = (byte)(childMask & parent1Mask);
        var parent2Loadout = (byte)(childMask & parent2Mask);
        TrimDuplicateAttacks(ref parent1Loadout, parent2Loadout);
        TrimDuplicateAttacks(ref parent2Loadout, parent1Loadout);
        return (ushort)((parent1Loadout << PackedParentLoadoutShift) | parent2Loadout);
    }

    private static void TrimDuplicateAttacks(ref byte loadout, byte otherLoadout)
    {
        while (BitOperations.PopCount((uint)loadout) > MaxEquippedAttacksPerParent)
        {
            var duplicateMask = (byte)(loadout & otherLoadout);
            var duplicateBit = 1 << BitOperations.TrailingZeroCount((uint)duplicateMask);
            loadout = (byte)(loadout & ~duplicateBit);
        }
    }

    private static class CakeMaskCache
    {
        // Loadout feasibility depends only on the two six-bit learned masks, so
        // precompute all 64 x 64 combinations once. Only maximal unions are kept:
        // extra desired attacks never hurt, and the player may equip a subset later.
        public static readonly ushort[][] Values = Build();

        private static ushort[][] Build()
        {
            var result = new ushort[TargetMaskCount * TargetMaskCount][];
            Span<ushort> buffer = stackalloc ushort[TargetMaskCount];
            for (var parent1Mask = 0; parent1Mask < TargetMaskCount; parent1Mask++)
                for (var parent2Mask = 0; parent2Mask < TargetMaskCount; parent2Mask++)
                {
                    var count = EnumerateCakeMasks(
                        (byte)parent1Mask,
                        (byte)parent2Mask,
                        buffer
                    );
                    result[(parent1Mask << TargetMaskBitCount) | parent2Mask] = buffer[..count].ToArray();
                }

            return result;
        }
    }
}
