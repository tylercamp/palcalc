using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
using PalCalc.Solver.Processing.Attacks;
using PalCalc.Solver.Processing.Search;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Tests.Processing;

[TestClass]
public class CandidateExpanderTests
{
    [DataTestMethod]
    [DataRow(PalGender.MALE, PalGender.FEMALE, "Wixen Noct")]
    [DataRow(PalGender.FEMALE, PalGender.MALE, "Katress Ignis")]
    public void ExpandBatch_UsesGenderSpecificBreedingResult(
        PalGender katressGender,
        PalGender wixenGender,
        string expectedChild
    )
    {
        var expansion = Expand(
            SolverTestScenario.Owned("Katress", katressGender),
            SolverTestScenario.Owned("Wixen", wixenGender),
            new PalSpecifier
            {
                Pal = expectedChild.ToPal(SolverTestScenario.DB),
            }
        );

        Assert.AreEqual(1L, expansion.Progress.NumProcessed);
        Assert.AreEqual(1, expansion.Candidates.Count);
        Assert.IsTrue(
            expansion.Candidates.All(
                candidate => candidate.Pal.Name == expectedChild
            )
        );
    }

    [TestMethod]
    public void ExpandBatch_ProducesRequiredPassiveAndIVState()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredPassives = [swift],
            IV_Attack = 90,
        };
        var expansion = Expand(
            SolverTestScenario.Owned(
                "Katress",
                PalGender.MALE,
                passives: [swift],
                ivAttack: 100
            ),
            SolverTestScenario.Owned(
                "Wixen",
                PalGender.FEMALE
            ),
            target
        );

        Assert.AreEqual(1, expansion.Candidates.Count);
        Assert.IsTrue(
            expansion.Candidates.All(
                candidate =>
                    candidate.EffectivePassives.Contains(swift) &&
                    candidate.IVs.Attack.IsRelevant &&
                    candidate.IVs.Attack.Satisfies(90)
            )
        );
    }

    [TestMethod]
    public void ExpandBatch_TreatsKnownZeroIVAsRealValue()
    {
        var expansion = Expand(
            SolverTestScenario.Owned("Katress", PalGender.MALE, ivHp: 0),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE, ivHp: 98),
            new PalSpecifier
            {
                Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            }
        );

        var childHP = expansion.Candidates.Single().IVs.HP;
        Assert.AreEqual(0, childHP.Min);
        Assert.AreEqual(98, childHP.Max);
    }

    [TestMethod]
    public void ExpandBatch_ComposesAttackProfilesWithoutEmbeddingAttackEffort()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var targetAttack = InheritableAttackNotInnateTo(child);
        var otherAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack
        );
        var target = new PalSpecifier
        {
            Pal = child,
            RequiredAttacks = [targetAttack],
        };

        var candidate = (BredPalReference)Expand(
            WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE), targetAttack),
            WithAttack(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), otherAttack),
            target
        ).Candidates.Single();

        var inherited = candidate.AttackProfile.Entries.Single(entry => entry.LearnedTargetMask == 1);
        Assert.IsTrue(candidate.AttackProfile.Entries.Count > 1);
        Assert.AreEqual(0, inherited.TotalSpecialCakes);
        Assert.AreEqual(
            (int)Math.Ceiling(1f / (candidate.PassivesProbability * candidate.IVsProbability)),
            candidate.AvgRequiredBreedings
        );

        var gendered = (BredPalReference)candidate.WithGuaranteedGender(SolverTestScenario.DB, PalGender.MALE, false);
        Assert.AreEqual(candidate.AttackProfile, gendered.AttackProfile);
    }

    [TestMethod]
    public void ExpandBatch_MissingTargetStillEmitsRoutingChild()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var targetAttack = InheritableAttackNotInnateTo(child);
        var otherAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack
        );

        var expansion = Expand(
            WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE), otherAttack),
            WithAttack(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), otherAttack),
            new PalSpecifier { Pal = child, RequiredAttacks = [targetAttack] }
        );
        var candidate = (BredPalReference)expansion.Candidates.Single();

        Assert.AreEqual(0, candidate.AttackProfile.Entries.Single().LearnedTargetMask);
    }

    [TestMethod]
    public void ExpandBatch_UsesGenderResolvedCompositeAttack()
    {
        var child = "Katress Ignis".ToPal(SolverTestScenario.DB);
        var targetAttack = InheritableAttackNotInnateTo(child);
        var otherAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack
        );
        var neutral = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit);
        var target = new PalSpecifier { Pal = child, RequiredAttacks = [targetAttack] };
        var male = WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE), otherAttack);
        var female = WithAttack(SolverTestScenario.Owned("Katress", PalGender.FEMALE), targetAttack);
        var wixen = WithAttack(SolverTestScenario.Owned("Wixen", PalGender.MALE), neutral);
        var configuredSolver = SolverTestScenario.Solver([male, female, wixen], maxSpecialCakes: 0, maxBreedingSteps: 1, maxSolverIterations: 1);
        var attackTargets = new AttackTargetContext(target, configuredSolver.Settings.DB);
        var composite = new CompositeOwnedPalReference(
            ReferenceFor(male, target, attackTargets),
            ReferenceFor(female, target, attackTargets)
        );

        var candidate = (BredPalReference)Expand(
            composite,
            ReferenceFor(wixen, target, attackTargets),
            target,
            configuredSolver.Settings
        ).Candidates.Single();

        Assert.IsTrue(candidate.AttackProfile.Contains(1));
        Assert.AreSame(
            female,
            new[] { candidate.Parent1, candidate.Parent2 }
                .OfType<OwnedPalReference>()
                .Single(parent => parent.Pal == female.Pal)
                .UnderlyingInstance
        );
    }

    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(1, true)]
    public void ExpandBatch_RetainsCakeOnlyCandidatesWithinBudget(int cakeLimit, bool expected)
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var targetAttack = InheritableAttackNotInnateTo(child);
        var otherAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack && !child.Level1AttackInternalIds.Contains(attack.InternalName));
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);
        var first = WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE, [swift]), targetAttack);
        var second = WithAttack(SolverTestScenario.Owned("Wixen", PalGender.FEMALE, [runner]), otherAttack);
        var target = new PalSpecifier
        {
            Pal = child,
            RequiredPassives = [swift, runner],
            RequiredAttacks = [targetAttack, otherAttack],
        };
        var settings = SolverTestScenario.Solver(
            [first, second], maxSpecialCakes: cakeLimit, maxBredIrrelevantPassives: 0
        ).Settings;
        var attackTargets = new AttackTargetContext(target, settings.DB);
        // Ordinary breeding can inherit only one passive; the override can retain both.
        var mechanics = new BreedingMechanics(
            SolverTestScenario.DB.BreedingMechanics.IVInheritanceWeights,
            new() { [1] = 1 },
            new() { [0] = 1 }
        );

        var candidates = Expand(
            ReferenceFor(first, target, attackTargets),
            ReferenceFor(second, target, attackTargets),
            target, settings, mechanics
        ).Candidates;

        Assert.AreEqual(expected ? 1 : 0, candidates.Count);
        if (!expected) return;

        var candidate = (BredPalReference)candidates.Single();
        var entry = candidate.AttackProfile.Entries.Single();
        Assert.AreEqual(1f, candidate.PassivesProbability);
        Assert.AreEqual(BreedingMethod.SpecialCake, candidate.Outcome.Method);
        Assert.AreEqual(4, candidate.EffectivePassives.Count);
        Assert.AreEqual(1, candidate.AvgRequiredBreedings);
        Assert.AreEqual(new AttackProfileEntry(3, 1), entry);
        var result = (BredPalReference)MaterializeSingle(new AttackResultMaterializer(attackTargets, settings), candidate, entry);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, result.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(1, result.MaterializedAttackInheritance.SpecialCakes);
        Assert.AreEqual(candidate.Outcome,
            ((BredPalReference)candidate.WithGuaranteedGender(settings.DB, PalGender.MALE, false)).Outcome);

        var mate = SolverTestScenario.Owned(child.Name, PalGender.FEMALE);
        var next = Expand(candidate, ReferenceFor(mate, target, attackTargets), target, settings)
            .Candidates.OfType<BredPalReference>()
            .Single(reference => reference.Outcome.Method == BreedingMethod.Ordinary);
        var expectedProbability = PalCalc.Solver.Probabilities.Passives.ProbabilityInheritedTargetPassives(
            settings.DB.BreedingMechanics, candidate.ActualPassives, [swift, runner], 2
        );
        var cleanProbability = PalCalc.Solver.Probabilities.Passives.ProbabilityInheritedTargetPassives(
            settings.DB.BreedingMechanics, [swift, runner], [swift, runner], 2
        );
        Assert.AreEqual(expectedProbability, next.PassivesProbability);
        Assert.IsTrue(next.PassivesProbability < cleanProbability);
    }

    private static ExpansionResult Expand(
        PalInstance first,
        PalInstance second,
        PalSpecifier target
    )
    {
        var configuredSolver = SolverTestScenario.Solver(
            [first, second],
            maxSpecialCakes: 0,
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );
        var settings = configuredSolver.Settings;
        var attackTargets = new AttackTargetContext(target, settings.DB);
        return Expand(
            ReferenceFor(first, target, attackTargets),
            ReferenceFor(second, target, attackTargets),
            target,
            settings
        );
    }

    private static ExpansionResult Expand(
        IPalReference firstReference,
        IPalReference secondReference,
        PalSpecifier target,
        BreedingSolverSettings settings,
        BreedingMechanics? mechanics = null
    )
    {
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var attackTargets = new AttackTargetContext(target, settings.DB);
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            controller.CancellationToken,
            attackTargets: attackTargets
        );
        var frontier = new SearchFrontier(
            target,
            [firstReference, secondReference],
            maxThreads: 1,
            controller,
            selectionPolicy,
            attackTargets
        );
        var context = new CandidateExpansionContext(
            StepIndex: 0,
            Target: target,
            PreFilter: new CandidatePreFilter(
                target,
                settings.MaxEffort,
                selectionPolicy,
                frontier,
                settings.DB.PalsById.Keys,
                attackTargets,
                settings
            ),
            AttackTargets: attackTargets
        );
        var progress = new WorkBatchProgress();
        var expander = new CandidateExpander(
            controller,
            settings,
            new ObjectPoolFactory(),
            mechanics ?? settings.DB.BreedingMechanics,
            settings.BreedingDB,
            attackTargets
        );

        var candidates = expander
            .ExpandBatch(
                [(firstReference, secondReference)],
                progress,
                context
            )
            .ToList();

        return new(candidates, progress);
    }

    private static OwnedPalReference ReferenceFor(
        PalInstance instance,
        PalSpecifier target,
        AttackTargetContext? attackTargets = null
    )
    {
        return new(
            instance,
            instance.PassiveSkills.ToDedicatedPassives(
                target.DesiredPassives
            ),
            new IV_Set(
                HP: EffectiveIV(target.IV_HP, instance.IV_HP),
                Attack: EffectiveIV(
                    target.IV_Attack,
                    instance.IV_Attack
                ),
                Defense: EffectiveIV(
                    target.IV_Defense,
                    instance.IV_Defense
                )
            ),
            attackProfile: attackTargets?.IsActive == true
                ? new(
                    attackTargets.IsActive && (instance.ActiveSkills ?? []).Any(attack => !attack.CanInherit),
                    new AttackProfileEntry(
                    attackTargets.MaskOf(instance.ActiveSkills ?? []),
                    0
                    )
                )
                : AttackProfile.Inactive
        );
    }

    private static PalInstance WithAttack(PalInstance instance, ActiveSkill attack)
    {
        instance.ActiveSkills = [attack];
        instance.EquippedActiveSkills = [attack];
        return instance;
    }

    private static ActiveSkill InheritableAttackNotInnateTo(Pal pal) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && !pal.Level1AttackInternalIds.Contains(attack.InternalName)
        );

    private static IV_Value EffectiveIV(int minimum, int value) =>
        new(
            IsRelevant: minimum != 0 && value >= minimum,
            Min: value,
            Max: value
        );

    private sealed record ExpansionResult(
        List<IPalReference> Candidates,
        WorkBatchProgress Progress
    );
    private static IPalReference MaterializeSingle(
        AttackResultMaterializer materializer, IPalReference reference, AttackProfileEntry entry) =>
        materializer.Materialize(materializer.Evaluate(reference, entry.LearnedTargetMask).Single());

}
