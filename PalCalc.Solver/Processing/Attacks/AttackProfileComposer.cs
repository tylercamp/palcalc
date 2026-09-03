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
    private const int TargetMaskBitCount = PalSpecifier.MaxRequiredAttacks;
    private const int TargetMaskCount = AttackProfile.TargetMaskCount;
    private const int MaxEquippedAttacksPerParent = 3;
    private const int PackedParentLoadoutShift = 8;

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
            metrics.ParentEntryPairs,
            metrics.BaselineAttempts,
            metrics.NormalAttempts,
            metrics.CakeAttempts,
            metrics.BaselinePrunedAttempts,
            metrics.NormalPrunedAttempts,
            metrics.CakePrunedAttempts,
            metrics.MaxEffortFallbacks,
            metrics.MaxEffortFallbackSuccesses,
            metrics.MaxEffortFallbackPairs,
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

        var parent1Profile = parent1.AttackProfile;
        var parent2Profile = parent2.AttackProfile;
        var parent1HasNoopAttack = parent1Profile.HasNoopAttack;
        var parent2HasNoopAttack = parent2Profile.HasNoopAttack;
        var inheritableTargetMask = targets.InheritableTargetMask;
        var maxSpecialCakes = settings.MaxSpecialCakes;

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

        var parent1Entries = parent1Profile.EntriesSpan;
        var parent2Entries = parent2Profile.EntriesSpan;
        // Normal inheritance only cares whether each parent can supply the one
        // target attack, plus the profile-wide noop flag. Track the cheapest
        // with/without entry for each target, and one unrestricted baseline entry.
        const int normalCategoryCount = TargetMaskBitCount * 2;
        const int anyCategory = normalCategoryCount;
        Span<int> parent1CategoryChampions = stackalloc int[normalCategoryCount + 1];
        Span<int> parent2CategoryChampions = stackalloc int[normalCategoryCount + 1];
        parent1CategoryChampions.Fill(-1);
        parent2CategoryChampions.Fill(-1);

        if (entries is not null)
        {
            BuildCategoryChampions(parent1Entries, parent1CategoryChampions);
            BuildCategoryChampions(parent2Entries, parent2CategoryChampions);

            metrics.BaselineAttempts++;
            if (TryGetBestPair(
                    parent1Entries,
                    parent2Entries,
                    parent1CategoryChampions,
                    parent2CategoryChampions,
                    anyCategory,
                    anyCategory,
                    guaranteedSelfEffort,
                    out var baselineParent1Entry,
                    out var baselineParent2Entry,
                    out var baselineParentCakes,
                    out var baselineParentEffort
                ) && !Emit(
                    baselineParent1Entry,
                    baselineParent2Entry,
                    baselineParentCakes,
                    baselineParentEffort,
                    AttackCompositionMode.Baseline,
                    parent1Loadout: 0,
                    parent2Loadout: 0,
                    childMask: innateMask,
                    attackProbability: 1,
                    usesSpecialCake: false
                ))
                metrics.BaselinePrunedAttempts++;

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
                    parent1Entries, parent2Entries,
                    parent1CategoryChampions, parent2CategoryChampions,
                    withAttack, withoutAttack, bit
                );
                EmitBestNormal(
                    parent1Entries, parent2Entries,
                    parent1CategoryChampions, parent2CategoryChampions,
                    withoutAttack, withAttack, bit
                );
                EmitBestNormal(
                    parent1Entries, parent2Entries,
                    parent1CategoryChampions, parent2CategoryChampions,
                    withAttack, withAttack, bit
                );
            }
        }

        // Materialization needs concrete witnesses for every possible outcome.
        // Search composition only needs the category champions above when cake
        // inheritance is disabled, so avoid entering the parent cross-product.
        if (choices is null && maxSpecialCakes == 0)
            return metrics;

        foreach (var parent1Entry in parent1Entries)
        foreach (var parent2Entry in parent2Entries)
        {
            if (entries is not null)
                metrics.ParentEntryPairs++;
            var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;
            var parentEffort = BredPalReferenceEffort.CombineParentEffort(
                settings.GameSettings,
                parent1,
                parent2,
                parent1Entry.BreedingEffort,
                parent2Entry.BreedingEffort
            );

            if (choices is not null)
            {
                metrics.BaselineAttempts++;
                if (!Emit(
                        parent1Entry,
                        parent2Entry,
                        parentCakes,
                        parentEffort,
                        AttackCompositionMode.Baseline,
                        parent1Loadout: 0,
                        parent2Loadout: 0,
                        childMask: innateMask,
                        attackProbability: 1,
                        usesSpecialCake: false
                    ))
                    metrics.BaselinePrunedAttempts++;
            }

            var parent1Mask = (byte)(parent1Entry.LearnedTargetMask & inheritableTargetMask);
            var parent2Mask = (byte)(parent2Entry.LearnedTargetMask & inheritableTargetMask);
            var normalTargets = choices is null
                ? (byte)0
                : (byte)((parent1Mask | parent2Mask) & ~innateMask);
            while (normalTargets != 0)
            {
                var bit = (byte)(normalTargets & -normalTargets);
                normalTargets &= (byte)~bit;

                metrics.NormalAttempts++;

                var parent1HasAttack = (parent1Mask & bit) != 0;
                var parent2HasAttack = (parent2Mask & bit) != 0;
                var probability = Probabilities.Attacks.ProbabilityInheritedTargetAttack(
                    parent1HasAttack,
                    parent2HasAttack,
                    parent1HasNoopAttack,
                    parent2HasNoopAttack
                );
                if (!Emit(
                        parent1Entry,
                        parent2Entry,
                        parentCakes,
                        parentEffort,
                        AttackCompositionMode.Normal,
                        parent1HasAttack ? bit : (byte)0,
                        parent2HasAttack ? bit : (byte)0,
                        (byte)(innateMask | bit),
                        probability,
                        usesSpecialCake: false
                    ))
                    metrics.NormalPrunedAttempts++;
            }

            if (maxSpecialCakes != 0)
            {
                var cakeLoadouts = CakeMaskCache.Values[
                    (parent1Mask << TargetMaskBitCount) | parent2Mask
                ];
                for (var i = 0; i < cakeLoadouts.Length; i++)
                {
                    metrics.CakeAttempts++;
                    var loadouts = cakeLoadouts[i];
                    var parent1Loadout = (byte)(loadouts >> PackedParentLoadoutShift);
                    var parent2Loadout = (byte)loadouts;
                    if (!Emit(
                            parent1Entry,
                            parent2Entry,
                            parentCakes,
                            parentEffort,
                            AttackCompositionMode.InheritAll,
                            parent1Loadout,
                            parent2Loadout,
                            (byte)(innateMask | parent1Loadout | parent2Loadout),
                            attackProbability: 1,
                            usesSpecialCake: true
                        ))
                        metrics.CakePrunedAttempts++;
                }
            }
        }

        return metrics;

        void BuildCategoryChampions(
            ReadOnlySpan<AttackProfileEntry> profileEntries,
            Span<int> champions
        )
        {
            for (var entryIndex = 0; entryIndex < profileEntries.Length; entryIndex++)
            {
                ref readonly var entry = ref profileEntries[entryIndex];
                UpdateChampion(anyCategory, entryIndex, entry, profileEntries, champions);

                var entryMask = (byte)(entry.LearnedTargetMask & inheritableTargetMask);
                for (var bitIndex = 0; bitIndex < TargetMaskBitCount; bitIndex++)
                {
                    var category = (bitIndex << 1) + ((entryMask >> bitIndex) & 1);
                    UpdateChampion(category, entryIndex, entry, profileEntries, champions);
                }
            }
        }

        static void UpdateChampion(
            int category,
            int entryIndex,
            in AttackProfileEntry entry,
            ReadOnlySpan<AttackProfileEntry> profileEntries,
            Span<int> champions
        )
        {
            var incumbentIndex = champions[category];
            if (incumbentIndex < 0 || AttackProfileEntryComparer.CompareCosts(
                    entry,
                    profileEntries[incumbentIndex]
                ) < 0)
                champions[category] = entryIndex;
        }

        void EmitBestNormal(
            ReadOnlySpan<AttackProfileEntry> firstEntries,
            ReadOnlySpan<AttackProfileEntry> secondEntries,
            ReadOnlySpan<int> firstChampions,
            ReadOnlySpan<int> secondChampions,
            int parent1Category,
            int parent2Category,
            byte bit
        )
        {
            if (firstChampions[parent1Category] < 0 || secondChampions[parent2Category] < 0)
                return;

            metrics.NormalAttempts++;
            var parent1HasAttack = (parent1Category & 1) != 0;
            var parent2HasAttack = (parent2Category & 1) != 0;
            var probability = Probabilities.Attacks.ProbabilityInheritedTargetAttack(
                parent1HasAttack,
                parent2HasAttack,
                parent1HasNoopAttack,
                parent2HasNoopAttack
            );
            var selfEffort = probability == 1 ? guaranteedSelfEffort : dilutedSelfEffort;
            if (TryGetBestPair(
                    firstEntries,
                    secondEntries,
                    firstChampions,
                    secondChampions,
                    parent1Category,
                    parent2Category,
                    selfEffort,
                    out var parent1Entry,
                    out var parent2Entry,
                    out var parentCakes,
                    out var parentEffort
                ) && !Emit(
                    parent1Entry,
                    parent2Entry,
                    parentCakes,
                    parentEffort,
                    AttackCompositionMode.Normal,
                    parent1HasAttack ? bit : (byte)0,
                    parent2HasAttack ? bit : (byte)0,
                    (byte)(innateMask | bit),
                    probability,
                    usesSpecialCake: false
                ))
                metrics.NormalPrunedAttempts++;
        }

        // Parent effort combines monotonically (sum, or max with parallel farms),
        // so the cheapest entry from each presence category forms the best pair.
        // MaxEffort is the exception: a cake-heavier but faster pair may be the
        // only feasible one, which is why that case falls back to a full scan.
        bool TryGetBestPair(
            ReadOnlySpan<AttackProfileEntry> firstEntries,
            ReadOnlySpan<AttackProfileEntry> secondEntries,
            ReadOnlySpan<int> firstChampions,
            ReadOnlySpan<int> secondChampions,
            int parent1Category,
            int parent2Category,
            TimeSpan selfEffort,
            out AttackProfileEntry parent1Entry,
            out AttackProfileEntry parent2Entry,
            out int parentCakes,
            out TimeSpan parentEffort
        )
        {
            var parent1Index = firstChampions[parent1Category];
            var parent2Index = secondChampions[parent2Category];
            if (parent1Index < 0 || parent2Index < 0)
            {
                parent1Entry = default;
                parent2Entry = default;
                parentCakes = default;
                parentEffort = default;
                return false;
            }

            parent1Entry = firstEntries[parent1Index];
            parent2Entry = secondEntries[parent2Index];
            parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;
            parentEffort = BredPalReferenceEffort.CombineParentEffort(
                settings.GameSettings,
                parent1,
                parent2,
                parent1Entry.BreedingEffort,
                parent2Entry.BreedingEffort
            );

            if (maxSpecialCakes is int maximumSpecialCakes && parentCakes > maximumSpecialCakes)
                return false;
            if (parentEffort + selfEffort <= settings.MaxEffort)
                return true;

            // A cake-heavier parent pair can still be the only pair under MaxEffort.
            // Fall back only for that constrained case; the normal path remains O(1).
            metrics.MaxEffortFallbacks++;
            var found = false;
            for (var parent1CandidateIndex = 0; parent1CandidateIndex < firstEntries.Length; parent1CandidateIndex++)
            {
                ref readonly var parent1Candidate = ref firstEntries[parent1CandidateIndex];
                if (!MatchesCategory(parent1Candidate, parent1Category))
                    continue;

                for (var parent2CandidateIndex = 0; parent2CandidateIndex < secondEntries.Length; parent2CandidateIndex++)
                {
                    metrics.MaxEffortFallbackPairs++;
                    metrics.ParentEntryPairs++;
                    ref readonly var parent2Candidate = ref secondEntries[parent2CandidateIndex];
                    if (!MatchesCategory(parent2Candidate, parent2Category))
                        continue;

                    var candidateCakes = parent1Candidate.TotalSpecialCakes + parent2Candidate.TotalSpecialCakes;
                    if (maxSpecialCakes is int maxCakes && candidateCakes > maxCakes)
                        continue;
                    var candidateEffort = BredPalReferenceEffort.CombineParentEffort(
                        settings.GameSettings,
                        parent1,
                        parent2,
                        parent1Candidate.BreedingEffort,
                        parent2Candidate.BreedingEffort
                    );
                    if (candidateEffort + selfEffort > settings.MaxEffort)
                        continue;
                    if (found && (candidateCakes > parentCakes ||
                        candidateCakes == parentCakes && candidateEffort >= parentEffort))
                        continue;

                    found = true;
                    parent1Entry = parent1Candidate;
                    parent2Entry = parent2Candidate;
                    parentCakes = candidateCakes;
                    parentEffort = candidateEffort;
                }
            }

            if (found)
                metrics.MaxEffortFallbackSuccesses++;
            return found;
        }

        bool MatchesCategory(in AttackProfileEntry entry, int category)
        {
            if (category == anyCategory)
                return true;
            var bit = 1 << (category >> 1);
            var hasAttack = (category & 1) != 0;
            return ((entry.LearnedTargetMask & inheritableTargetMask & bit) != 0) == hasAttack;
        }

        bool Emit(
            in AttackProfileEntry parent1Entry,
            in AttackProfileEntry parent2Entry,
            int parentCakes,
            TimeSpan parentEffort,
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
            if (maxSpecialCakes is int maximumSpecialCakes && totalCakes > maximumSpecialCakes)
                return true;

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
                return false;

            var childEntry = new AttackProfileEntry(
                childMask,
                totalCakes,
                parentEffort + (guaranteed ? guaranteedSelfEffort : dilutedSelfEffort),
                selfBreedings,
                usesSpecialCake
            );
            if (childEntry.BreedingEffort > settings.MaxEffort)
                return true;

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
            return true;
        }
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
        public long ParentEntryPairs;
        public long BaselineAttempts;
        public long NormalAttempts;
        public long CakeAttempts;
        public long BaselinePrunedAttempts;
        public long NormalPrunedAttempts;
        public long CakePrunedAttempts;
        public long MaxEffortFallbacks;
        public long MaxEffortFallbackSuccesses;
        public long MaxEffortFallbackPairs;
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
