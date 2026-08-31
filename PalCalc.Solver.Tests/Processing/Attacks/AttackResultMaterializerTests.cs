using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackResultMaterializerTests
{
    [TestMethod]
    public void Solve_RevalidatesAndMaterializesTheGenderAdjustedProfileEntry()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var neutralAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var carrier = SolverTestScenario.Owned("Katress", PalGender.MALE);
        carrier.ActiveSkills = [requiredAttack];
        carrier.EquippedActiveSkills = [requiredAttack];
        var mate = SolverTestScenario.Owned("Wixen", PalGender.FEMALE);
        mate.ActiveSkills = [neutralAttack];
        mate.EquippedActiveSkills = [neutralAttack];

        var result = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [carrier, mate],
                maxSpecialCakes: 0,
                maxBreedingSteps: 1,
                maxSolverIterations: 1
            ),
            child.Name,
            requiredGender: PalGender.MALE,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().Single();

        var entry = result.AttackProfile.Entries.Single();

        Assert.AreEqual(PalGender.MALE, result.Gender);
        Assert.AreEqual(entry.SelfBreedings, result.AvgRequiredBreedings);
        Assert.AreEqual(entry.BreedingEffort, result.BreedingEffort);
        Assert.IsNotNull(result.MaterializedAttackInheritance);
    }

    [TestMethod]
    public void Solve_MaterializesTheSelectedAttackProfileEntryAndParentLoadouts()
    {
        var child = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var requiredAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var neutralAttack = SolverTestScenario.DB.ActiveSkills.First(attack =>
            !attack.CanInherit &&
            !child.Level1ActiveSkills(SolverTestScenario.DB).Contains(attack)
        );
        var carrier = SolverTestScenario.Owned("Katress", PalGender.MALE);
        carrier.ActiveSkills = [requiredAttack];
        carrier.EquippedActiveSkills = [requiredAttack];
        var mate = SolverTestScenario.Owned("Wixen", PalGender.FEMALE);
        mate.ActiveSkills = [neutralAttack];
        mate.EquippedActiveSkills = [neutralAttack];

        var result = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                [carrier, mate],
                maxSpecialCakes: 0,
                maxBreedingSteps: 1,
                maxSolverIterations: 1
            ),
            child.Name,
            requiredAttack: requiredAttack
        ).OfType<BredPalReference>().First();

        var entry = result.AttackProfile.Entries.Single();
        var inheritance = result.MaterializedAttackInheritance;

        Assert.IsNotNull(inheritance);
        Assert.AreEqual(entry.SelfBreedings, result.AvgRequiredBreedings);
        Assert.AreEqual(entry.BreedingEffort, result.BreedingEffort);
        Assert.AreEqual(AttackInheritanceMode.Normal, inheritance.Mode);
        Assert.AreEqual(1f, inheritance.AttackProbability);
        Assert.IsTrue(inheritance.InheritedAttacks.Contains(requiredAttack));
        Assert.IsTrue(inheritance.ChildMasteredAttacks.Contains(requiredAttack));
        Assert.IsTrue(inheritance.Parent1Loadout.Count is >= 1 and <= 3);
        Assert.IsTrue(inheritance.Parent2Loadout.Count is >= 1 and <= 3);
        Assert.IsTrue(result.Parent1.AttackProfile.Entries.Count == 1);
        Assert.IsTrue(result.Parent2.AttackProfile.Entries.Count == 1);
    }
}
