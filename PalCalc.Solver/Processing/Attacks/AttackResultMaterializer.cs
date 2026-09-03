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
    AttackProfileEntry ChildEntry,
    float AttackProbability
);

/// <summary>
/// Used by the `ResultPostProcessor` in a finalization step. Takes a `IPalReference`
/// with a generic attack profile, and resolves to a new `IPalReference`
/// with specific inheritance instructions.
/// </summary>
internal sealed class AttackResultMaterializer
{
    private readonly AttackTargetContext targets;
    private readonly BreedingSolverSettings settings;

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
    ///     all while respecting Palworld's general limits for attack inheritance.
    /// </para>
    /// </summary>
    /// <remarks>
    ///     `reference.AttackProfile` is a cumulative record of possible attack inheritance outcomes. The `selectedEntry`
    ///     MUST be covered by one of the profiles entries.
    /// </remarks>
    public IPalReference Materialize(IPalReference reference, AttackProfileEntry selectedEntry) =>
        reference switch
        {
            SurgeryTablePalReference surgery => new SurgeryTablePalReference(
                Materialize(surgery.Input, selectedEntry), surgery.Operations
            ),
            BredPalReference bred => MaterializeBred(bred, selectedEntry),
            _ => reference,
        };

    private BredPalReference MaterializeBred(
        BredPalReference bred,
        AttackProfileEntry selectedEntry
    )
    {
        // Several concrete parent loadouts may reconstruct the same packed
        // profile entry. Use the search's cake-first ordering before the stable
        // loadout/mask tie-breakers so materialization preserves its objective.
        var choice = FindChoice(bred, selectedEntry);

        var parent1 = Materialize(bred.Parent1, choice.Parent1Entry);
        var parent2 = Materialize(bred.Parent2, choice.Parent2Entry);
        var inheritedAttacks = AttacksForMask((byte)(
            choice.Parent1TargetMask | choice.Parent2TargetMask
        ));
        var childLearnedAttacks = inheritedAttacks
            .Concat(bred.Pal.Level1ActiveSkills(settings.DB))
            .Distinct()
            .ToArray();
        var inheritance = new MaterializedAttackInheritance(
            (AttackInheritanceMode)choice.Mode,
            LoadoutFor(parent1, choice.Parent1TargetMask),
            LoadoutFor(parent2, choice.Parent2TargetMask),
            inheritedAttacks,
            childLearnedAttacks,
            choice.ChildEntry.SelfUsesSpecialCake ? choice.ChildEntry.SelfBreedings : 0,
            choice.AttackProbability
        );

        return new BredPalReference(
            settings.GameSettings,
            bred.Pal,
            parent1,
            parent2,
            [.. bred.EffectivePassives],
            bred.PassivesProbability,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, selectedEntry),
            materializedAttackInheritance: inheritance,
            avgRequiredBreedings: selectedEntry.SelfBreedings,
            gender: bred.Gender
        );
    }

    private AttackCompositionChoice FindChoice(
        BredPalReference bred,
        AttackProfileEntry selectedEntry
    )
    {
        var found = false;
        var best = default(AttackCompositionChoice);
        foreach (var choice in EnumerateChoices(
            bred.Pal,
            bred.Parent1,
            bred.Parent2,
            bred.PassivesProbability,
            bred.IVsProbability
        ))
        {
            if (!MaterializedEntryFor(bred, choice.ChildEntry).Equals(selectedEntry))
                continue;
            if (!found || CompareChoices(choice, best) < 0)
            {
                best = choice;
                found = true;
            }
        }

        return found
            ? best
            : throw new InvalidOperationException(
                "The selected attack profile entry cannot be reconstructed."
            );
    }

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
        var guaranteedBreedings = (int)Math.Ceiling(1f / baseProbability);
        var dilutedBreedings = (int)Math.Ceiling(1f / (baseProbability * 0.5f));
        var guaranteedSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings, child, parent1.TimeFactor, parent2.TimeFactor, guaranteedBreedings
        );
        var dilutedSelfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings, child, parent1.TimeFactor, parent2.TimeFactor, dilutedBreedings
        );
        var cakeLoadouts = new ushort[AttackProfile.TargetMaskCount];

        foreach (var parent1Entry in parent1Profile.Entries)
            foreach (var parent2Entry in parent2Profile.Entries)
            {
                var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;
                var parentEffort = BredPalReferenceEffort.CombineParentEffort(
                    settings.GameSettings,
                    parent1,
                    parent2,
                    parent1Entry.BreedingEffort,
                    parent2Entry.BreedingEffort
                );

                var baseline = CreateChoice(
                    parent1Entry,
                    parent2Entry,
                    parentCakes,
                    parentEffort,
                    AttackCompositionMode.Baseline,
                    0,
                    0,
                    innateMask,
                    1,
                    usesSpecialCake: false
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
                    var probability = Probabilities.Attacks.ProbabilityInheritedTargetAttack(
                        parent1HasAttack,
                        parent2HasAttack,
                        parent1Profile.HasNoopAttack,
                        parent2Profile.HasNoopAttack
                    );
                    var normal = CreateChoice(
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
                        parentEffort,
                        AttackCompositionMode.InheritAll,
                        parent1Loadout,
                        parent2Loadout,
                        (byte)(innateMask | parent1Loadout | parent2Loadout),
                        1,
                        usesSpecialCake: true
                    );
                    if (cake is AttackCompositionChoice cakeChoice)
                        yield return cakeChoice;
                }
            }

        AttackCompositionChoice? CreateChoice(
            in AttackProfileEntry parent1Entry,
            in AttackProfileEntry parent2Entry,
            int parentCakes,
            TimeSpan parentEffort,
            AttackCompositionMode mode,
            byte parent1TargetMask,
            byte parent2TargetMask,
            byte childMask,
            float attackProbability,
            bool usesSpecialCake
        )
        {
            var guaranteed = attackProbability == 1;
            var selfBreedings = guaranteed ? guaranteedBreedings : dilutedBreedings;
            var totalCakes = parentCakes + (usesSpecialCake ? selfBreedings : 0);
            if (settings.MaxSpecialCakes is int maxCakes && totalCakes > maxCakes)
                return null;

            var childEntry = new AttackProfileEntry(
                childMask,
                totalCakes,
                parentEffort + (guaranteed ? guaranteedSelfEffort : dilutedSelfEffort),
                selfBreedings,
                usesSpecialCake
            );
            return childEntry.BreedingEffort <= settings.MaxEffort
                ? new(
                    parent1Entry,
                    parent2Entry,
                    mode,
                    parent1TargetMask,
                    parent2TargetMask,
                    childEntry,
                    attackProbability
                )
                : null;
        }
    }

    private static int CompareChoices(
        in AttackCompositionChoice left,
        in AttackCompositionChoice right
    )
    {
        var comparison = AttackProfileEntryComparer.Instance.Compare(left.ChildEntry, right.ChildEntry);
        if (comparison != 0) return comparison;
        comparison = left.Mode.CompareTo(right.Mode);
        if (comparison != 0) return comparison;
        comparison = left.Parent1TargetMask.CompareTo(right.Parent1TargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Parent2TargetMask.CompareTo(right.Parent2TargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Parent1Entry.LearnedTargetMask.CompareTo(right.Parent1Entry.LearnedTargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Parent2Entry.LearnedTargetMask.CompareTo(right.Parent2Entry.LearnedTargetMask);
        if (comparison != 0) return comparison;
        comparison = left.Parent1Entry.TotalSpecialCakes.CompareTo(right.Parent1Entry.TotalSpecialCakes);
        return comparison != 0
            ? comparison
            : left.Parent2Entry.TotalSpecialCakes.CompareTo(right.Parent2Entry.TotalSpecialCakes);
    }

    private ActiveSkill[] AttacksForMask(byte mask)
    {
        var attacks = new List<ActiveSkill>();
        for (var bit = (byte)1; bit != 0 && bit <= targets.FullTargetMask; bit <<= 1)
            if ((mask & bit) != 0)
                attacks.Add(targets.AttackForBit(bit));
        return attacks.ToArray();
    }

    private IReadOnlyList<ActiveSkill> LoadoutFor(IPalReference parent, byte targetMask)
    {
        var loadout = AttacksForMask(targetMask).ToList();
        if (loadout.Count == 0)
        {
            var filler = LearnedAttacks(parent)
                .OrderBy(attack => attack.CanInherit)
                .ThenBy(attack => attack.InternalName, StringComparer.Ordinal)
                .FirstOrDefault() ?? new RandomActiveSkill();
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

    private AttackProfileEntry MaterializedEntryFor(
        BredPalReference reference,
        AttackProfileEntry entry
    ) => reference.Gender == PalGender.WILDCARD
        ? entry
        : entry.WithGuaranteedGender(
            settings.GameSettings,
            reference.Pal,
            reference.Parent1.TimeFactor,
            reference.Parent2.TimeFactor,
            settings.DB,
            reference.Gender,
            settings.UseGenderReversers
        );
}
