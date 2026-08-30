using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests;

[TestClass]
public class AttackInheritanceSolverTests
{
    [TestMethod]
    public void Solve_UsesOwnedMasteredAttackWithoutBreeding()
    {
        var targetPal = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(targetPal);
        var neutralAttack = NonInheritableAttackNotInnateTo(targetPal);
        var owned = WithAttacks(
            SolverTestScenario.Owned(targetPal.Name, PalGender.FEMALE),
            neutralAttack,
            requiredAttack
        );

        var results = SolverTestScenario.Solve(
            SolverTestScenario.Solver([owned], maxBreedingSteps: 0),
            targetPal.Name,
            requiredAttack: requiredAttack
        );

        var result = results.Single();
        Assert.IsInstanceOfType<OwnedPalReference>(result);
        Assert.AreSame(requiredAttack, result.ActualAttack);
        Assert.AreSame(requiredAttack, result.EffectiveAttack);
        Assert.AreEqual(0, result.NumTotalBreedingSteps);
    }

    [TestMethod]
    public void Solve_InheritsAttackWithProbabilityOneFromNeutralMate()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(child);
        var neutralAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), requiredAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutralAttack),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var result = SolverTestScenario.Solve(
            solver,
            child.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();

        Assert.AreSame(requiredAttack, result.EffectiveAttack);
        Assert.AreEqual(1f, result.AttacksProbability);
        Assert.AreEqual(
            (int)Math.Ceiling(
                1f /
                (result.PassivesProbability * result.IVsProbability * result.AttacksProbability)
            ),
            result.AvgRequiredBreedings
        );
    }

    [TestMethod]
    public void Solve_StoresDilutedAttackPassiveAndIVProbabilitiesInTheProfile()
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
        var inherited = result.AttackProfile.Entries.Single(entry => entry.MasteredTargetMask == 1);

        Assert.AreEqual(0.5f, result.AttacksProbability);
        Assert.AreEqual(expectedProfileEggs, inherited.SelfBreedings);
        Assert.AreEqual(expectedProfileEggs, result.AvgRequiredBreedings);
        Assert.AreEqual(inherited.BreedingEffort, result.BreedingEffort);
    }

    [TestMethod]
    public void Solve_TransfersAttackThroughIntermediateSpecies()
    {
        var intermediate = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var target = "Azurobe".ToPal(SolverTestScenario.DB);
        var requiredAttack = InheritableAttackNotInnateTo(intermediate, target);
        var neutralAttack = NonInheritableAttackNotInnateTo(intermediate, target);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), requiredAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutralAttack),
                WithAttacks(SolverTestScenario.Owned("Kingpaca", PalGender.FEMALE), neutralAttack),
            ],
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

        Assert.AreSame(requiredAttack, bredIntermediate.EffectiveAttack);
        Assert.AreSame(requiredAttack, result.EffectiveAttack);
    }

    [TestMethod]
    public void Solve_GainsRequiredLevelOneAttackWithoutParentCarrier()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var neutralAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), neutralAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutralAttack),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var result = SolverTestScenario.Solve(
            solver,
            child.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();

        Assert.AreSame(requiredAttack, result.EffectiveAttack);
        Assert.AreEqual(1f, result.AttacksProbability);
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
                maxBreedingSteps: 1,
                maxSolverIterations: 1
            ),
            target.Name,
            requiredAttack: requiredAttack
        );
        var ownedResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [WithAttacks(SolverTestScenario.Owned(target.Name, PalGender.FEMALE), requiredAttack)],
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
        var neutralAttack = NonInheritableAttackNotInnateTo(intermediate, target);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), neutralAttack),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutralAttack),
                WithAttacks(SolverTestScenario.Owned("Kingpaca", PalGender.FEMALE), requiredAttack),
            ],
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

        Assert.AreNotSame(requiredAttack, bredIntermediate.EffectiveAttack);
        Assert.AreSame(requiredAttack, result.EffectiveAttack);
    }

    [TestMethod]
    public void Solve_TwoNonInnateAttacksRequireCakeAndRespectItsLimit()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var attacks = InheritableAttacksNotInnateTo(2, child);
        var neutralAttack = NonInheritableAttackNotInnateTo(child);

        SolverTestScenario.ConfiguredSolver Configure(int? maxSpecialCakes) => SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), attacks[0]),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), attacks[1]),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxSpecialCakes: maxSpecialCakes
        );

        Assert.AreEqual(0, SolverTestScenario.Solve(Configure(0), child.Name, attacks).Count);
        Assert.AreEqual(0, SolverTestScenario.Solve(Configure(0), child.Name, [attacks[0], attacks[0], attacks[1]]).Count);
        Assert.AreEqual(0, SolverTestScenario.Solve(Configure(1), child.Name, attacks).Count);

        var limited = SolverTestScenario.Solve(Configure(100), child.Name, attacks)
            .OfType<BredPalReference>()
            .Single();
        var duplicateTargets = SolverTestScenario.Solve(
                Configure(100),
                child.Name,
                [attacks[0], attacks[0], attacks[1]]
            )
            .OfType<BredPalReference>()
            .Single();
        var unlimited = SolverTestScenario.Solve(Configure(null), child.Name, attacks)
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(0b11, limited.AttackProfile.Entries.Single().MasteredTargetMask);
        Assert.IsTrue(limited.MaterializedAttackInheritance.SpecialCakes > 1);
        Assert.AreEqual(AttackInheritanceMode.InheritAll, limited.MaterializedAttackInheritance.Mode);
        Assert.AreEqual(0b11, duplicateTargets.AttackProfile.Entries.Single().MasteredTargetMask);
        Assert.AreEqual(0b11, unlimited.AttackProfile.Entries.Single().MasteredTargetMask);
    }

    [TestMethod]
    public void Solve_CakeTransfersAtMostThreeTargetAttacksPerParent()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var attacks = InheritableAttacksNotInnateTo(6, child);
        var neutralAttack = NonInheritableAttackNotInnateTo(child);

        SolverTestScenario.ConfiguredSolver Configure(IEnumerable<ActiveSkill> first, IEnumerable<ActiveSkill> second) =>
            SolverTestScenario.Solver(
                [
                    WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), first.First(), first.Skip(1).ToArray()),
                    WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), second.First(), second.Skip(1).ToArray()),
                ],
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxSpecialCakes: 100
            );

        var split = SolverTestScenario.Solve(Configure(attacks[..3], attacks[3..]), child.Name, attacks)
            .OfType<BredPalReference>()
            .Single();
        var concentrated = SolverTestScenario.Solve(Configure(attacks[..4], [neutralAttack]), child.Name, attacks[..4]);

        Assert.AreEqual(0b11_1111, split.AttackProfile.Entries.Single().MasteredTargetMask);
        Assert.AreEqual(0, concentrated.Count);
    }

    [TestMethod]
    public void Solve_CakeAddsInheritedAttacksToTheChildsInnateTargets()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var innate = child.Level1ActiveSkills(SolverTestScenario.DB).First();
        var inherited = InheritableAttacksNotInnateTo(3, child);
        var neutralAttack = NonInheritableAttackNotInnateTo(child);
        var solver = SolverTestScenario.Solver(
            [
                WithAttacks(SolverTestScenario.Owned("Katress", PalGender.MALE), inherited[0], inherited[1], inherited[2]),
                WithAttacks(SolverTestScenario.Owned("Wixen", PalGender.FEMALE), neutralAttack),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxSpecialCakes: 100
        );

        var result = SolverTestScenario.Solve(solver, child.Name, [innate, .. inherited])
            .OfType<BredPalReference>()
            .Single();

        Assert.AreEqual(4, result.MaterializedAttackInheritance.ChildMasteredAttacks.Count);
        Assert.AreEqual(3, result.MaterializedAttackInheritance.InheritedAttacks.Count);
    }

    [TestMethod]
    public void Solve_WithoutRequiredAttackRetainsDeterministicOneStepResults()
    {
        static IReadOnlyList<SolverTestScenario.ResultSignature> Run() =>
            SolverTestScenario.Signatures(
                SolverTestScenario.Solve(
                    SolverTestScenario.Solver(
                        [
                            SolverTestScenario.Owned("Katress", PalGender.MALE),
                            SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
                        ],
                        maxBreedingSteps: 1,
                        maxSolverIterations: 1
                    ),
                    "Wixen Noct"
                )
            );

        var first = Run();
        var second = Run();

        Assert.IsTrue(first.Count > 0);
        CollectionAssert.AreEqual(first.ToArray(), second.ToArray());
    }

    private static PalInstance WithAttacks(
        PalInstance pal,
        ActiveSkill equippedAttack,
        params ActiveSkill[] otherMasteredAttacks
    )
    {
        pal.ActiveSkills = [equippedAttack, .. otherMasteredAttacks];
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
