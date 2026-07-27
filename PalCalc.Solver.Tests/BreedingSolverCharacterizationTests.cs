using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.Processing;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedingSolverCharacterizationTests
{
    [TestMethod]
    public void EmbeddedBreedingData_DistinguishesKatressWixenParentGenders()
    {
        var db = SolverTestScenario.DB;
        var breedingDb = PalBreedingDB.LoadEmbedded(db);
        var katress = "Katress".ToPal(db);
        var wixen = "Wixen".ToPal(db);
        var results = breedingDb.BreedingByParent[katress][wixen];

        Assert.AreEqual(
            "Wixen Noct",
            results.Single(r => r.Matches(katress, PalGender.MALE, wixen, PalGender.FEMALE)).Child.Name
        );
        Assert.AreEqual(
            "Katress Ignis",
            results.Single(r => r.Matches(katress, PalGender.FEMALE, wixen, PalGender.MALE)).Child.Name
        );
    }

    [DataTestMethod]
    [DataRow(PalGender.MALE, PalGender.FEMALE, "Wixen Noct")]
    [DataRow(PalGender.FEMALE, PalGender.MALE, "Katress Ignis")]
    public void Solve_UsesGenderSpecificBreedingResult(
        PalGender katressGender,
        PalGender wixenGender,
        string expectedChild
    )
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", katressGender),
                SolverTestScenario.Owned("Wixen", wixenGender),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var results = SolverTestScenario.Solve(solver, expectedChild);

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.Pal.Name == expectedChild));

        var bred = results.OfType<BredPalReference>().First();
        var katressParent = new[] { bred.Parent1, bred.Parent2 }.Single(p => p.Pal.Name == "Katress");
        var wixenParent = new[] { bred.Parent1, bred.Parent2 }.Single(p => p.Pal.Name == "Wixen");

        Assert.AreEqual(katressGender, katressParent.Gender);
        Assert.AreEqual(wixenGender, wixenParent.Gender);
    }

    [TestMethod]
    public void Solve_StopsAfterFrontierConverges()
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 20
        );
        var observedBreedingSteps = new HashSet<int>();

        solver.StatusUpdated += status =>
        {
            if (status.CurrentPhase == SolverPhase.Breeding)
                observedBreedingSteps.Add(status.CurrentStepIndex);
        };

        var results = SolverTestScenario.Solve(solver, "Wixen Noct");

        Assert.IsTrue(results.Count > 0);
        CollectionAssert.AreEquivalent(new[] { 0 }, observedBreedingSteps.ToArray());
    }

    [TestMethod]
    public void Solve_PreservesOwnedAndBredResultEffortTiers()
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Wixen Noct", PalGender.FEMALE),
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var results = SolverTestScenario.Solve(solver, "Wixen Noct");
        var effortTiers = results.Select(r => r.BreedingEffort).Distinct().Order().ToList();

        Assert.IsTrue(effortTiers.Contains(TimeSpan.Zero));
        Assert.IsTrue(effortTiers.Any(e => e > TimeSpan.Zero));
    }

    [TestMethod]
    public void Solve_RespectsMaximumBreedingSteps()
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 0,
            maxSolverIterations: 5
        );

        var results = SolverTestScenario.Solve(solver, "Wixen Noct");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Solve_RespectsMaximumEffort()
    {
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxEffort: TimeSpan.Zero
        );

        var results = SolverTestScenario.Solve(solver, "Wixen Noct");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Solve_RespectsMaximumWildPalCount()
    {
        var allowedWildPals = new[]
        {
            "Katress".ToPal(SolverTestScenario.DB),
            "Wixen".ToPal(SolverTestScenario.DB),
        };
        var oneWildPalResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                ownedPals: [],
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxWildPals: 1,
                allowedWildPals: allowedWildPals
            ),
            "Wixen Noct"
        );
        var twoWildPalResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                ownedPals: [],
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxWildPals: 2,
                allowedWildPals: allowedWildPals
            ),
            "Wixen Noct"
        );

        Assert.AreEqual(0, oneWildPalResults.Count);
        Assert.IsTrue(twoWildPalResults.Count > 0);
        Assert.IsTrue(twoWildPalResults.All(r => r.NumTotalWildPals == 2));
    }

    [TestMethod]
    public void Solve_RespectsBannedBredPals()
    {
        var wixenNoct = "Wixen Noct".ToPal(SolverTestScenario.DB);
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            bannedBredPals: [wixenNoct]
        );

        var results = SolverTestScenario.Solve(solver, "Wixen Noct");

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Solve_ProducesRequiredPassiveAndIVState()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned(
                    "Katress",
                    PalGender.MALE,
                    passives: [swift],
                    ivAttack: 100
                ),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var results = SolverTestScenario.Solve(
            solver,
            "Wixen Noct",
            requiredPassives: [swift],
            ivAttack: 90
        );

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.EffectivePassives.Contains(swift)));
        Assert.IsTrue(results.All(r => r.IVs.Attack.IsRelevant));
        Assert.IsTrue(results.All(r => r.IVs.Attack.Satisfies(90)));
    }

    [TestMethod]
    public void Solve_AppliesConfiguredPassiveSurgeryAfterBreeding()
    {
        var surgeryPassive = SolverTestScenario.DB.SurgeryPassiveSkills.First();
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxSurgeryCost: surgeryPassive.SurgeryCost,
            allowedSurgeryPassives: [surgeryPassive]
        );

        var results = SolverTestScenario.Solve(
            solver,
            "Wixen Noct",
            requiredPassives: [surgeryPassive]
        );

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(r => r.EffectivePassives.Contains(surgeryPassive)));
        Assert.IsTrue(results.Any(r => r is SurgeryTablePalReference));
        Assert.IsTrue(results.All(r => r.TotalCost == surgeryPassive.SurgeryCost));
    }

    [TestMethod]
    public void Solve_DoesNotExceedConfiguredSurgeryCost()
    {
        var surgeryPassive = SolverTestScenario.DB.SurgeryPassiveSkills.First();
        var solver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxSurgeryCost: surgeryPassive.SurgeryCost - 1,
            allowedSurgeryPassives: [surgeryPassive]
        );

        var results = SolverTestScenario.Solve(
            solver,
            "Wixen Noct",
            requiredPassives: [surgeryPassive]
        );

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public void Solve_EnforcesRequiredGenderOnWildcardBredResult()
    {
        var wildcardSolver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );
        var genderedSolver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var wildcardResults = SolverTestScenario.Solve(wildcardSolver, "Wixen Noct");
        var maleResults = SolverTestScenario.Solve(
            genderedSolver,
            "Wixen Noct",
            requiredGender: PalGender.MALE
        );

        Assert.IsTrue(wildcardResults.Count > 0);
        Assert.IsTrue(maleResults.Count > 0);
        Assert.IsTrue(wildcardResults.All(r => r.Gender == PalGender.WILDCARD));
        Assert.IsTrue(maleResults.All(r => r.Gender == PalGender.MALE));
        Assert.IsTrue(maleResults.Min(r => r.BreedingEffort) >= wildcardResults.Min(r => r.BreedingEffort));
    }

    [TestMethod]
    public void Solve_SingleAndMultipleWorkersProduceSameSemanticResults()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);
        var ownedPals = new[]
        {
            SolverTestScenario.Owned(
                "Katress",
                PalGender.MALE,
                passives: [swift],
                ivAttack: 100
            ),
            SolverTestScenario.Owned(
                "Wixen",
                PalGender.FEMALE,
                passives: [runner]
            ),
        };

        var singleWorkerResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                ownedPals,
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxThreads: 1
            ),
            "Wixen Noct",
            requiredPassives: [swift],
            optionalPassives: [runner],
            ivAttack: 90
        );
        var multipleWorkerResults = SolverTestScenario.Solve(
            SolverTestScenario.Solver(
                ownedPals,
                maxBreedingSteps: 1,
                maxSolverIterations: 1,
                maxThreads: 2
            ),
            "Wixen Noct",
            requiredPassives: [swift],
            optionalPassives: [runner],
            ivAttack: 90
        );

        CollectionAssert.AreEqual(
            SolverTestScenario.Signatures(singleWorkerResults).ToArray(),
            SolverTestScenario.Signatures(multipleWorkerResults).ToArray()
        );
    }
}
