using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;
using System.Runtime.CompilerServices;

namespace PalCalc.Solver.Processing.Attacks;

/// <summary>
/// One concrete inheritance witness for a bred pal: which profile entry each
/// parent uses, the inheritance mode, the target bits each parent contributes,
/// and the resulting child entry.
/// </summary>
internal readonly record struct AttackCompositionChoice(
    AttackProfileEntry Parent1Entry,
    AttackProfileEntry Parent2Entry,
    AttackInheritanceMode Mode,
    byte Parent1TargetMask,
    byte Parent2TargetMask,
    AttackProfileEntry ChildEntry
);

internal readonly record struct AttackMaterializationMetrics(
    TimeSpan BreedingEffort,
    int TotalSpecialCakes
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
    private static readonly ActiveSkill[] AnyAttackLoadout = [AnyAttack];

    private readonly AttackTargetContext targets;
    private readonly BreedingSolverSettings settings;
    private readonly ActiveSkill[][] attacksByMask;
    private readonly Dictionary<(Pal Pal, byte InheritedMask), ActiveSkill[]> childLearnedAttacks =
        [];
    private readonly Dictionary<ResultKey, EvaluatedResult> evaluated = [];
    private readonly Dictionary<ResultKey, MaterializedResult> materialized = [];

    public AttackResultMaterializer(AttackTargetContext targets, BreedingSolverSettings settings)
    {
        this.targets = targets;
        this.settings = settings;
        attacksByMask = new ActiveSkill[targets.FullTargetMask + 1][];
        attacksByMask[0] = [];
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

    public AttackMaterializationMetrics Evaluate(
        IPalReference reference,
        AttackProfileEntry selectedEntry
    )
    {
        var result = EvaluateResult(reference, selectedEntry);
        return new(result.BreedingEffort, result.TotalSpecialCakes);
    }

    private EvaluatedResult EvaluateResult(
        IPalReference reference,
        AttackProfileEntry selectedEntry
    )
    {
        ArgumentNullException.ThrowIfNull(reference);

        var key = new ResultKey(reference, selectedEntry);
        if (evaluated.TryGetValue(key, out var result))
            return result;

        result = reference switch
        {
            SurgeryTablePalReference surgery => EvaluateResult(surgery.Input, selectedEntry),
            BredPalReference bred => EvaluateBred(bred, selectedEntry),
            _ => EvaluateLeaf(reference, selectedEntry),
        };
        evaluated.Add(key, result);
        return result;
    }

    private MaterializedResult MaterializeResult(
        IPalReference reference,
        AttackProfileEntry selectedEntry
    )
    {
        ArgumentNullException.ThrowIfNull(reference);

        var key = new ResultKey(reference, selectedEntry);
        if (materialized.TryGetValue(key, out var result))
            return result;

        var evaluation = EvaluateResult(reference, selectedEntry);
        result = reference switch
        {
            SurgeryTablePalReference surgery => MaterializeSurgery(surgery, selectedEntry),
            BredPalReference bred => MaterializeChoice(bred, evaluation),
            _ => new(MaterializeLeaf(reference, selectedEntry), selectedEntry.TotalSpecialCakes),
        };
        materialized.Add(key, result);
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
    /// different concrete outcomes, so every match is evaluated and
    /// <see cref="CompareChoices"/> picks the winner: exact cakes, then effort,
    /// then deterministic tie-breaks on the witness fields. Only that winner
    /// is materialized.
    /// </summary>
    private EvaluatedResult EvaluateBred(
        BredPalReference bred,
        AttackProfileEntry selectedEntry
    )
    {
        var found = false;
        var best = default(EvaluatedChoice);
        foreach (var choice in EnumerateChoices(
            bred.Pal,
            bred.Parent1,
            bred.Parent2,
            bred.PassivesProbability,
            bred.IVsProbability,
            bred.SpecialCakePassivesProbability
        ))
        {
            if (!MatchesSearchEntry(choice, selectedEntry))
                continue;

            var candidate = EvaluateChoice(bred, choice);
            if (!found || CompareChoices(candidate, best) < 0)
            {
                best = candidate;
                found = true;
            }
        }

        return found
            ? new(
                best.BreedingEffort,
                best.TotalSpecialCakes,
                best.Choice,
                best.PassivesProbability,
                best.AttackProbability,
                best.RequiredBreedings
            )
            : throw new InvalidOperationException(
                "The selected attack profile entry cannot be reconstructed."
            );
    }

    private EvaluatedChoice EvaluateChoice(
        BredPalReference bred,
        AttackCompositionChoice choice
    )
    {
        var parent1 = EvaluateResult(bred.Parent1, choice.Parent1Entry);
        var parent2 = EvaluateResult(bred.Parent2, choice.Parent2Entry);
        var parentCakes = parent1.TotalSpecialCakes + parent2.TotalSpecialCakes;
        var attackProbability = AttackProbabilityFor(choice, bred.Parent1, bred.Parent2);
        var passivesProbability = choice.Mode == AttackInheritanceMode.InheritAll
            ? bred.SpecialCakePassivesProbability
            : bred.PassivesProbability;
        if (passivesProbability <= 0)
            throw new InvalidOperationException("A materialized attack choice has an impossible passive outcome.");
        var requiredBreedings = RequiredBreedings(
            bred,
            passivesProbability,
            attackProbability
        );
        var usesSpecialCake = choice.Mode == AttackInheritanceMode.InheritAll;
        var totalCakes = parentCakes + (usesSpecialCake ? requiredBreedings : 0);
        var parentEffort = BredPalReferenceEffort.CombineParentEffort(
            settings.GameSettings,
            bred.Parent1,
            bred.Parent2
        );
        var selfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings,
            bred.Pal,
            bred.Parent1.TimeFactor,
            bred.Parent2.TimeFactor,
            requiredBreedings
        );

        return new(
            choice,
            passivesProbability,
            attackProbability,
            requiredBreedings,
            totalCakes,
            parentEffort + selfEffort
        );
    }

    private MaterializedResult MaterializeChoice(
        BredPalReference bred,
        in EvaluatedResult evaluated
    )
    {
        var choice = evaluated.Choice ?? throw new InvalidOperationException(
            "A bred result has no selected attack-inheritance witness."
        );
        var parent1 = MaterializeResult(bred.Parent1, choice.Parent1Entry);
        var parent2 = MaterializeResult(bred.Parent2, choice.Parent2Entry);
        var inheritedMask = (byte)(choice.Parent1TargetMask | choice.Parent2TargetMask);
        var inheritedAttacks = AttacksForMask(inheritedMask);
        var actualEntry = new AttackProfileEntry(
            choice.ChildEntry.LearnedTargetMask,
            evaluated.TotalSpecialCakes
        );
        // A normal-inheritance roll is guaranteed only when the parent that
        // contributes no target attack equips a non-inheritable one: Palworld
        // excludes it from the roll, leaving the other parent's attack the
        // sole candidate. The search-time profile recorded only HasNoopAttack,
        // not which attack, so the concrete filler is picked here.
        var normalInheritance = choice.Mode == AttackInheritanceMode.Normal;
        var parent1RequiresNoop = normalInheritance &&
            choice.Parent1TargetMask == 0 &&
            choice.Parent2TargetMask != 0 &&
            bred.Parent1.AttackProfile.HasNoopAttack;
        var parent2RequiresNoop = normalInheritance &&
            choice.Parent2TargetMask == 0 &&
            choice.Parent1TargetMask != 0 &&
            bred.Parent2.AttackProfile.HasNoopAttack;
        var inheritance = new MaterializedAttackInheritance(
            choice.Mode,
            LoadoutFor(parent1.Reference, choice.Parent1TargetMask, parent1RequiresNoop),
            LoadoutFor(parent2.Reference, choice.Parent2TargetMask, parent2RequiresNoop),
            inheritedAttacks,
            ChildLearnedAttacksFor(bred.Pal, inheritedMask),
            choice.Mode == AttackInheritanceMode.InheritAll ? evaluated.RequiredBreedings : 0,
            evaluated.AttackProbability
        );
        var reference = new BredPalReference(
            settings.GameSettings,
            bred.Pal,
            gender: bred.Gender,
            parent1.Reference,
            parent2.Reference,
            avgRequiredBreedings: evaluated.RequiredBreedings,
            [.. bred.EffectivePassives],
            evaluated.PassivesProbability,
            bred.SpecialCakePassivesProbability,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, actualEntry),
            materializedAttackInheritance: inheritance
        );

        return new(reference, evaluated.TotalSpecialCakes);
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
    private int RequiredBreedings(
        BredPalReference bred,
        float passivesProbability,
        float attackProbability
    )
    {
        var requiredBreedings = (int)Math.Ceiling(
            1f / (passivesProbability * bred.IVsProbability * attackProbability)
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
        AttackInheritanceMode.Baseline or AttackInheritanceMode.InheritAll => 1,
        AttackInheritanceMode.Normal => Probabilities.Attacks.ProbabilityInheritedTargetAttack(
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

    private static EvaluatedResult EvaluateLeaf(
        IPalReference reference,
        in AttackProfileEntry selectedEntry
    )
    {
        MaterializeLeaf(reference, selectedEntry);
        return new(
            reference.BreedingEffort,
            selectedEntry.TotalSpecialCakes,
            Choice: null,
            PassivesProbability: 1,
            AttackProbability: 1,
            RequiredBreedings: 0
        );
    }

    private readonly record struct EvaluatedChoice(
        AttackCompositionChoice Choice,
        float PassivesProbability,
        float AttackProbability,
        int RequiredBreedings,
        int TotalSpecialCakes,
        TimeSpan BreedingEffort
    );

    private readonly record struct EvaluatedResult(
        TimeSpan BreedingEffort,
        int TotalSpecialCakes,
        AttackCompositionChoice? Choice,
        float PassivesProbability,
        float AttackProbability,
        int RequiredBreedings
    );

    private readonly record struct MaterializedResult(
        IPalReference Reference,
        int TotalSpecialCakes
    );

    private readonly struct ResultKey(
        IPalReference reference,
        AttackProfileEntry entry
    ) : IEquatable<ResultKey>
    {
        private readonly IPalReference reference = reference;
        private readonly AttackProfileEntry entry = entry;

        // Distinct search nodes can be structurally equal but require separate
        // reconstruction cache entries. Keep the hash consistent with
        // ReferenceEquals by bypassing IPalReference.GetHashCode overrides.
        public bool Equals(ResultKey other) =>
            ReferenceEquals(reference, other.reference) && entry.Equals(other.entry);

        public override bool Equals(object obj) =>
            obj is ResultKey other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(reference), entry);
    }

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
        float ivsProbability,
        float specialCakePassivesProbability
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
        var cakeBaseProbability = specialCakePassivesProbability * ivsProbability;
        var cakeBreedings = cakeBaseProbability > 0
            ? (int)Math.Ceiling(1f / cakeBaseProbability)
            : int.MaxValue;
        foreach (var parent1Entry in parent1Profile.Entries)
            foreach (var parent2Entry in parent2Profile.Entries)
            {
                var parentCakes = parent1Entry.TotalSpecialCakes + parent2Entry.TotalSpecialCakes;

                var baseline = CreateChoice(
                    parent1Entry,
                    parent2Entry,
                    parentCakes,
                    AttackInheritanceMode.Baseline,
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
                        AttackInheritanceMode.Normal,
                        parent1HasAttack ? bit : (byte)0,
                        parent2HasAttack ? bit : (byte)0,
                        (byte)(innateMask | bit)
                    );
                    if (normal is AttackCompositionChoice normalChoice)
                        yield return normalChoice;
                }

                if (settings.MaxSpecialCakes == 0 || cakeBaseProbability <= 0)
                    continue;

                var cakeLoadouts = AttackProfileComposer.CakeMasksFor(
                    parent1Mask,
                    parent2Mask
                );
                for (var i = 0; i < cakeLoadouts.Count; i++)
                {
                    var loadouts = cakeLoadouts[i];
                    var parent1Loadout = (byte)(loadouts >> 8);
                    var parent2Loadout = (byte)loadouts;
                    var cake = CreateChoice(
                        parent1Entry,
                        parent2Entry,
                        parentCakes,
                        AttackInheritanceMode.InheritAll,
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
            AttackInheritanceMode mode,
            byte parent1TargetMask,
            byte parent2TargetMask,
            byte childMask
        )
        {
            var totalCakes = parentCakes +
                (mode == AttackInheritanceMode.InheritAll ? cakeBreedings : 0);
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
        in EvaluatedChoice left,
        in EvaluatedChoice right
    )
    {
        var comparison = left.TotalSpecialCakes.CompareTo(right.TotalSpecialCakes);
        if (comparison != 0) return comparison;
        comparison = left.BreedingEffort.CompareTo(right.BreedingEffort);
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
        if (mask > targets.FullTargetMask)
            throw new ArgumentOutOfRangeException(nameof(mask));
        if (attacksByMask[mask] is { } cached)
            return cached;

        var count = 0;
        for (var bits = mask; bits != 0; bits &= (byte)(bits - 1))
            count++;

        var attacks = new ActiveSkill[count];
        var index = 0;
        for (var bit = (byte)1; bit != 0 && bit <= targets.FullTargetMask; bit <<= 1)
            if ((mask & bit) != 0)
                attacks[index++] = targets.AttackForBit(bit);
        return attacksByMask[mask] = attacks;
    }

    private ActiveSkill[] ChildLearnedAttacksFor(Pal child, byte inheritedMask)
    {
        var key = (child, inheritedMask);
        if (childLearnedAttacks.TryGetValue(key, out var attacks))
            return attacks;

        attacks = AttacksForMask(inheritedMask)
            .Concat(child.Level1ActiveSkills(settings.DB))
            .Distinct()
            .ToArray();
        childLearnedAttacks.Add(key, attacks);
        return attacks;
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
        if (targetMask != 0)
        {
            var loadout = AttacksForMask(targetMask);
            if (loadout.Length > 3)
                throw new InvalidOperationException("A parent loadout must contain one to three attacks.");
            return loadout;
        }

        if (!requiresNoop)
            return AnyAttackLoadout;

        var filler = LearnedAttacks(parent)
            .Where(attack => !attack.CanInherit)
            .OrderBy(attack => attack.InternalName, StringComparer.Ordinal)
            .FirstOrDefault() ?? throw new InvalidOperationException(
                "The selected attack profile entry requires a non-inheritable parent attack that cannot be reconstructed."
            );
        return [filler];
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
