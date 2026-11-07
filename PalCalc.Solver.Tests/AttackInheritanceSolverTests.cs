using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests;

[TestClass]
public class AttackInheritanceSolverTests
{
    [TestMethod]
    public void Solve_UsesOwnedLearnedAttackWithoutBreeding()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(targetPal);
        var noopAttack = NonInheritableAttackNotInnateTo(targetPal);
        var owned = WithAttacks(
            SolverTestScenario.Owned(targetPal.Name, PalGender.FEMALE),
            noopAttack,
            requiredAttack
        );

        var results = SolverTestScenario.Solve(
            SolverTestScenario.Solver([owned], specialCakePolicy: SpecialCakePolicy.Never, maxBreedingSteps: 0),
            targetPal.Name,
            requiredAttack: requiredAttack
        );

        var result = results.Single();
        Assert.IsInstanceOfType<OwnedPalReference>(result);
        var ownedResult = (OwnedPalReference)result;
        CollectionAssert.Contains(ownedResult.UnderlyingInstance.ActiveSkills, requiredAttack);
        Assert.AreEqual(0, result.NumTotalBreedingSteps);
    }

    [TestMethod]
    public void Solve_InheritsAttackWithProbabilityOneFromNeutralMate()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(child);
        var noopAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), requiredAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), noopAttack),
            ],
            specialCakePolicy: SpecialCakePolicy.Never,
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var result = SolverTestScenario.Solve(
            solver,
            child.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();

        CollectionAssert.Contains(result.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
        Assert.AreEqual(1f, result.MaterializedAttackInheritance.AttackProbability);
        Assert.AreEqual(
            (int)Math.Ceiling(
                1f /
                (result.PassivesProbability * result.IVsProbability * result.MaterializedAttackInheritance.AttackProbability)
            ),
            result.AvgRequiredBreedings
        );
    }

    [TestMethod]
    public void Solve_ReconstructsCombinedAttackPassiveAndIVProbability()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(child);
        var irrelevantAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit &&
            attack != requiredAttack &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(
                    SolverTestScenario.Owned(
                        "Katress",
                        PalGender.MALE,
                        passives: [swift],
                        ivAttack: 100
                    ),
                    requiredAttack
                ),
                WithAttacks(
                    SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
                    irrelevantAttack
                ),
            ],
            specialCakePolicy: SpecialCakePolicy.Never,
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var result = SolverTestScenario.Solve(
            solver,
            child.Name,
            requiredPassives: [swift],
            ivAttack: 90,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();
        var expectedProfileEggs = (int)Math.Ceiling(
            1f /
            (result.PassivesProbability * result.IVsProbability * 0.5f)
        );
        Assert.AreEqual((byte)1, result.AttackProfile.Masks.Single());

        Assert.AreEqual(0.5f, result.MaterializedAttackInheritance.AttackProbability);
        Assert.AreEqual(0, result.ExpectedSpecialCakes);
        Assert.AreEqual(expectedProfileEggs, result.AvgRequiredBreedings);
        Assert.IsTrue(result.BreedingEffort > TimeSpan.Zero);
    }

    [TestMethod]
    public void Solve_TransfersAttackThroughIntermediateSpecies()
    {
        var intermediate = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var target = "Azurobe".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(intermediate, target);
        var noopAttack = NonInheritableAttackNotInnateTo(intermediate, target);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), requiredAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), noopAttack),
                WithAttacks(SolverTestScenario.Owned("Kingpaca", PalGender.FEMALE), noopAttack),
            ],
            specialCakePolicy: SpecialCakePolicy.Never,
            maxBreedingSteps: 2,
            maxSolverIterations: 2
        );

        var result = SolverTestScenario.Solve(
            solver,
            target.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First(r => r.NumTotalBreedingSteps == 2);
        var bredIntermediate = new[] { result.Parent1, result.Parent2 }
            .OfType<BredPalReference>()
            .Single(parent => parent.Pal == intermediate);

        CollectionAssert.Contains(bredIntermediate.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
        CollectionAssert.Contains(result.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
    }

    [TestMethod]
    public void Solve_GainsRequiredLevelOneAttackWithoutParentCarrier()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var noopAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), noopAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), noopAttack),
            ],
            specialCakePolicy: SpecialCakePolicy.Never,
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var result = SolverTestScenario.Solve(
            solver,
            child.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();

        CollectionAssert.Contains(result.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
        Assert.AreEqual(1f, result.MaterializedAttackInheritance.AttackProbability);
    }

    [TestMethod]
    public void Solve_DoesNotTransferNonInheritableAttackButAcceptsOwnedTarget()
    {
        var target = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = NonInheritableAttackNotInnateTo(target);
        var carrier = WithAttacks(
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            requiredAttack
        );
        var mate = WithAttacks(
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            requiredAttack
        );

        var inheritedResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [carrier, mate],
                specialCakePolicy: SpecialCakePolicy.Never,
                maxBreedingSteps: 1,
                maxSolverIterations: 1
            ),
            target.Name,
            requiredAttack: requiredAttack
        );
        var ownedResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [WithAttacks(SolverTestScenario.Owned(target.Name, PalGender.FEMALE), requiredAttack)],
                specialCakePolicy: SpecialCakePolicy.Never,
                maxBreedingSteps: 0
            ),
            target.Name,
            requiredAttack: requiredAttack
        );

        Assert.AreEqual(0, inheritedResults.Count);
        Assert.IsInstanceOfType<OwnedPalReference>(ownedResults.Single());
    }

    [TestMethod]
    public void Solve_CanIntroduceAttackAfterIrrelevantIntermediate()
    {
        var intermediate = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var target = "Azurobe".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(intermediate, target);
        var noopAttack = NonInheritableAttackNotInnateTo(intermediate, target);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), noopAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), noopAttack),
                WithAttacks(SolverTestScenario.Owned("Kingpaca", PalGender.FEMALE), requiredAttack),
            ],
            specialCakePolicy: SpecialCakePolicy.Never,
            maxBreedingSteps: 2,
            maxSolverIterations: 2
        );

        var result = SolverTestScenario.Solve(
            solver,
            target.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First(r => r.NumTotalBreedingSteps == 2);
        var bredIntermediate = new[] { result.Parent1, result.Parent2 }
            .OfType<BredPalReference>()
            .Single(parent => parent.Pal == intermediate);

        CollectionAssert.DoesNotContain(bredIntermediate.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
        CollectionAssert.Contains(result.MaterializedAttackInheritance.ChildLearnedAttacks.ToList(), requiredAttack);
    }

    [TestMethod]
    public void Solve_TwoNonInnateAttacksRequireCakeAndNormalizeDuplicateTargets()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var attacks = InheritableAttacksNotInnateTo(2, child);

        SolverTestScenario.ConfiguredSolver Configure(SpecialCakePolicy policy) => SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE, ivHp: 100), attacks[0]),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), attacks[1]),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxBredIrrelevantPassives: 4,
            specialCakePolicy: policy
        );

        Assert.AreEqual(0, SolverTestScenario.Solve(Configure(SpecialCakePolicy.Never), child.Name, attacks, ivHp: 90).Count);
        Assert.AreEqual(0, SolverTestScenario.Solve(Configure(SpecialCakePolicy.Never), child.Name, [attacks[0], attacks[0], attacks[1]], ivHp: 90).Count);

        var attackOnly = SolverTestScenario.Solve(Configure(SpecialCakePolicy.AttackInheritanceOnly), child.Name, attacks, ivHp: 90)
            .OfType<BredPalReference>()
            .Single();
        var duplicateTargets = SolverTestScenario.Solve(
                Configure(SpecialCakePolicy.AttackInheritanceOnly),
                child.Name,
                [attacks[0], attacks[0], attacks[1]], ivHp: 90
            )
            .OfType<BredPalReference>()
            .Single();
        var broadPolicy = SolverTestScenario.Solve(Configure(SpecialCakePolicy.AttacksAndPassives), child.Name, attacks, ivHp: 90)
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(0b11, attackOnly.AttackProfile.Masks.Single());
        Assert.AreEqual(1f / 3f, attackOnly.IVsProbability, 0.000001f);
        Assert.AreEqual(4, attackOnly.EffectivePassives.Count);
        Assert.AreEqual(1f, attackOnly.PassivesProbability);
        Assert.IsTrue(attackOnly.SelfSpecialCakes > 1);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, attackOnly.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(0b11, duplicateTargets.AttackProfile.Masks.Single());
        Assert.AreEqual(0b11, broadPolicy.AttackProfile.Masks.Single());
    }

    [TestMethod]
    public void Solve_CakeTargetsFourEvenWithAnUndersizedParentPool()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var attacks = InheritableAttacksNotInnateTo(2, child);
        var passives = SolverTestScenario.DB.PassiveSkills.Take(4).ToArray();

        SolverTestScenario.ConfiguredSolver Configure(bool includeIrrelevantPassives, int maxIrrelevant = 1) =>
            SolverTestScenario.Solver(
                [
                    WithAttacks(
                        SolverTestScenario.Owned(
                            "Katress",
                            PalGender.MALE,
                            includeIrrelevantPassives ? passives[..3] : passives[..1]
                        ),
                        attacks[0]
                    ),
                    WithAttacks(
                        SolverTestScenario.Owned(
                            "Wixen",
                            PalGender.FEMALE,
                            includeIrrelevantPassives ? passives[3..] : passives[1..2]
                        ),
                        attacks[1]
                    ),
                ],
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxBredIrrelevantPassives: maxIrrelevant,
                specialCakePolicy: SpecialCakePolicy.AttackInheritanceOnly
            );

        var dirty = SolverTestScenario.Solve(
            Configure(includeIrrelevantPassives: true),
            child.Name,
            attacks,
            requiredPassives: passives[..2]
        );
        var clean = SolverTestScenario.Solve(
                Configure(includeIrrelevantPassives: false, maxIrrelevant: 2),
                child.Name,
                attacks,
                requiredPassives: passives[..2]
            )
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(4, dirty.OfType<BredPalReference>().Single().EffectivePassives.Count);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, clean.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(
            1f,
            clean.PassivesProbability,
            0.0001f
        );
    }

    [TestMethod]
    public void Solve_CakeTransfersAtMostThreeTargetAttacksPerParent()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var attacks = InheritableAttacksNotInnateTo(6, child);
        var noopAttack = NonInheritableAttackNotInnateTo(child);

        SolverTestScenario.ConfiguredSolver Configure(IEnumerable<ActiveSkill> first, IEnumerable<ActiveSkill> second) =>
            SolverTestScenario.Solver(
                [
                    WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), first.First(), first.Skip(1).ToArray()),
                    WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), second.First(), second.Skip(1).ToArray()),
                ],
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxBredIrrelevantPassives: 4,
                specialCakePolicy: SpecialCakePolicy.AttackInheritanceOnly
            );

        var split = SolverTestScenario.Solve(Configure(attacks[..3], attacks[3..]), child.Name, attacks)
            .OfType<BredPalReference>()
            .Single();
        var concentrated = SolverTestScenario.Solve(Configure(attacks[..4], [noopAttack]), child.Name, attacks[..4]);

        Assert.AreEqual(0b11_1111, split.AttackProfile.Masks.Single());
        Assert.AreEqual(0, concentrated.Count);
    }

    [TestMethod]
    public void Solve_CakeAddsInheritedAttacksToTheChildsInnateTargets()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var innate = child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var inherited = InheritableAttacksNotInnateTo(3, child);
        var noopAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), inherited[0], inherited[1], inherited[2]),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), noopAttack),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxBredIrrelevantPassives: 4,
            specialCakePolicy: SpecialCakePolicy.AttackInheritanceOnly
        );

        var result = SolverTestScenario.Solve(solver, child.Name, [innate, .. inherited])
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(4, result.MaterializedAttackInheritance.ChildLearnedAttacks.Count);
        Assert.AreEqual(3, result.MaterializedAttackInheritance.InheritedAttacks.Count);
    }

    private static PalInstance WithAttacks(
        PalInstance pal,
        ActiveSkill equippedAttack,
        params ActiveSkill[] otherLearnedAttacks
    )
    {
        pal.ActiveSkills = [equippedAttack, .. otherLearnedAttacks];
        pal.EquippedActiveSkills = [equippedAttack];
        return pal;
    }

    private static ActiveSkill InheritableAttackNotInnateTo(params Pal[] pals) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit &&
            pals.All(pal => !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack))
        );

    private static ActiveSkill[] InheritableAttacksNotInnateTo(int count, params Pal[] pals) =>
        SolverTestScenario.DB.ActiveSkills
            .Where(attack => attack.CanInherit)
            .Where(attack => pals.All(pal => !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)))
            .Take(count)
            .ToArray();

    private static ActiveSkill NonInheritableAttackNotInnateTo(params Pal[] pals) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit &&
            pals.All(pal => !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack))
        );
}
