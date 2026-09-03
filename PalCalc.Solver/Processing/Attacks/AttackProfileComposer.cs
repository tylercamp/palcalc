using System.Numerics;
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
    AttackSolverDiagnostics diagnostics = null,
    PalSpecifier terminalTarget = null
)
{
    private readonly AttackProfileReducer.Accumulator accumulator = new(diagnostics);
    private readonly AttackProfileReducer.Accumulator terminalGenderAccumulator = new();

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

        var hasNoopAttack = targets.StateOf(child).HasNooplLevel1Attack;
        accumulator.Reset(hasNoopAttack);
        // Required gender can change both expected cakes and effort at the final
        // edge. Preserve that mask champion alongside the ordinary parent-use
        // champion so applying gender after the search cannot reverse a choice
        // which was already discarded.
        var preserveTerminalGenderChoice =
            terminalTarget is not null &&
            terminalTarget.RequiredGender != PalGender.WILDCARD &&
            child == terminalTarget.Pal;
        if (preserveTerminalGenderChoice)
            terminalGenderAccumulator.Reset(hasNoopAttack);

        var metrics = Enumerate(
            child,
            parent1,
            parent2,
            passivesProbability,
            ivsProbability,
            accumulator,
            preserveTerminalGenderChoice ? terminalGenderAccumulator : null,
            null
        );
        var result = accumulator.Build();
        if (preserveTerminalGenderChoice)
            result = Merge(result, terminalGenderAccumulator.Build());
        diagnostics?.RecordComposition(
            parent1.AttackProfile.EntriesSpan.Length,
            parent2.AttackProfile.EntriesSpan.Length,
            metrics.BaselineAttempts,
            metrics.NormalAttempts,
            metrics.CakeAttempts,
            metrics.CakeFirstPrunedAttempts,
            accumulator.InputCount,
            result.EntriesSpan.Length
        );
        return result;
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
            Enumerate(child, parent1, parent2, passivesProbability, ivsProbability, null, null, choices);
        return choices;
    }

    private CompositionMetrics Enumerate(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability,
        AttackProfileReducer.Accumulator entries,
        AttackProfileReducer.Accumulator terminalGenderEntries,
        List<AttackCompositionChoice> choices
    )
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent1);
        ArgumentNullException.ThrowIfNull(parent2);

        var baseProbability = passivesProbability * ivsProbability;
        if (baseProbability <= 0)
            return default;

        var metrics = new CompositionMetrics();

        var innateMask = targets.StateOf(child).Level1TargetMask;
        var guaranteedBreedings = (int)Math.Ceiling(1f / baseProbability);
        var dilutedBreedings = (int)Math.Ceiling(1f / (baseProbability * 0.5f));
        var guaranteedSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings, child, parent1.TimeFactor, parent2.TimeFactor, guaranteedBreedings
        );
        var dilutedSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings, child, parent1.TimeFactor, parent2.TimeFactor, dilutedBreedings
        );
        foreach (var parent1Entry in parent1.AttackProfile.EntriesSpan)
        foreach (var parent2Entry in parent2.AttackProfile.EntriesSpan)
        {
            metrics.BaselineAttempts++;
            var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;
            var parentEffort = BredPalReferenceEffort.CombineParentEffort(
                settings.GameSettings,
                parent1,
                parent2,
                parent1Entry.BreedingEffort,
                parent2Entry.BreedingEffort
            );

            Emit(
                AttackCompositionMode.Baseline,
                parent1Loadout: 0,
                parent2Loadout: 0,
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

                metrics.NormalAttempts++;

                var parent1HasAttack = (parent1Mask & bit) != 0;
                var parent2HasAttack = (parent2Mask & bit) != 0;
                var probability = Probabilities.Attacks.ProbabilityInheritedTargetAttack(
                    parent1HasAttack,
                    parent2HasAttack,
                    parent1.AttackProfile.HasNoopAttack,
                    parent2.AttackProfile.HasNoopAttack
                );
                Emit(
                    AttackCompositionMode.Normal,
                    parent1HasAttack ? bit : (byte)0,
                    parent2HasAttack ? bit : (byte)0,
                    (byte)(innateMask | bit),
                    probability,
                    usesSpecialCake: false
                );
            }

            if (settings.MaxSpecialCakes != 0)
            {
                var cakeLoadouts = CakeMaskCache.Values[(parent1Mask << 6) | parent2Mask];
                for (var i = 0; i < cakeLoadouts.Length; i++)
                {
                    metrics.CakeAttempts++;
                    var loadouts = cakeLoadouts[i];
                    var parent1Loadout = (byte)(loadouts >> 8);
                    var parent2Loadout = (byte)loadouts;
                    Emit(
                        AttackCompositionMode.InheritAll,
                        parent1Loadout,
                        parent2Loadout,
                        (byte)(innateMask | parent1Loadout | parent2Loadout),
                        attackProbability: 1,
                        usesSpecialCake: true
                    );
                }
            }

            void Emit(
                AttackCompositionMode mode,
                byte parent1Loadout,
                byte parent2Loadout,
                byte childMask,
                float attackProbability,
                bool usesSpecialCake
            )
            {
                var guaranteed = attackProbability == 1;
                var selfBreedings = guaranteed ? guaranteedBreedings : dilutedBreedings;
                var totalCakes = parentCakes + (usesSpecialCake ? selfBreedings : 0);
                if (settings.MaxSpecialCakes is int maxSpecialCakes && totalCakes > maxSpecialCakes)
                    return;

                var ordinaryCouldImprove = entries?.CouldImprove(childMask, totalCakes) != false;
                var adjustedTotalCakes = totalCakes;
                var terminalCouldImprove = false;
                if (terminalGenderEntries is not null)
                {
                    var adjustedBreedings = BredPalReferenceEffort.WithGuaranteedGender(
                        selfBreedings,
                        child,
                        settings.DB,
                        terminalTarget.RequiredGender,
                        settings.UseGenderReversers
                    );
                    adjustedTotalCakes += usesSpecialCake
                        ? adjustedBreedings - selfBreedings
                        : 0;
                    terminalCouldImprove = terminalGenderEntries.CouldImprove(
                        childMask,
                        adjustedTotalCakes
                    );
                }

                if (entries is not null && !ordinaryCouldImprove && !terminalCouldImprove)
                {
                    metrics.CakeFirstPrunedAttempts++;
                    return;
                }

                var childEntry = new AttackProfileEntry(
                    childMask,
                    totalCakes,
                    parentEffort + (guaranteed ? guaranteedSelfEffort : dilutedSelfEffort),
                    selfBreedings,
                    usesSpecialCake
                );
                if (childEntry.BreedingEffort > settings.MaxEffort)
                    return;

                if (entries is not null)
                {
                    if (ordinaryCouldImprove)
                        entries.Add(childEntry);
                    if (terminalCouldImprove)
                    {
                        var adjusted = childEntry.WithGuaranteedGender(
                            settings.GameSettings,
                            child,
                            parent1.TimeFactor,
                            parent2.TimeFactor,
                            settings.DB,
                            terminalTarget.RequiredGender,
                            settings.UseGenderReversers
                        );
                        terminalGenderEntries.Add(childEntry, adjusted);
                    }
                }
                else
                    choices!.Add(new(
                        parent1Entry,
                        parent2Entry,
                        mode,
                        parent1Loadout,
                        parent2Loadout,
                        childEntry,
                        attackProbability
                    ));
            }
        }

        return metrics;
    }

    private static AttackProfile Merge(AttackProfile primary, AttackProfile terminal)
    {
        var result = new AttackProfileEntry[
            primary.EntriesSpan.Length + terminal.EntriesSpan.Length
        ];
        var count = 0;
        foreach (ref readonly var entry in primary.EntriesSpan)
            result[count++] = entry;
        foreach (ref readonly var entry in terminal.EntriesSpan)
        {
            if (primary.EntriesSpan.Contains(entry))
                continue;
            result[count++] = entry;
        }

        if (count != result.Length)
            Array.Resize(ref result, count);
        return new AttackProfile(primary.HasNoopAttack, result);
    }

    private struct CompositionMetrics
    {
        public long BaselineAttempts;
        public long NormalAttempts;
        public long CakeAttempts;
        public long CakeFirstPrunedAttempts;
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
        for (var candidate = 0; candidate < 64; candidate++)
        {
            var childMask = (byte)candidate;
            if ((childMask & ~availableMask) != 0 ||
                !IsCakeMaskFeasible(parent1Mask, parent2Mask, childMask))
                continue;

            var isMaximal = true;
            var missingMask = (byte)(availableMask & ~childMask);
            for (var bit = 1; bit < 64; bit <<= 1)
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

    private static bool IsCakeMaskFeasible(byte parent1Mask, byte parent2Mask, byte childMask) =>
        BitOperations.PopCount((uint)childMask) <= 6 &&
        BitOperations.PopCount((uint)(childMask & ~parent2Mask)) <= 3 &&
        BitOperations.PopCount((uint)(childMask & ~parent1Mask)) <= 3;

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
        while (BitOperations.PopCount((uint)loadout) > 3)
        {
            var duplicateMask = (byte)(loadout & otherLoadout);
            var duplicateBit = 1 << BitOperations.TrailingZeroCount((uint)duplicateMask);
            loadout = (byte)(loadout & ~duplicateBit);
        }
    }

    private static class CakeMaskCache
    {
        public static readonly ushort[][] Values = Build();

        private static ushort[][] Build()
        {
            var result = new ushort[64 * 64][];
            Span<ushort> buffer = stackalloc ushort[64];
            for (var parent1Mask = 0; parent1Mask < 64; parent1Mask++)
            for (var parent2Mask = 0; parent2Mask < 64; parent2Mask++)
            {
                var count = EnumerateCakeMasks(
                    (byte)parent1Mask,
                    (byte)parent2Mask,
                    buffer
                );
                result[(parent1Mask << 6) | parent2Mask] = buffer[..count].ToArray();
            }

            return result;
        }
    }
}
