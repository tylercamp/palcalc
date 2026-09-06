using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// The inheritance mode of one reconstruction witness. The declaration order
/// mirrors <see cref="AttackInheritanceMode"/>, and the materializer converts
/// between the two via a numeric cast, so the two enums must stay in sync.
/// </summary>
internal enum AttackCompositionMode
{
    Baseline,
    Normal,
    InheritAll,
}

/// <summary>
/// One concrete inheritance witness for a bred pal: which profile entry each
/// parent uses, the inheritance mode, the target bits each parent contributes,
/// and the resulting child entry.
/// </summary>
internal readonly record struct AttackCompositionChoice(
    AttackProfileEntry Parent1Entry,
    AttackProfileEntry Parent2Entry,
    AttackCompositionMode Mode,
    byte Parent1TargetMask,
    byte Parent2TargetMask,
    AttackProfileEntry ChildEntry
);

/// <summary>
/// A finalization step used by <see cref="ResultPostProcessor"/>: given a
/// search-time <see cref="IPalReference"/> and one of its profile entries, it
/// reconstructs a materialized <see cref="IPalReference"/> for it.
///
/// Search-time references carry only a lossy profile (per-mask availability
/// and an estimated Special Cake cost). Materialized references additionally
/// carry an exact <see cref="MaterializedAttackInheritance"/> (probabilities,
/// effort, cake totals, parent loadouts). Reconstruction is recursive: a bred
/// pal is realized from a witness whose child outcome matches the selected
/// entry exactly, with each parent recursively materialized for the entry that
/// witness depends on; a leaf or surgery pal must already contain the selected
/// entry.
///
/// Every reference in the reconstructed tree carries a single-entry
/// <see cref="AttackProfile"/>, since a materialized reference has exactly one
/// resolved attack outcome; <see cref="ResultPostProcessor"/> relies on this
/// for its final constraint check.
/// </summary>
internal sealed class AttackResultMaterializer
{
    // Wildcard filler for a loadout slot that contributes no target attack; the
    // user-facing "Any Attack".
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
    /// <para>
    ///     Reconstructs a materialized <see cref="IPalReference"/> for the given
    ///     search-time reference and one of its profile entries.
    /// </para>
    /// <para>
    ///     During the solver process, an <see cref="IPalReference"/> only tracks
    ///     its <em>potential</em> attack outcomes. This method does the work of
    ///     traversing the tree, choosing the specific attack-breeding paths as
    ///     necessary, all while respecting Palworld's general limits for attack
    ///     inheritance. Exact parent effort and cake totals come from the
    ///     recursively materialized results, not from the search-time profile,
    ///     which only estimates them.
    /// </para>
    /// </summary>
    /// <param name="reference">The search-time reference to reconstruct.</param>
    /// <param name="selectedEntry">
    ///     The profile entry to materialize. It must be one of
    ///     <c>reference.AttackProfile.Entries</c>, matching exactly on both
    ///     <c>LearnedTargetMask</c> and <c>TotalSpecialCakes</c>.
    /// </param>
    /// <returns>
    ///     A materialized reference for the same pal. Bred nodes additionally
    ///     carry a <see cref="MaterializedAttackInheritance"/>, and every node
    ///     in the reconstructed tree has a single-entry
    ///     <see cref="AttackProfile"/>.
    /// </returns>
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

    /// <summary>
    /// Surgery only rewrites passive skills;
    /// <c>SurgeryTablePalReference.AttackProfile</c> delegates to its input, so
    /// the same selected entry passes through unchanged.
    /// </summary>
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

    /// <summary>
    /// Finds the witness matching <c>selectedEntry</c> at the lowest actual
    /// cost. Several witnesses can realize the same (mask, cake) pair with
    /// different concrete outcomes, so every match is materialized and
    /// <see cref="CompareChoices"/> picks the winner: exact cakes, then effort,
    /// then deterministic tie-breaks on the witness fields.
    /// </summary>
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
        // A normal-inheritance roll is guaranteed only when the parent that
        // contributes no target attack equips a non-inheritable one: Palworld
        // excludes it from the roll, leaving the other parent's attack the
        // sole candidate. The search-time profile recorded only HasNoopAttack,
        // not which attack, so the concrete filler is picked here.
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
            gender: bred.Gender,
            parent1.Reference,
            parent2.Reference,
            avgRequiredBreedings: requiredBreedings,
            [.. bred.EffectivePassives],
            bred.PassivesProbability,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, actualEntry),
            materializedAttackInheritance: inheritance
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

    // Exact expected attempts for the realized outcome; unlike the search-time
    // estimate, they include the attack probability and any guaranteed-gender
    // adjustment.
    private int RequiredBreedings(BredPalReference bred, float attackProbability)
    {
        var requiredBreedings = (int)Math.Ceiling(
            1f / (bred.PassivesProbability * bred.IVsProbability * attackProbability)
        );
        return bred.Gender == PalGender.WILDCARD
            ? requiredBreedings
            : BredPalReferenceEffort.WithGuaranteedGender(
                settings.DB,
                bred.Pal,
                requiredBreedings,
                bred.Gender,
                settings.UseGenderReversers
            );
    }

    /// <summary>
    /// Probability that a single breeding attempt yields the target attack.
    /// Baseline and special-cake inheritance need no inheritance roll (the
    /// child keeps its level-1 attacks; cakes force the equipped ones), so
    /// both are certain; normal inheritance follows the 100%/50% rule above.
    /// </summary>
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
    /// Exhaustively enumerates every concrete witness for the given parents and
    /// child. Unlike search, which only needs the cheapest entry per mask (the
    /// profile-only algorithm in <see cref="AttackProfileComposer"/>),
    /// reconstruction must see all witnesses matching the selected entry so it
    /// can pick the one whose recursively materialized parents cost the least.
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

    /// <summary>
    /// Builds a parent loadout of one to three attacks: the target attacks the
    /// parent contributes, or a single filler. A roll that must be guaranteed
    /// needs a non-inheritable filler (picked deterministically); otherwise the
    /// filler is the <c>AnyAttack</c> wildcard.
    /// </summary>
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
            // A composite only exists when both copies share the same attack
            // profile (see InitialPalBuilder), so the male's attacks stand in
            // for the pair.
            CompositeOwnedPalReference composite => composite.Male.UnderlyingInstance.ActiveSkills ?? [],
            BredPalReference { MaterializedAttackInheritance: not null } bred =>
                bred.MaterializedAttackInheritance.ChildLearnedAttacks,
            _ => reference.Pal.Level1ActiveSkills(settings.DB),
        };

}
