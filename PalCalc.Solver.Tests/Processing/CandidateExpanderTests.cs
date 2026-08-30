using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing;
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
    public void ExpandBatch_AppliesAttackProbabilityAndState()
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

        Assert.AreSame(targetAttack, candidate.ActualAttack);
        Assert.AreSame(targetAttack, candidate.EffectiveAttack);
        Assert.AreEqual(0.5f, candidate.AttacksProbability);
        Assert.AreEqual(
            (int)Math.Ceiling(1f / (candidate.PassivesProbability * candidate.IVsProbability * candidate.AttacksProbability)),
            candidate.AvgRequiredBreedings
        );

        var gendered = (BredPalReference)candidate.WithGuaranteedGender(SolverTestScenario.DB, PalGender.MALE, false);
        Assert.AreSame(candidate.ActualAttack, gendered.ActualAttack);
        Assert.AreSame(candidate.EffectiveAttack, gendered.EffectiveAttack);
        Assert.AreEqual(candidate.AttacksProbability, gendered.AttacksProbability);
        Assert.AreEqual(
            (int)Math.Ceiling(
                candidate.AvgRequiredBreedings /
                SolverTestScenario.DB.BreedingGenderProbability[candidate.Pal][PalGender.MALE]
            ),
            gendered.AvgRequiredBreedings
        );
        Assert.IsTrue(gendered.AvgRequiredBreedings > candidate.AvgRequiredBreedings);
    }

    [TestMethod]
    public void ExpandBatch_LevelOneTargetIsGuaranteed()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var targetAttack = child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var otherAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit && attack != targetAttack
        );

        var candidate = (BredPalReference)Expand(
            WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE), otherAttack),
            WithAttack(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), otherAttack),
            new PalSpecifier { Pal = child, RequiredAttacks = [targetAttack] }
        ).Candidates.Single();

        Assert.AreSame(targetAttack, candidate.EffectiveAttack);
        Assert.AreEqual(1, candidate.AttacksProbability);
    }

    [TestMethod]
    public void ExpandBatch_NonInheritableFillerDoesNotDiluteTarget()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var targetAttack = InheritableAttackNotInnateTo(child);
        var neutral = SolverTestScenario.DB.ActiveSkills.First(attack => !attack.CanInherit);

        var candidate = (BredPalReference)Expand(
            WithAttack(SolverTestScenario.Owned("Katress", PalGender.MALE), targetAttack),
            WithAttack(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutral),
            new PalSpecifier { Pal = child, RequiredAttacks = [targetAttack] }
        ).Candidates.Single();

        Assert.AreSame(targetAttack, candidate.EffectiveAttack);
        Assert.AreEqual(1, candidate.AttacksProbability);
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

        Assert.AreNotSame(targetAttack, candidate.EffectiveAttack);
        Assert.AreEqual(1, candidate.AttacksProbability);
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
        var configuredSolver = SolverTestScenario.Solver([male, female, wixen], maxBreedingSteps: 1, maxSolverIterations: 1);
        var composite = new CompositeOwnedPalReference(ReferenceFor(male, target), ReferenceFor(female, target));

        var candidate = (BredPalReference)Expand(
            composite,
            ReferenceFor(wixen, target),
            target,
            configuredSolver.Settings
        ).Candidates.Single();

        Assert.AreSame(targetAttack, candidate.EffectiveAttack);
        Assert.AreEqual(1, candidate.AttacksProbability);
        Assert.AreSame(
            female,
            new[] { candidate.Parent1, candidate.Parent2 }
                .OfType<OwnedPalReference>()
                .Single(parent => parent.Pal == female.Pal)
                .UnderlyingInstance
        );
    }

    private static ExpansionResult Expand(
        PalInstance first,
        PalInstance second,
        PalSpecifier target
    )
    {
        var configuredSolver = SolverTestScenario.Solver(
            [first, second],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );
        var settings = configuredSolver.Settings;
        return Expand(
            ReferenceFor(first, target),
            ReferenceFor(second, target),
            target,
            settings
        );
    }

    private static ExpansionResult Expand(
        IPalReference firstReference,
        IPalReference secondReference,
        PalSpecifier target,
        BreedingSolverSettings settings
    )
    {
        var controller = new SolverStateController(
            CancellationToken.None
        );
        var selectionPolicy = new DefaultCandidateSelectionPolicy(
            ResultPruningPolicy.Default,
            controller.CancellationToken
        );
        var frontier = new SearchFrontier(
            target,
            [firstReference, secondReference],
            maxThreads: 1,
            controller,
            selectionPolicy
        );
        var context = new CandidateExpansionContext(
            StepIndex: 0,
            Target: target,
            PreFilter: new CandidatePreFilter(
                target,
                settings.MaxEffort,
                selectionPolicy,
                frontier,
                settings.DB.PalsById.Keys
            )
        );
        var progress = new WorkBatchProgress();
        var expander = new CandidateExpander(
            controller,
            settings,
            new ObjectPoolFactory(),
            settings.DB.BreedingMechanics,
            settings.BreedingDB
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
        PalSpecifier target
    )
    {
        var actualAttack = instance.EquippedActiveSkills.FirstOrDefault();
        var effectiveAttack = actualAttack == target.RequiredAttack
            ? actualAttack
            : actualAttack?.CanInherit == true
                ? new RandomActiveSkill()
                : null;

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
            actualAttack,
            effectiveAttack
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
}
