using System.Numerics;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// Computes the child's search-time <see cref="AttackProfile"/> from the two parents'
/// profiles: which requested attacks can end up on the child, using the chosen
/// breeding method. Concrete inheritance choices are reconstructed later by
/// <see cref="AttackResultMaterializer"/>.
/// </summary>
internal sealed class AttackProfileComposer(
    AttackTargetContext targets,
    BreedingSolverSettings settings
)
{
    private const int TargetMaskBitCount = PalSpecifier.MaxRequiredAttacks;
    private const int TargetMaskCount = AttackProfile.TargetMaskCount;
    private const int MaxEquippedAttacksPerParent = 3;

    private Pal cachedChild;
    private IPalReference cachedParent1;
    private IPalReference cachedParent2;
    private AttackProfile? ordinaryProfile;
    private AttackProfile? cakeProfile;

    internal AttackProfile Compose(Pal child, IPalReference parent1, IPalReference parent2, BreedingMethod method)
    {
        if (!targets.HasRequiredAttacks)
            return AttackProfile.Inactive;

        if (!ReferenceEquals(child, cachedChild) ||
            !ReferenceEquals(parent1, cachedParent1) ||
            !ReferenceEquals(parent2, cachedParent2))
        {
            cachedChild = child;
            cachedParent1 = parent1;
            cachedParent2 = parent2;
            ordinaryProfile = cakeProfile = null;
        }

        return method switch
        {
            BreedingMethod.Ordinary => ordinaryProfile ??= ComposeUncached(child, parent1.AttackProfile, parent2.AttackProfile, method),
            BreedingMethod.SpecialCake => cakeProfile ??= ComposeUncached(child, parent1.AttackProfile, parent2.AttackProfile, method),
            _ => throw new ArgumentOutOfRangeException(nameof(method)),
        };
    }

    private AttackProfile ComposeUncached(Pal child, AttackProfile first, AttackProfile second, BreedingMethod method)
    {
        var state = targets.StateOf(child);
        var innateMask = state.Level1TargetMask;
        var inheritableMask = targets.InheritableTargetMask;
        ulong masks = 0;
        if (first.EntryTargetMasks == 0 || second.EntryTargetMasks == 0)
            return new(masks, state.HasNooplLevel1Attack);

        if (method == BreedingMethod.Ordinary)
        {
            masks = 1UL << innateMask;
            // Normal inheritance transfers at most one target attack. We only need
            // to know which attacks are available from either parent, so we can skip
            // the full Cartesian product of the two parent profiles.
            var available = (AvailableAttacks(first) | AvailableAttacks(second)) & inheritableMask & ~innateMask;
            while (available != 0)
            {
                var bit = available & -available;
                available &= available - 1;
                masks |= 1UL << (innateMask | bit);
            }
        }
        else if (settings.SpecialCakePolicy != SpecialCakePolicy.Never)
        {
            for (var firstMasks = first.EntryTargetMasks; firstMasks != 0; firstMasks &= firstMasks - 1)
                for (var secondMasks = second.EntryTargetMasks; secondMasks != 0; secondMasks &= secondMasks - 1)
                {
                    var firstMask = BitOperations.TrailingZeroCount(firstMasks) & inheritableMask;
                    var secondMask = BitOperations.TrailingZeroCount(secondMasks) & inheritableMask;
                    foreach (var loadouts in CakeMaskCache.Values[(firstMask << TargetMaskBitCount) | secondMask])
                    {
                        var inherited = (byte)((loadouts >> 8) | (byte)loadouts);
                        if (AllowsSpecialCake(settings.SpecialCakePolicy, innateMask, inherited))
                            masks |= 1UL << (innateMask | inherited);
                    }
                }
        }

        return new(masks, state.HasNooplLevel1Attack);
    }

    private static int AvailableAttacks(AttackProfile profile)
    {
        var available = 0;
        for (var masks = profile.EntryTargetMasks; masks != 0; masks &= masks - 1)
            available |= BitOperations.TrailingZeroCount(masks);
        return available;
    }

    // With the attack-only policy, only consider cakes when at least two distinct
    // requested attacks need inheritance; the child's level-one attacks already come for free.
    internal static bool AllowsSpecialCake(SpecialCakePolicy policy, byte innateMask, byte inheritedMask) =>
        policy switch
        {
            SpecialCakePolicy.Never => false,
            SpecialCakePolicy.AttackInheritanceOnly =>
                BitOperations.PopCount((uint)(inheritedMask & ~innateMask)) > 1,
            SpecialCakePolicy.AttacksAndPassives => true,
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };

    /// <summary>
    /// For special-cake inheritance, writes the inclusion-maximal unions of target
    /// attacks the child can inherit, given that each parent can contribute at most
    /// three equipped attacks. Each packed result is one legal loadout witness:
    /// parent 1 in the high byte, parent 2 in the low byte.
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

    internal static IReadOnlyList<ushort> CakeMasksFor(byte parent1Mask, byte parent2Mask) =>
        CakeMaskCache.Values[(parent1Mask << TargetMaskBitCount) | parent2Mask];

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
        return (ushort)((parent1Loadout << 8) | parent2Loadout);
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
