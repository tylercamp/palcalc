using System.Numerics;
using System.Runtime.InteropServices;
using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Attacks;

internal enum AttackCompositionMode
{
    Baseline,
    Normal,
    InheritAll,
}

/// <summary>
/// <para>>A fully selected inheritance outcome used to reconstruct a full attack profile.</para
/// <para>The search process gathers possibilities for each child, and this is used to select a specific outcome.</para>
/// </summary>
internal readonly record struct AttackCompositionChoice(
    AttackProfileEntry Parent1Entry,
    AttackProfileEntry Parent2Entry,
    AttackCompositionMode Mode,
    byte Parent1TargetMask,
    byte Parent2TargetMask,
    AttackProfileEntry ChildEntry,
    float AttackProbability
);

/// <summary>
/// <para>Used to combine the attack profiles of two parents into a single, merged attack profile.</para>
/// <para>A single profile can cover a range of possible outcomes (profile "entries".)</para>
/// </summary>
internal sealed class AttackProfileComposer(
    AttackTargetContext targets,
    BreedingSolverSettings settings,
    ObjectPoolFactory poolFactory
)
{
    private readonly LocalListPool<AttackProfileEntry> entryListPool = poolFactory.GetListPool<AttackProfileEntry>();

    /// <summary>
    /// Calculates a combined attack profile from the given parents. Profile entries which require multiple attempts
    /// (e.g. 50/50 odds instead of 100%) will include other mechanics which also require multiple attempts
    /// for accurate weighting (e.g. passive and IV probabilities.)
    /// </summary>
    public AttackProfile Compose(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability
    )
    {
        if (!targets.IsActive)
            return AttackProfile.Inactive;

        using var entriesRef = entryListPool.Borrow();
        Enumerate(child, parent1, parent2, passivesProbability, ivsProbability, entriesRef.Value, null);
        return AttackProfileReducer.Reduce(
            targets.StateOf(child).HasNooplLevel1Attack,
            CollectionsMarshal.AsSpan(entriesRef.Value)
        );
    }

    public IReadOnlyList<AttackCompositionChoice> EnumerateChoices(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability
    )
    {
        var choices = new List<AttackCompositionChoice>();
        if (targets.IsActive)
            Enumerate(child, parent1, parent2, passivesProbability, ivsProbability, null, choices);
        return choices;
    }

    private void Enumerate(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability,
        List<AttackProfileEntry> entries,
        List<AttackCompositionChoice> choices
    )
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);

        var baseProbability = passivesProbability * ivsProbability;
        if (baseProbability <= 0)
            return;

        var innateMask = targets.StateOf(child).Level1TargetMask;
        foreach (var parent1Entry in parent1.AttackProfile.Entries)
        foreach (var parent2Entry in parent2.AttackProfile.Entries)
        {
            Emit(
                parent1Entry,
                parent2Entry,
                AttackCompositionMode.Baseline,
                parent1Mask: 0,
                parent2Mask: 0,
                childMask: innateMask,
                attackProbability: 1,
                usesSpecialCake: false
            );

            var parent1Mask = (byte)(parent1Entry.LearnedTargetMask & targets.InheritableTargetMask);
            var parent2Mask = (byte)(parent2Entry.LearnedTargetMask & targets.InheritableTargetMask);
            var normalTargets = (byte)((parent1Mask | parent2Mask) & ~innateMask);
            for (var bit = (byte)1; bit != 0 && bit <= targets.FullTargetMask; bit <<= 1)
            {
                if ((normalTargets & bit) == 0)
                    continue;

                var parent1HasAttack = (parent1Mask & bit) != 0;
                var parent2HasAttack = (parent2Mask & bit) != 0;
                var probability = Probabilities.Attacks.ProbabilityInheritedTargetAttack(
                    parent1HasAttack,
                    parent2HasAttack,
                    parent1.AttackProfile.HasNoopAttack,
                    parent2.AttackProfile.HasNoopAttack
                );
                Emit(
                    parent1Entry,
                    parent2Entry,
                    AttackCompositionMode.Normal,
                    parent1HasAttack ? bit : (byte)0,
                    parent2HasAttack ? bit : (byte)0,
                    (byte)(innateMask | bit),
                    probability,
                    usesSpecialCake: false
                );
            }

            if (settings.MaxSpecialCakes != 0)
                EnumerateCakeMasks(parent1Mask, parent2Mask, (parent1Loadout, parent2Loadout) => Emit(
                    parent1Entry,
                    parent2Entry,
                    AttackCompositionMode.InheritAll,
                    parent1Loadout,
                    parent2Loadout,
                    (byte)(innateMask | parent1Loadout | parent2Loadout),
                    attackProbability: 1,
                    usesSpecialCake: true
                ));
        }

        void Emit(
            AttackProfileEntry parent1Entry,
            AttackProfileEntry parent2Entry,
            AttackCompositionMode mode,
            byte parent1Mask,
            byte parent2Mask,
            byte childMask,
            float attackProbability,
            bool usesSpecialCake
        )
        {
            var selfBreedings = (int)Math.Ceiling(1f / (baseProbability * attackProbability));
            var totalCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes +
                (usesSpecialCake ? selfBreedings : 0);
            if (settings.MaxSpecialCakes is int maxSpecialCakes && totalCakes > maxSpecialCakes)
                return;

            var parentEffort = BredPalReferenceEffort.CombineParentEffort(
                settings.GameSettings,
                parent1,
                parent2,
                parent1Entry.BreedingEffort,
                parent2Entry.BreedingEffort
            );
            var childEntry = new AttackProfileEntry(
                childMask,
                totalCakes,
                parentEffort + BredPalReferenceEffort.CalculateSelfBreedingEffort(
                    settings.GameSettings, child, parent1.TimeFactor, parent2.TimeFactor, selfBreedings
                ),
                selfBreedings,
                usesSpecialCake
            );
            if (childEntry.BreedingEffort > settings.MaxEffort)
                return;

            if (entries is not null)
                entries.Add(childEntry);
            else
                choices!.Add(new(
                    parent1Entry, parent2Entry, mode, parent1Mask, parent2Mask, childEntry, attackProbability
                ));
        }
    }

    /// <summary>
    /// Emits the inclusion-maximal attack unions attainable with at most three
    /// attacks equipped by each parent. One legal parent-loadout witness is
    /// retained for every union so overlapping parent masks cannot reconstruct
    /// an impossible four-or-more-attack loadout later.
    /// </summary>
    private static void EnumerateCakeMasks(
        byte parent1Mask,
        byte parent2Mask,
        Action<byte, byte> emit
    )
    {
        ulong feasibleMasks = 0;
        Span<ushort> loadoutsByMask = stackalloc ushort[64];
        for (var subset1 = parent1Mask; ; subset1 = (byte)((subset1 - 1) & parent1Mask))
        {
            if (BitOperations.PopCount((uint)subset1) <= 3)
                for (var subset2 = parent2Mask; ; subset2 = (byte)((subset2 - 1) & parent2Mask))
                {
                    if (BitOperations.PopCount((uint)subset2) <= 3)
                    {
                        var mask = (byte)(subset1 | subset2);
                        var maskBit = 1UL << mask;
                        if ((feasibleMasks & maskBit) == 0)
                            loadoutsByMask[mask] = (ushort)((subset1 << 8) | subset2);
                        feasibleMasks |= maskBit;
                    }
                    if (subset2 == 0)
                        break;
                }
            if (subset1 == 0)
                break;
        }

        for (byte mask = 0; mask < 64; mask++)
        {
            if ((feasibleMasks & (1UL << mask)) == 0)
                continue;

            var hasStrictSuperset = false;
            for (byte other = 0; other < 64; other++)
            {
                if (other != mask && (feasibleMasks & (1UL << other)) != 0 && (other & mask) == mask)
                {
                    hasStrictSuperset = true;
                    break;
                }
            }

            if (!hasStrictSuperset)
            {
                var loadouts = loadoutsByMask[mask];
                emit((byte)(loadouts >> 8), (byte)loadouts);
            }
        }
    }
}
