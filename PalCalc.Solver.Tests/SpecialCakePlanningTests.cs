using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests;

[TestClass]
public class SpecialCakePlanningTests
{
    private static readonly Pal Child = "Wixen Noct".ToPal(SolverTestScenario.DB);
    private static readonly ActiveSkill[] Attacks = SolverTestScenario.DB.ActiveSkills
        .Where(attack => attack.CanInherit && !Child.Level1AttackInternalIds.Contains(attack.InternalName))
        .Take(2).ToArray();

    [DataTestMethod]
    [DataRow(0, 0, false)]
    [DataRow(0, 1, true)]
    [DataRow(1, 1, true)]
    [DataRow(4, 1, true)]
    public void Solve_AttackRequiredCakeBypassesOrdinaryPassiveVariantLimit(int variantLimit, int cakeLimit, bool expected)
    {
        // Both parents have unwanted passives: the old pair-wide zero-limit shortcut
        // must not suppress the mandatory cake outcome.
        var unwanted = SolverTestScenario.DB.PassiveSkills.First();
        var first = Parent("Katress", PalGender.MALE, Attacks[0], [unwanted]);
        var second = Parent("Wixen", PalGender.FEMALE, Attacks[1], [unwanted]);
        var results = SolverTestScenario.Solve(
            SolverTestScenario.Solver([first, second], maxSpecialCakes: cakeLimit,
                maxBredIrrelevantPassives: variantLimit, maxBreedingSteps: 1, maxSolverIterations: 1),
            Child.Name, Attacks
        );

        Assert.AreEqual(expected ? 1 : 0, results.Count);
        if (!expected) return;
        var result = (BredPalReference)results.Single();
        Assert.AreEqual(BreedingMethod.SpecialCake, result.Outcome.Method);
        Assert.AreEqual(4, result.ActualPassives.Count);
        Assert.AreEqual(1f, result.PassivesProbability);
        Assert.AreEqual(1, result.MaterializedAttackInheritance.SpecialCakes);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Solve_OneNonInnateAttackDoesNotIntroduceCakes(bool includeInnateTarget)
    {
        var targets = includeInnateTarget
            ? new[] { Attacks[0], Child.Level1ActiveSkills(SolverTestScenario.DB).First() }
            : new[] { Attacks[0] };
        var results = SolverTestScenario.Solve(
            SolverTestScenario.Solver([
                Parent("Katress", PalGender.MALE, Attacks[0], []),
                Parent("Wixen", PalGender.FEMALE, Attacks[1], []),
            ], maxSpecialCakes: 100, maxBredIrrelevantPassives: 0,
                maxBreedingSteps: 1, maxSolverIterations: 1),
            Child.Name, targets
        );

        var result = (BredPalReference)results.Single();
        Assert.AreEqual(BreedingMethod.Ordinary, result.Outcome.Method);
        Assert.AreEqual(0, result.ActualPassives.Count);
        Assert.AreEqual(0, result.MaterializedAttackInheritance.SpecialCakes);
    }

    private static PalInstance Parent(string name, PalGender gender, ActiveSkill attack, PassiveSkill[] passives)
    {
        var instance = SolverTestScenario.Owned(name, gender, passives);
        instance.ActiveSkills = [attack];
        instance.EquippedActiveSkills = [attack];
        return instance;
    }
}
