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
    byte Parent1Mask,
    byte Parent2Mask,
    AttackInheritanceMode Mode,
    byte Parent1TargetMask,
    byte Parent2TargetMask,
    byte ChildMask
);

// these only live during finalization. keep the chosen parents with each result
// so materializing it later uses the same breeding path we evaluated.
internal sealed record EvaluatedAttackResult(
    IPalReference Source,
    byte TargetMask,
    BreedingPathMetrics Metrics,
    AttackCompositionChoice? Choice,
    int RequiredBreedings,
    float AttackProbability,
    EvaluatedAttackResult Parent1,
    EvaluatedAttackResult Parent2,
    EvaluatedAttackResult OriginalResult // for surgery
)
{
    public TimeSpan BreedingEffort => Metrics.BreedingEffort;
    public int TotalSpecialCakes => Metrics.TotalSpecialCakes;

    public static EvaluatedAttackResult Noop(IPalReference source, byte targetMask, BreedingPathMetrics metrics, EvaluatedAttackResult originalResult) =>
        new(
            Source: source, TargetMask: targetMask, Metrics: metrics,
            Choice: null, RequiredBreedings: 0, AttackProbability: 1f,
            Parent1: null, Parent2: null, OriginalResult: originalResult
        );
}

/// <summary>
/// A finalization step used by <see cref="ResultPostProcessor"/> to evaluate
/// attack-inheritance alternatives for a search-time <see cref="IPalReference"/>,
/// then reconstruct the selected alternatives as materialized references.
///
/// Search records feasible attack masks and estimates effort and cake costs
/// for each breeding path. Reconstruction selects concrete parent loadouts,
/// then computes effort and cake use from the required attempts. Retained
/// parent profiles, breeding methods, and cake policy constrain the choices.
/// Only selected alternatives are materialized.
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

    public BreedingPathMetrics LowerBounds(IPalReference source) =>
        new(EffortLowerBound(source), source.BaseSpecialCakes);

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
        CancellationToken cancellationToken = default
    )
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
    /// the recursively evaluated results, rather than the estimates used during search.
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
                .Select(input => EvaluatedAttackResult.Noop(reference, targetMask, input.Metrics, input))
                .ToArray();
        }
        else if (reference is BredPalReference bred)
        {
            result = EvaluateBred(bred, targetMask);
        }
        else
        {
            if ((reference.AttackProfile.EntryTargetMasks & (1UL << targetMask)) == 0)
                throw new InvalidOperationException("The selected attack mask cannot be reconstructed.");
            result = [
                EvaluatedAttackResult.Noop(reference, targetMask, new(reference.BreedingEffort, reference.ExpectedSpecialCakes), originalResult: null)
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
            SurgeryTablePalReference surgery => new SurgeryTablePalReference(Materialize(selected.OriginalResult), surgery.Operations),
            BredPalReference bred => MaterializeChoice(bred, selected),
            _ => selected.Source,
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
            if (choice.ChildMask != targetMask)
                continue;

            foundWitness = true;
            var first = Evaluate(bred.Parent1, choice.Parent1Mask);
            var second = Evaluate(bred.Parent2, choice.Parent2Mask);
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

        var firstAttrs  = (first.Mode, first.Parent1TargetMask, first.Parent2TargetMask, first.Parent1Mask, first.Parent2Mask);
        var secondAttrs = (second.Mode, second.Parent1TargetMask, second.Parent2TargetMask, second.Parent1Mask, second.Parent2Mask);

        return firstAttrs.CompareTo(secondAttrs);
    }

    private EvaluatedAttackResult EvaluateChoice(
        BredPalReference bred,
        AttackCompositionChoice choice,
        EvaluatedAttackResult parent1,
        EvaluatedAttackResult parent2
    )
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
            choice.ChildMask,
            new BreedingPathMetrics(parentEffort + selfEffort, totalCakes),
            choice,
            requiredBreedings,
            attackProbability,
            parent1, parent2,
            OriginalResult: null
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
            attackProfile: new AttackProfile(bred.AttackProfile.HasNoopAttack, choice.ChildMask),
            materializedAttackInheritance: inheritance,
            recordedSpecialCakes: null
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

    private readonly struct ResultKey(IPalReference reference, byte targetMask) : IEquatable<ResultKey>
    {
        private readonly IPalReference reference = reference;
        private readonly byte targetMask = targetMask;
        // two search nodes can compare equal but still need their own reconstruction.
        // use reference identity for both equality and hashing here, rather than the
        // equality/hash overrides on IPalReference.
        public bool Equals(ResultKey other) => ReferenceEquals(reference, other.reference) && targetMask == other.targetMask;
        public override bool Equals(object obj) => obj is ResultKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(reference), targetMask);
    }

    /// <summary>
    /// Enumerates choices from the retained parent profiles, respecting the
    /// breeding method and inheritance feasibility. Cake choices use cached
    /// loadouts that cover the largest possible sets of target attacks.
    /// Cake eligibility follows the same usage policy as search-time composition.
    /// </summary>
    internal IEnumerable<AttackCompositionChoice> EnumerateChoices(
        Pal child,
        IPalReference parent1,
        IPalReference parent2,
        BreedingOutcome outcome,
        float ivsProbability
    )
    {
        if (!targets.HasRequiredAttacks)
            yield break;

        var baseProbability = outcome.PassivesProbability * ivsProbability;
        if (baseProbability <= 0)
            yield break;

        var innateMask = targets.StateOf(child).Level1TargetMask;
        var inheritableTargetMask = targets.InheritableTargetMask;
        var parent1Profile = parent1.AttackProfile;
        var parent2Profile = parent2.AttackProfile;
        foreach (var firstMask in parent1Profile.Masks)
            foreach (var secondMask in parent2Profile.Masks)
            {
                var baseline = CreateChoice(
                    firstMask,
                    secondMask,
                    AttackInheritanceMode.Baseline,
                    0,
                    0,
                    innateMask
                );
                if (baseline is AttackCompositionChoice baselineChoice)
                    yield return baselineChoice;

                var parent1Mask = (byte)(firstMask & inheritableTargetMask);
                var parent2Mask = (byte)(secondMask & inheritableTargetMask);
                var normalTargets = (byte)((parent1Mask | parent2Mask) & ~innateMask);
                while (normalTargets != 0)
                {
                    var bit = (byte)(normalTargets & -normalTargets);
                    normalTargets &= (byte)~bit;
                    var parent1HasAttack = (parent1Mask & bit) != 0;
                    var parent2HasAttack = (parent2Mask & bit) != 0;
                    var normal = CreateChoice(
                        firstMask,
                        secondMask,
                        AttackInheritanceMode.Normal,
                        parent1HasAttack ? bit : (byte)0,
                        parent2HasAttack ? bit : (byte)0,
                        (byte)(innateMask | bit)
                    );
                    if (normal is AttackCompositionChoice normalChoice)
                        yield return normalChoice;
                }

                if (settings.SpecialCakePolicy == SpecialCakePolicy.Never || !outcome.Allows(AttackInheritanceMode.InheritAll))
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
                        firstMask,
                        secondMask,
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
            byte firstMask,
            byte secondMask,
            AttackInheritanceMode mode,
            byte parent1TargetMask,
            byte parent2TargetMask,
            byte childMask
        )
        {
            if (!outcome.Allows(mode))
                return null;
            if (
                mode == AttackInheritanceMode.InheritAll &&
                !AttackProfileComposer.AllowsSpecialCake(settings.SpecialCakePolicy, innateMask, (byte)(parent1TargetMask | parent2TargetMask))
            )
                return null;

            return new(firstMask, secondMask, mode, parent1TargetMask, parent2TargetMask, childMask);
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
