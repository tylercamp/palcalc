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
    [DataRow(0, SpecialCakePolicy.Never, false)]
    [DataRow(0, SpecialCakePolicy.AttackInheritanceOnly, true)]
    [DataRow(0, SpecialCakePolicy.AttacksAndPassives, true)]
    [DataRow(1, SpecialCakePolicy.AttackInheritanceOnly, true)]
    [DataRow(4, SpecialCakePolicy.AttackInheritanceOnly, true)]
    public void Solve_AttackRequiredCakeBypassesOrdinaryPassiveVariantLimit(int variantLimit, SpecialCakePolicy policy, bool expected)
    {
        // Both parents have unwanted passives: the old pair-wide zero-limit shortcut
        // must not suppress the mandatory cake outcome.
        var unwanted = SolverTestScenario.DB.PassiveSkills.First();
        var first = Parent("Katress", PalGender.MALE, Attacks[0], [unwanted]);
        var second = Parent("Wixen", PalGender.FEMALE, Attacks[1], [unwanted]);
        var results = SolverTestScenario.Solve(
            SolverTestScenario.Solver([first, second], specialCakePolicy: policy,
                maxBredIrrelevantPassives: variantLimit, maxBreedingSteps: 1, maxSolverIterations: 1),
            Child.Name, Attacks
        );

        Assert.AreEqual(expected ? 1 : 0, results.Count);
        if (!expected) return;
        var result = (BredPalReference)results.Single();
        Assert.AreEqual(BreedingMethod.SpecialCake, result.Outcome.Method);
        Assert.AreEqual(4, result.ActualPassives.Count);
        Assert.AreEqual(1f, result.PassivesProbability);
        Assert.AreEqual(1, result.SelfSpecialCakes);
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
            ], specialCakePolicy: SpecialCakePolicy.AttackInheritanceOnly, maxBredIrrelevantPassives: 0,
                maxBreedingSteps: 1, maxSolverIterations: 1),
            Child.Name, targets
        );

        var result = (BredPalReference)results.Single();
        Assert.AreEqual(BreedingMethod.Ordinary, result.Outcome.Method);
        Assert.AreEqual(0, result.ActualPassives.Count);
        Assert.AreEqual(0, result.SelfSpecialCakes);
    }

    [TestMethod]
    [DataRow(SpecialCakePolicy.Never, false)]
    [DataRow(SpecialCakePolicy.AttackInheritanceOnly, false)]
    [DataRow(SpecialCakePolicy.AttacksAndPassives, true)]
    public void Solve_PassiveOnlyCakeIsAvailableUnderBroadPolicy(SpecialCakePolicy policy, bool expectsCake)
    {
        var passives = new[] { "Swift", "Runner", "Artisan", "Serious" }
            .Select(name => name.ToStandardPassive(SolverTestScenario.DB)).ToArray();
        var results = SolverTestScenario.Solve(SolverTestScenario.Solver([
            SolverTestScenario.Owned("Katress", PalGender.MALE, passives.Take(2)),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE, passives.Skip(2)),
        ], specialCakePolicy: policy, maxBreedingSteps: 1, maxSolverIterations: 1),
            Child.Name, requiredPassives: passives).OfType<BredPalReference>().ToList();

        var ordinary = results.Single(result => result.Outcome.Method == BreedingMethod.Ordinary);
        Assert.AreEqual(.1f, ordinary.PassivesProbability, .000001f);
        Assert.AreEqual(expectsCake ? 2 : 1, results.Count);
        if (!expectsCake) return;
        var cake = results.Single(result => result.Outcome.Method == BreedingMethod.SpecialCake);
        Assert.AreEqual(1f, cake.PassivesProbability);
        Assert.AreEqual(1, cake.SelfSpecialCakes);
        Assert.AreEqual(AttackProfile.Inactive, cake.AttackProfile);
        Assert.IsNull(cake.MaterializedAttackInheritance);
        Assert.IsTrue(cake.BreedingEffort < ordinary.BreedingEffort);
        Assert.IsNull(cake.Parent1.LevelRequirements);
        Assert.IsNull(cake.Parent2.LevelRequirements);
    }

    [TestMethod]
    public void Solve_BroadPolicyUsesAttackOnlyRulesWithoutRequestedPassives()
    {
        var results = SolverTestScenario.Solve(SolverTestScenario.Solver([
            Parent("Katress", PalGender.MALE, Attacks[0], []),
            Parent("Wixen", PalGender.FEMALE, Attacks[1], []),
        ], specialCakePolicy: SpecialCakePolicy.AttacksAndPassives,
            maxBreedingSteps: 1, maxSolverIterations: 1), Child.Name, [Attacks[0]])
            .OfType<BredPalReference>().ToList();

        var result = results.Single();
        Assert.AreEqual(BreedingMethod.Ordinary, result.Outcome.Method);
        Assert.AreEqual(0, result.ExpectedSpecialCakes);
        CollectionAssert.AreEqual(new[] { Attacks[0] }, result.MaterializedAttackInheritance.InheritedAttacks.ToArray());
    }

    [TestMethod]
    public void Solve_IVAndGenderTargetsAloneDoNotIntroduceCakes()
    {
        var results = SolverTestScenario.Solve(SolverTestScenario.Solver([
            SolverTestScenario.Owned("Katress", PalGender.MALE, ivHp: 100),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
        ], specialCakePolicy: SpecialCakePolicy.AttacksAndPassives,
            maxBreedingSteps: 1, maxSolverIterations: 1), Child.Name,
            ivHp: 90, requiredGender: PalGender.MALE).OfType<BredPalReference>().ToList();

        var result = results.Single();
        Assert.AreEqual(1f / 3f, result.IVsProbability, .000001f);
        Assert.AreEqual(PalGender.MALE, result.Gender);
        Assert.IsTrue(result.AvgRequiredBreedings > 1);
        Assert.AreEqual(0, result.ExpectedSpecialCakes);
    }

    [TestMethod]
    public void Solve_AllPassivesDeferredToSurgeryDoNotIntroduceCakes()
    {
        var passives = SolverTestScenario.DB.SurgeryPassiveSkills
            .Where(passive => passive.TrackedEffects.Count == 0).Take(4).ToArray();
        var solver = SolverTestScenario.Solver([
            SolverTestScenario.Owned("Katress", PalGender.MALE),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
        ], specialCakePolicy: SpecialCakePolicy.AttacksAndPassives,
            maxBreedingSteps: 1, maxSolverIterations: 1,
            maxSurgeryCost: passives.Sum(passive => passive.SurgeryCost),
            allowedSurgeryPassives: passives, skipSurgeryPassivesDuringSearch: true);

        var results = SolverTestScenario.Solve(solver, Child.Name, requiredPassives: passives);

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(result => result is SurgeryTablePalReference &&
            result.ExpectedSpecialCakes == 0 && passives.All(result.ActualPassives.Contains)));
    }

    [TestMethod]
    public void Solve_PassiveOnlySearchRetainsCakeUsingIntermediatesAndRecursiveCosts()
    {
        var passives = new[] { "Swift", "Runner", "Artisan", "Serious" }
            .Select(name => name.ToStandardPassive(SolverTestScenario.DB)).ToArray();
        var results = SolverTestScenario.Solve(SolverTestScenario.Solver([
            SolverTestScenario.Owned("Katress", PalGender.MALE, passives.Take(2)),
            SolverTestScenario.Owned("Wixen", PalGender.FEMALE, passives.Skip(2)),
            SolverTestScenario.Owned("Kingpaca", PalGender.FEMALE),
        ], specialCakePolicy: SpecialCakePolicy.AttacksAndPassives,
            maxBreedingSteps: 2, maxSolverIterations: 2), "Azurobe", requiredPassives: passives)
            .OfType<BredPalReference>().Where(result => result.NumTotalBreedingSteps == 2).ToList();

        Assert.IsTrue(results.Any(result => Intermediate(result)?.Outcome.Method == BreedingMethod.SpecialCake));
        Assert.IsTrue(results.Any(result => Intermediate(result)?.Outcome.Method == BreedingMethod.Ordinary));
        foreach (var result in results)
        {
            var intermediate = Intermediate(result);
            if (intermediate is null) continue;
            Assert.AreEqual(result.SelfSpecialCakes +
                intermediate.SelfSpecialCakes,
                result.ExpectedSpecialCakes);
        }

        BredPalReference? Intermediate(BredPalReference result) => new[] { result.Parent1, result.Parent2 }
            .OfType<BredPalReference>().SingleOrDefault(parent => parent.Pal == Child);
    }

    private static PalInstance Parent(string name, PalGender gender, ActiveSkill attack, PassiveSkill[] passives)
    {
        var instance = SolverTestScenario.Owned(name, gender, passives);
        instance.ActiveSkills = [attack];
        instance.EquippedActiveSkills = [attack];
        return instance;
    }
}
