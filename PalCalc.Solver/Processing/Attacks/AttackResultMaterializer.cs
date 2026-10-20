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
)
{
    public bool Covers(AttackMaterializationMetrics other) =>
        BreedingEffort <= other.BreedingEffort && TotalSpecialCakes <= other.TotalSpecialCakes;

    public bool Dominates(AttackMaterializationMetrics other) =>
        Covers(other) && this != other;
}

// these only live during finalization. keep the chosen parents with each result
// so materializing it later uses the same breeding path we evaluated.
internal sealed record EvaluatedAttackResult(
    IPalReference Source,
    byte TargetMask,
    AttackMaterializationMetrics Metrics,
    AttackCompositionChoice? Choice = null,
    int RequiredBreedings = 0,
    float AttackProbability = 1,
    EvaluatedAttackResult Parent1 = null,
    EvaluatedAttackResult Parent2 = null,
    EvaluatedAttackResult Input = null)
{
    public TimeSpan BreedingEffort => Metrics.BreedingEffort;
    public int TotalSpecialCakes => Metrics.TotalSpecialCakes;
}

/// <summary>
/// A finalization step used by <see cref="ResultPostProcessor"/> to evaluate
/// attack-inheritance alternatives for a search-time <see cref="IPalReference"/>,
/// then reconstruct the selected alternatives as materialized references.
///
/// Search-time references carry a summary of possible attack outcomes and
/// their estimated Special Cake costs. Materialized references additionally
/// carry an exact <see cref="MaterializedAttackInheritance"/> (probabilities,
/// effort, cake totals, parent loadouts). Reconstruction is recursive: a bred
/// pal retains effort/cake tradeoffs for the requested attacks, including the
/// exact parent choices for each. Reconstruction does not require matching the
/// selected search entry's estimated cake count. Retained parent profiles,
/// breeding method, loadout feasibility, and cake-budget checks still constrain
/// choices. Only selected alternatives are materialized.
///
/// Each reconstructed bred reference has one selected attack outcome in its profile. Owned
/// leaves retain their learned-attack availability; their final equipped attacks
/// are recorded on the downstream materialized inheritance.
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
    private readonly Dictionary<ResultKey, IReadOnlyList<EvaluatedAttackResult>> evaluated = [];
    private readonly Dictionary<EvaluatedAttackResult, IPalReference> materialized =
        new(ReferenceEqualityComparer.Instance);
    private readonly CancellationToken cancellationToken;
    private readonly Dictionary<IPalReference, TimeSpan> effortBounds = new(ReferenceEqualityComparer.Instance);

    internal int EvaluatedStateCount => evaluated.Count;
    internal int MaterializedCount => materialized.Count;
    internal int EvaluationCacheHits { get; private set; }
    internal int EvaluatedChoiceCount { get; private set; }

    public AttackMaterializationMetrics LowerBounds(IPalReference source, AttackProfileEntry entry) =>
        new(EffortLowerBound(source), entry.TotalSpecialCakes);

    private TimeSpan EffortLowerBound(IPalReference source)
    {
        if (effortBounds.TryGetValue(source, out var cached))
            return cached;

        // this reference might already include a specific attack-inheritance choice.
        // calculate again without attack rolls so we still get a lower bound.
        var result = source switch
        {
            SurgeryTablePalReference surgery => EffortLowerBound(surgery.Input),
            BredPalReference bred =>
                BredPalReferenceEffort.CombineParentEffort(settings.GameSettings, bred.Parent1, EffortLowerBound(bred.Parent1), bred.Parent2, EffortLowerBound(bred.Parent2)) +
                BredPalReferenceEffort.CalculateSelfBreedingEffort(settings.GameSettings, bred.Pal, bred.Parent1.TimeFactor, bred.Parent2.TimeFactor, RequiredBreedings(bred, bred.PassivesProbability, attackProbability: 1)),

            OwnedPalReference or WildPalReference or CompositeOwnedPalReference => source.BreedingEffort,
            _ => TimeSpan.Zero,
        };
        effortBounds.Add(source, result);
        return result;
    }

    public AttackResultMaterializer(
        AttackTargetContext targets,
        BreedingSolverSettings settings,
        CancellationToken cancellationToken = default)
    {
        this.targets = targets;
        this.settings = settings;
        this.cancellationToken = cancellationToken;
        attacksByMask = new ActiveSkill[targets.FullTargetMask + 1][];
        attacksByMask[0] = [];
    }

    /// <summary>
    /// Finds effort/cake tradeoffs for the requested attacks without constructing
    /// the breeding trees.
    /// <para>
    /// During the solver process, an <see cref="IPalReference"/> only tracks its
    /// potential attack outcomes. Exact parent effort and cake totals come from
    /// the recursively evaluated results, not from the search-time profile,
    /// which only estimates them.
    /// </para>
    /// </summary>
    public IReadOnlyList<EvaluatedAttackResult> Evaluate(IPalReference reference, byte targetMask)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();
        var key = new ResultKey(reference, targetMask);
        if (evaluated.TryGetValue(key, out var cached))
        {
            EvaluationCacheHits++;
            return cached;
        }

        IReadOnlyList<EvaluatedAttackResult> result;
        if (reference is SurgeryTablePalReference surgery)
        {
            // surgery only changes passives, so we can pass the same attack requirements
            // through to the input. its AttackProfile delegates to the input too.
            result = Evaluate(surgery.Input, targetMask)
                .Select(input => new EvaluatedAttackResult(reference, targetMask, input.Metrics, Input: input))
                .ToArray();
        }
        else if (reference is BredPalReference bred)
        {
            result = EvaluateBred(bred, targetMask);
        }
        else
        {
            var entry = reference.AttackProfile.Entries.Single(e => e.LearnedTargetMask == targetMask);
            result = [
                new(reference, targetMask, new(reference.BreedingEffort, entry.TotalSpecialCakes))
            ];
        }
        evaluated.Add(key, result);
        return result;
    }

    /// <summary>Builds the breeding tree using the exact parent choices retained during evaluation.</summary>
    public IPalReference Materialize(EvaluatedAttackResult selected)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (materialized.TryGetValue(selected, out var cached))
            return cached;

        var result = selected.Source switch
        {
            SurgeryTablePalReference surgery => new SurgeryTablePalReference(Materialize(selected.Input), surgery.Operations),
            BredPalReference bred => MaterializeChoice(bred, selected),
            _ => MaterializeLeaf(selected.Source, new AttackProfileEntry(selected.TargetMask, selected.TotalSpecialCakes)),
        };
        materialized.Add(selected, result);
        return result;
    }

    // there may be several ways to get the same target attacks, with different effort
    // and cake costs. keep the tradeoffs; if both costs match, use a consistent
    // tie-break so the result doesn't depend on which option we checked first.
    private IReadOnlyList<EvaluatedAttackResult> EvaluateBred(BredPalReference bred, byte targetMask)
    {
        var alternatives = new List<EvaluatedAttackResult>();
        var foundWitness = false;
        foreach (var choice in EnumerateChoices(
            bred.Pal, bred.Parent1, bred.Parent2, bred.Outcome, bred.IVsProbability))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (choice.ChildEntry.LearnedTargetMask != targetMask)
                continue;

            foundWitness = true;
            var first = Evaluate(bred.Parent1, choice.Parent1Entry.LearnedTargetMask);
            var second = Evaluate(bred.Parent2, choice.Parent2Entry.LearnedTargetMask);
            foreach (var parent1 in first)
                foreach (var parent2 in second)
                {
                    EvaluatedChoiceCount++;
                    var candidate = EvaluateChoice(bred, choice, parent1, parent2);
                    // a more expensive parent can't make a cheaper child here: parent effort is
                    // combined with sum/max, and cake counts are added. so if another option is
                    // at least as good on both costs and better on one, we can drop this one.
                    var equal = alternatives.FindIndex(existing => existing.Metrics == candidate.Metrics);
                    if (equal >= 0)
                    {
                        if (CompareWitnesses(candidate, alternatives[equal]) < 0)
                            alternatives[equal] = candidate;
                        continue;
                    }
                    if (alternatives.Any(existing => existing.Metrics.Dominates(candidate.Metrics)))
                        continue;

                    alternatives.RemoveAll(existing => candidate.Metrics.Dominates(existing.Metrics));
                    alternatives.Add(candidate);
                }
        }
        if (!foundWitness)
            throw new InvalidOperationException("The selected attack mask cannot be reconstructed.");
        return alternatives;
    }

    private static int CompareWitnesses(EvaluatedAttackResult left, EvaluatedAttackResult right)
    {
        var first = left.Choice.Value;
        var second = right.Choice.Value;

        var firstAttrs  = (first.Mode, first.Parent1TargetMask, first.Parent2TargetMask, first.Parent1Entry.LearnedTargetMask, first.Parent2Entry.LearnedTargetMask, first.Parent1Entry.TotalSpecialCakes, first.Parent2Entry.TotalSpecialCakes);
        var secondAttrs = (second.Mode, second.Parent1TargetMask, second.Parent2TargetMask, second.Parent1Entry.LearnedTargetMask, second.Parent2Entry.LearnedTargetMask, second.Parent1Entry.TotalSpecialCakes, second.Parent2Entry.TotalSpecialCakes);

        return firstAttrs.CompareTo(secondAttrs);
    }

    private EvaluatedAttackResult EvaluateChoice(
        BredPalReference bred,
        AttackCompositionChoice choice,
        EvaluatedAttackResult parent1,
        EvaluatedAttackResult parent2)
    {
        var attackProbability = AttackProbabilityFor(choice, bred.Parent1, bred.Parent2);
        if (bred.PassivesProbability <= 0)
            throw new InvalidOperationException("A materialized attack choice has an impossible passive outcome.");

        var requiredBreedings = RequiredBreedings(bred, bred.PassivesProbability, attackProbability);
        var totalCakes = parent1.TotalSpecialCakes + parent2.TotalSpecialCakes + bred.Outcome.SpecialCakesFor(requiredBreedings);

        var parentEffort = BredPalReferenceEffort.CombineParentEffort(
            settings.GameSettings,
            bred.Parent1, parent1.BreedingEffort,
            bred.Parent2, parent2.BreedingEffort
        );

        var selfEffort = BredPalReferenceEffort.CalculateSelfBreedingEffort(
            settings.GameSettings,
            bred.Pal,
            bred.Parent1.TimeFactor, bred.Parent2.TimeFactor,
            requiredBreedings
        );

        return new EvaluatedAttackResult(
            bred,
            choice.ChildEntry.LearnedTargetMask,
            new AttackMaterializationMetrics(parentEffort + selfEffort, totalCakes),
            choice,
            requiredBreedings,
            attackProbability,
            parent1, parent2
        );
    }

    private IPalReference MaterializeChoice(
        BredPalReference bred,
        EvaluatedAttackResult evaluated
    )
    {
        var choice = evaluated.Choice ?? throw new InvalidOperationException(
            "A bred result has no selected attack-inheritance witness."
        );
        var parent1 = Materialize(evaluated.Parent1);
        var parent2 = Materialize(evaluated.Parent2);
        var inheritedMask = (byte)(choice.Parent1TargetMask | choice.Parent2TargetMask);
        var inheritedAttacks = AttacksForMask(inheritedMask);
        var actualEntry = new AttackProfileEntry(
            choice.ChildEntry.LearnedTargetMask,
            evaluated.TotalSpecialCakes
        );

        // if only one parent contributes the target attack, we can guarantee inheritance
        // by having the other parent equip a non-inheritable attack. Palworld excludes
        // that attack from the roll, leaving only the one we want.
        //
        // during search we only tracked HasNoopAttack, so we still need to pick the
        // actual non-inheritable attack to show in the parent's loadout.
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
            LoadoutFor(parent1, choice.Parent1TargetMask, parent1RequiresNoop),
            LoadoutFor(parent2, choice.Parent2TargetMask, parent2RequiresNoop),
            inheritedAttacks,
            ChildLearnedAttacksFor(bred.Pal, inheritedMask),
            bred.Outcome.SpecialCakesFor(evaluated.RequiredBreedings),
            evaluated.AttackProbability
        );

        var reference = new BredPalReference(
            settings.GameSettings,
            bred.Pal,
            gender: bred.Gender,
            parent1,
            parent2,
            avgRequiredBreedings: evaluated.RequiredBreedings,
            [.. bred.EffectivePassives],
            bred.Outcome,
            bred.IVs,
            bred.IVsProbability,
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, actualEntry),
            materializedAttackInheritance: inheritance
        );

        return reference;
    }

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
    /// both are certain. Normal inheritance is guaranteed when both parents
    /// contribute the target attack or the other parent uses a non-inheritable
    /// attack; otherwise it has a 50% chance.
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

    private readonly struct ResultKey(IPalReference reference, byte targetMask) : IEquatable<ResultKey>
    {
        private readonly IPalReference reference = reference;
        private readonly byte targetMask = targetMask;
        // two search nodes can compare equal but still need their own reconstruction.
        // use reference identity for both equality and hashing here, rather than the
        // equality/hash overrides on IPalReference.
        public bool Equals(ResultKey other) =>
            ReferenceEquals(reference, other.reference) && targetMask == other.targetMask;
        public override bool Equals(object obj) => obj is ResultKey other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(RuntimeHelpers.GetHashCode(reference), targetMask);
    }

    /// <summary>
    /// Enumerates choices from the retained parent profiles, respecting the
    /// breeding method and inheritance feasibility. Cake choices use cached
    /// loadouts that cover the largest possible sets of target attacks.
    /// Estimated cake costs are checked against the configured MaxSpecialCakes limit.
    /// </summary>
    internal IEnumerable<AttackCompositionChoice> EnumerateChoices(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        BreedingOutcome outcome,
        float ivsProbability
    )
    {
        if (!targets.IsActive)
            yield break;

        var baseProbability = outcome.PassivesProbability * ivsProbability;
        if (baseProbability <= 0)
            yield break;

        var innateMask = targets.StateOf(child).Level1TargetMask;
        var inheritableTargetMask = targets.InheritableTargetMask;
        var parent1Profile = parent1.AttackProfile;
        var parent2Profile = parent2.AttackProfile;
        var edgeCakes = outcome.SpecialCakesFor((int)Math.Ceiling(1f / baseProbability));
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

                if (settings.MaxSpecialCakes == 0 || !outcome.Allows(AttackInheritanceMode.InheritAll))
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
            if (!outcome.Allows(mode))
                return null;
            if (mode == AttackInheritanceMode.InheritAll &&
                !AttackProfileComposer.RequiresSpecialCake(innateMask, (byte)(parent1TargetMask | parent2TargetMask)))
                return null;

            var totalCakes = parentCakes + edgeCakes;
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
            OwnedPalReference owned => (owned.UnderlyingInstance.ActiveSkills ?? []).Concat(TrainingAttacks(owned)),
            WildPalReference wild => wild.Pal.WildActiveSkills(settings.DB).Concat(TrainingAttacks(wild)),
            // we only combine owned pals when their attack profiles match (see InitialPalBuilder),
            // so we can use the male's attacks when picking a loadout for the pair.
            CompositeOwnedPalReference composite => composite.Male.UnderlyingInstance.ActiveSkills ?? [],
            BredPalReference { MaterializedAttackInheritance: not null } bred => bred.MaterializedAttackInheritance.ChildLearnedAttacks,
            _ => reference.Pal.Level1ActiveSkills(settings.DB),
        };

    private IEnumerable<ActiveSkill> TrainingAttacks(IPalReference reference) =>
        reference.LevelRequirements is { } levels
            ? reference.Pal.AttackLeveling(settings.DB)
                .Where(entry => entry.Level > levels.InitialLevel && entry.Level <= levels.FinalLevel)
                .Select(entry => entry.Attack)
            : [];

}
