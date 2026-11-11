using PalCalc.Model;
using PalCalc.Solver.PalReference;

namespace PalCalc.Solver.Tests;

[TestClass]
public class IVInheritanceSolverTests
{
    [DataTestMethod]
    [DataRow(100, 100, 100, 100, 100, 100, 5f / 18f, 9, 15)]
    [DataRow(100, 100, 100, 0, 0, 0, 5f / 144f, 72, 120)]
    [DataRow(100, 100, 0, 0, 0, 100, 5f / 144f, 72, 120)]
    [DataRow(100, 100, 0, 100, 0, 100, 5f / 72f, 36, 60)]
    public void Solve_AllHundredIVsUseCorrectedProbabilityAndEffort(
        int firstHp, int firstAttack, int firstDefense,
        int secondHp, int secondAttack, int secondDefense,
        float expectedProbability, int expectedAttempts, int legacyAttempts)
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE,
                    ivHp: firstHp, ivAttack: firstAttack, ivDefense: firstDefense),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE,
                    ivHp: secondHp, ivAttack: secondAttack, ivDefense: secondDefense),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            gameSettings: new GameSettings { MassiveEggIncubationTime = TimeSpan.Zero }
        );

        var result = SolverTestScenario.Solve(solver, "Wixen Noct", ivHp: 100, ivAttack: 100, ivDefense: 100)
            .OfType<BredPalReference>().Single();

        Assert.AreEqual(expectedProbability, result.IVsProbability, 0.000001f);
        Assert.AreEqual(0.4f, result.PassivesProbability, 0.000001f);
        Assert.AreEqual(expectedAttempts, result.AvgRequiredBreedings);
        Assert.AreEqual(TimeSpan.FromMinutes(10 * expectedAttempts), result.BreedingEffort);
        Assert.IsTrue(result.IVs.HP.Satisfies(100) && result.IVs.Attack.Satisfies(100) && result.IVs.Defense.Satisfies(100));
        var singleSourceStats = new[]
        {
            firstHp != secondHp, firstAttack != secondAttack, firstDefense != secondDefense
        }.Count(singleSource => singleSource);
        var legacyIvProbability = (1f / 6f) / (1 << singleSourceStats);
        Assert.AreEqual(legacyAttempts, (int)Math.Ceiling(1f / (legacyIvProbability * result.PassivesProbability)));
        Assert.IsTrue(result.AvgRequiredBreedings < legacyAttempts);
        Console.WriteLine($"IV probability: {result.IVsProbability}; attempts: {result.AvgRequiredBreedings} (old-model calculation {legacyAttempts}); effort: {result.BreedingEffort}");
    }

    [TestMethod]
    public void Solve_AlreadyOwnedAllHundredTargetNeedsNoBreeding()
    {
        var solver = SolverTestScenario.Solver(
            [SolverTestScenario.Owned("Wixen Noct", PalGender.MALE, ivHp: 100, ivAttack: 100, ivDefense: 100)],
            maxBreedingSteps: 0,
            maxSolverIterations: 0
        );

        var result = SolverTestScenario.Solve(solver, "Wixen Noct", ivHp: 100, ivAttack: 100, ivDefense: 100).Single();

        Assert.IsInstanceOfType<OwnedPalReference>(result);
        Assert.AreEqual(TimeSpan.Zero, result.BreedingEffort);
    }
}
