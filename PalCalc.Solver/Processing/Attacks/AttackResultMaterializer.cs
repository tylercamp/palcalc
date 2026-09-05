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

/// <summary>A concrete inheritance outcome used to reconstruct one selected profile entry.</summary>
internal readonly record struct AttackCompositionChoice(
    AttackProfileEntry Parent1Entry,
    AttackProfileEntry Parent2Entry,
    AttackCompositionMode Mode,
    byte Parent1TargetMask,
    byte Parent2TargetMask,
    AttackProfileEntry ChildEntry
);

/// <summary>
/// Used by the `ResultPostProcessor` in a finalization step. Takes an unmaterialized
/// search entry containing only an exact mask and estimated cake total, then
/// recursively resolves a new `IPalReference` with exact inheritance instructions,
/// probability, effort, and cake usage.
/// </summary>
internal sealed class AttackResultMaterializer
{
    private static readonly ActiveSkill AnyAttack = new RandomActiveSkill();

    private readonly AttackTargetContext targets;
    private readonly BreedingSolverSettings settings;
    private readonly Dictionary<IPalReference, Dictionary<AttackProfileEntry, MaterializedResult>> materialized =
        new(ReferenceEqualityComparer.Instance);

    public AttackResultMaterializer(AttackTargetContext targets, BreedingSolverSettings settings)
    {
        this.targets = targets;
        this.settings = settings;
    }

    /// <summary>
    /// <para>Reconstructs the given `IPalReference` to satisfy the given `AttackProfileEntry`.</para>
    /// <para>
    ///     During the solver process, an `IPalReference` only tracks its <em>potential</em> attack outcomes. This
    ///     method does the work of traversing the tree, choosing the specific attack-breeding paths as necessary,
    ///     all while respecting Palworld's general limits for attack inheritance. Exact parent effort and cake
    ///     totals come from the recursively materialized results, not from search-entry metadata.
    /// </para>
    /// </summary>
    /// <remarks>
    ///     `reference.AttackProfile` is a cumulative record of possible attack inheritance outcomes. The `selectedEntry`
    ///     MUST be covered by one of the profiles entries.
    /// </remarks>
    public IPalReference Materialize(IPalReference reference, AttackProfileEntry selectedEntry) =>
        MaterializeResult(reference, selectedEntry).Reference;

    private MaterializedResult MaterializeResult(
        IPalReference reference,
        AttackProfileEntry selectedEntry
    )
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!materialized.TryGetValue(reference, out var entries))
        {
            entries = [];
            materialized.Add(reference, entries);
        }

        if (entries.TryGetValue(selectedEntry, out var result))
            return result;

        result = reference switch
        {
            SurgeryTablePalReference surgery => MaterializeSurgery(surgery, selectedEntry),
            BredPalReference bred => MaterializeBred(bred, selectedEntry),
            _ => new(MaterializeLeaf(reference, selectedEntry), selectedEntry.TotalSpecialCakes),
        };
        entries.Add(selectedEntry, result);
        return result;
    }

    private MaterializedResult MaterializeSurgery(
        SurgeryTablePalReference surgery,
        AttackProfileEntry selectedEntry
    )
    {
        var input = MaterializeResult(surgery.Input, selectedEntry);
        return new(
            new SurgeryTablePalReference(input.Reference, surgery.Operations),
            input.TotalSpecialCakes
        );
    }

    private MaterializedResult MaterializeBred(
        BredPalReference bred,
        AttackProfileEntry selectedEntry
    )
    {
        var found = false;
        var best = default(MaterializedChoice);
        foreach (var choice in EnumerateChoices(
            bred.Pal,
            bred.Parent1,
            bred.Parent2,
            bred.PassivesProbability,
            bred.IVsProbability
        ))
        {
            if (!MatchesSearchEntry(choice, selectedEntry))
                continue;

            var candidate = MaterializeChoice(bred, choice);
            if (!found || CompareChoices(candidate, best) < 0)
            {
                best = candidate;
                found = true;
            }
        }

        return found
            ? best.Result
            : throw new InvalidOperationException(
                "The selected attack profile entry cannot be reconstructed."
            );
    }

    private MaterializedChoice MaterializeChoice(
        BredPalReference bred,
        AttackCompositionChoice choice
    )
    {
        // Parent effort and cake totals are only authoritative after their
        // selected entries have been recursively materialized.
        var parent1 = MaterializeResult(bred.Parent1, choice.Parent1Entry);
        var parent2 = MaterializeResult(bred.Parent2, choice.Parent2Entry);
        var parentCakes = parent1.TotalSpecialCakes + parent2.TotalSpecialCakes;
        var attackProbability = AttackProbabilityFor(choice, bred.Parent1, bred.Parent2);
        var requiredBreedings = RequiredBreedings(bred, attackProbability);
        var usesSpecialCake = choice.Mode == AttackCompositionMode.InheritAll;
        var totalCakes = parentCakes + (usesSpecialCake ? requiredBreedings : 0);
        var inheritedAttacks = AttacksForMask((byte)(
            choice.Parent1TargetMask | choice.Parent2TargetMask
        ));
        var childLearnedAttacks = inheritedAttacks
            .Concat(bred.Pal.Level1ActiveSkills(settings.DB))
            .Distinct()
            .ToArray();
        var actualEntry = new AttackProfileEntry(
            choice.ChildEntry.LearnedTargetMask,
            totalCakes
        );
        var normalInheritance = choice.Mode == AttackCompositionMode.Normal;
        var parent1RequiresNoop = normalInheritance &&
            choice.Parent1TargetMask == 0 &&
            choice.Parent2TargetMask != 0 &&
            bred.Parent1.AttackProfile.HasNoopAttack;
        var parent2RequiresNoop = normalInheritance &&
            choice.Parent2TargetMask == 0 &&
            choice.Parent1TargetMask != 0 &&
            bred.Parent2.AttackProfile.HasNoopAttack;
        var inheritance = new MaterializedAttackInheritance(
            (AttackInheritanceMode)choice.Mode,
            LoadoutFor(parent1.Reference, choice.Parent1TargetMask, parent1RequiresNoop),
            LoadoutFor(parent2.Reference, choice.Parent2TargetMask, parent2RequiresNoop),
            inheritedAttacks,
            childLearnedAttacks,
            usesSpecialCake ? requiredBreedings : 0,
            attackProbability
        );
        var reference = new BredPalReference(
            settings.GameSettings,
            bred.Pal,
            parent1.Reference,
            parent2.Reference,
            [.. bred.EffectivePassives],
            bred.PassivesProbability,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, actualEntry),
            materializedAttackInheritance: inheritance,
            avgRequiredBreedings: requiredBreedings,
            gender: bred.Gender
        );

        return new(
            new MaterializedResult(reference, totalCakes),
            choice
        );
    }

    private static bool MatchesSearchEntry(
        in AttackCompositionChoice choice,
        in AttackProfileEntry selectedEntry
    ) =>
        choice.ChildEntry.LearnedTargetMask == selectedEntry.LearnedTargetMask &&
        choice.ChildEntry.TotalSpecialCakes == selectedEntry.TotalSpecialCakes;

    private int RequiredBreedings(BredPalReference bred, float attackProbability)
    {
        var requiredBreedings = (int)Math.Ceiling(
            1f / (bred.PassivesProbability * bred.IVsProbability * attackProbability)
        );
        return bred.Gender == PalGender.WILDCARD
            ? requiredBreedings
            : BredPalReferenceEffort.WithGuaranteedGender(
                requiredBreedings,
                bred.Pal,
                settings.DB,
                bred.Gender,
                settings.UseGenderReversers
            );
    }

    private static float AttackProbabilityFor(
        in AttackCompositionChoice choice,
        IPalReference parent1,
        IPalReference parent2
    ) => choice.Mode switch
    {
        AttackCompositionMode.Baseline or AttackCompositionMode.InheritAll => 1,
        AttackCompositionMode.Normal => Probabilities.Attacks.ProbabilityInheritedTargetAttack(
            choice.Parent1TargetMask != 0,
            choice.Parent2TargetMask != 0,
            parent1.AttackProfile.HasNoopAttack,
            parent2.AttackProfile.HasNoopAttack
        ),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    private static IPalReference MaterializeLeaf(
        IPalReference reference,
        in AttackProfileEntry selectedEntry
    )
    {
        foreach (ref readonly var entry in reference.AttackProfile.EntriesSpan)
        {
            if (entry.LearnedTargetMask == selectedEntry.LearnedTargetMask &&
                entry.TotalSpecialCakes == selectedEntry.TotalSpecialCakes)
                return reference;
        }

        throw new InvalidOperationException(
            "The selected attack profile entry cannot be reconstructed."
        );
    }

    private readonly record struct MaterializedChoice(
        MaterializedResult Result,
        AttackCompositionChoice Choice
    );

    private readonly record struct MaterializedResult(
        IPalReference Reference,
        int TotalSpecialCakes
    );

    /// <summary>
    /// Exhaustively enumerates concrete witnesses for reconstruction. Search uses
    /// <see cref="AttackProfileComposer"/>'s optimized profile-only algorithm instead.
    /// </summary>
    internal IEnumerable<AttackCompositionChoice> EnumerateChoices(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        float passivesProbability,
        float ivsProbability
    )
    {
        if (!targets.IsActive)
            yield break;

        var baseProbability = passivesProbability * ivsProbability;
        if (baseProbability <= 0)
            yield break;

        var innateMask = targets.StateOf(child).Level1TargetMask;
        var inheritableTargetMask = targets.InheritableTargetMask;
        var parent1Profile = parent1.AttackProfile;
        var parent2Profile = parent2.AttackProfile;
        var cakeBreedings = (int)Math.Ceiling(1f / baseProbability);
        var cakeLoadouts = new ushort[AttackProfile.TargetMaskCount];

        foreach (var parent1Entry in parent1Profile.Entries)
            foreach (var parent2Entry in parent2Profile.Entries)
            {
                var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;

                var baseline = CreateChoice(
                    parent1Entry,
                    parent2Entry,
                    parentCakes,
                    AttackCompositionMode.Baseline,
                    0,
                    0,
                    innateMask
                );
                if (baseline is AttackCompositionChoice baselineChoice)
                    yield return baselineChoice;

                var parent1Mask = (byte)(parent1Entry.LearnedTargetMask & inheritableTargetMask);
                var parent2Mask = (byte)(parent2Entry.LearnedTargetMask & inheritableTargetMask);
                var normalTargets = (byte)((parent1Mask | parent2Mask) & ~innateMask);
                while (normalTargets != 0)
                {
                    var bit = (byte)(normalTargets & -normalTargets);
                    normalTargets &= (byte)~bit;
                    var parent1HasAttack = (parent1Mask & bit) != 0;
                    var parent2HasAttack = (parent2Mask & bit) != 0;
                    var normal = CreateChoice(
                        parent1Entry,
                        parent2Entry,
                        parentCakes,
                        AttackCompositionMode.Normal,
                        parent1HasAttack ? bit : (byte)0,
                        parent2HasAttack ? bit : (byte)0,
                        (byte)(innateMask | bit)
                    );
                    if (normal is AttackCompositionChoice normalChoice)
                        yield return normalChoice;
                }

                if (settings.MaxSpecialCakes == 0)
                    continue;

                var count = AttackProfileComposer.EnumerateCakeMasks(
                    parent1Mask,
                    parent2Mask,
                    cakeLoadouts
                );
                for (var i = 0; i < count; i++)
                {
                    var loadouts = cakeLoadouts[i];
                    var parent1Loadout = (byte)(loadouts >> 8);
                    var parent2Loadout = (byte)loadouts;
                    var cake = CreateChoice(
                        parent1Entry,
                        parent2Entry,
                        parentCakes,
                        AttackCompositionMode.InheritAll,
                        parent1Loadout,
                        parent2Loadout,
                        (byte)(innateMask | parent1Loadout | parent2Loadout)
                    );
                    if (cake is AttackCompositionChoice cakeChoice)
                        yield return cakeChoice;
                }
            }

        AttackCompositionChoice? CreateChoice(
            in AttackProfileEntry parent1Entry,
            in AttackProfileEntry parent2Entry,
            int parentCakes,
            AttackCompositionMode mode,
            byte parent1TargetMask,
            byte parent2TargetMask,
            byte childMask
        )
        {
            var totalCakes = parentCakes +
                (mode == AttackCompositionMode.InheritAll ? cakeBreedings : 0);
            if (settings.MaxSpecialCakes is int maxCakes && totalCakes > maxCakes)
                return null;

            var childEntry = new AttackProfileEntry(
                childMask,
                totalCakes
            );
            return new(
                parent1Entry,
                parent2Entry,
                mode,
                parent1TargetMask,
                parent2TargetMask,
                childEntry
            );
        }
    }

    private static int CompareChoices(
        in MaterializedChoice left,
        in MaterializedChoice right
    )
    {
        var comparison = left.Result.TotalSpecialCakes.CompareTo(
            right.Result.TotalSpecialCakes
        );
        if (comparison != 0) return comparison;
        comparison = left.Result.Reference.BreedingEffort.CompareTo(
            right.Result.Reference.BreedingEffort
        );
        if (comparison != 0) return comparison;
        comparison = left.Choice.Mode.CompareTo(right.Choice.Mode);
        if (comparison != 0) return comparison;
        comparison = left.Choice.Parent1TargetMask.CompareTo(right.Choice.Parent1TargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Choice.Parent2TargetMask.CompareTo(right.Choice.Parent2TargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Choice.Parent1Entry.LearnedTargetMask.CompareTo(
            right.Choice.Parent1Entry.LearnedTargetMask
        );
        if (comparison != 0) return comparison;
        comparison = left.Choice.Parent2Entry.LearnedTargetMask.CompareTo(
            right.Choice.Parent2Entry.LearnedTargetMask
        );
        if (comparison != 0) return comparison;
        comparison = left.Choice.Parent1Entry.TotalSpecialCakes.CompareTo(
            right.Choice.Parent1Entry.TotalSpecialCakes
        );
        return comparison != 0
            ? comparison
            : left.Choice.Parent2Entry.TotalSpecialCakes.CompareTo(
                right.Choice.Parent2Entry.TotalSpecialCakes
            );
    }

    private ActiveSkill[] AttacksForMask(byte mask)
    {
        var attacks = new List<ActiveSkill>();
        for (var bit = (byte)1; bit != 0 && bit <= targets.FullTargetMask; bit <<= 1)
            if ((mask & bit) != 0)
                attacks.Add(targets.AttackForBit(bit));
        return attacks.ToArray();
    }

    private IReadOnlyList<ActiveSkill> LoadoutFor(
        IPalReference parent,
        byte targetMask,
        bool requiresNoop
    )
    {
        var loadout = AttacksForMask(targetMask).ToList();
        if (loadout.Count == 0)
        {
            var filler = requiresNoop
                ? LearnedAttacks(parent)
                    .Where(attack => !attack.CanInherit)
                    .OrderBy(attack => attack.InternalName, StringComparer.Ordinal)
                    .FirstOrDefault() ?? throw new InvalidOperationException(
                        "The selected attack profile entry requires a non-inheritable parent attack that cannot be reconstructed."
                    )
                : AnyAttack;
            loadout.Add(filler);
        }

        if (loadout.Count is < 1 or > 3)
            throw new InvalidOperationException("A parent loadout must contain one to three attacks.");
        return loadout;
    }

    private IEnumerable<ActiveSkill> LearnedAttacks(IPalReference reference) =>
        reference switch
        {
            SurgeryTablePalReference surgery => LearnedAttacks(surgery.Input),
            OwnedPalReference owned => owned.UnderlyingInstance.ActiveSkills ?? [],
            CompositeOwnedPalReference composite => composite.Male.UnderlyingInstance.ActiveSkills ?? [],
            BredPalReference { MaterializedAttackInheritance: not null } bred =>
                bred.MaterializedAttackInheritance.ChildLearnedAttacks,
            _ => reference.Pal.Level1ActiveSkills(settings.DB),
        };

}
