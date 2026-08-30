using PalCalc.Model;
using PalCalc.Solver.PalReference;

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

    private static ActiveSkill NonInheritableAttackNotInnateTo(params Pal[] pals) =>
        SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit &&
            pals.All(pal => !pal.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack))
        );
}
