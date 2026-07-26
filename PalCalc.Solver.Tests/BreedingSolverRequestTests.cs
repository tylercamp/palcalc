using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedingSolverRequestTests
{
    [TestMethod]
    public void Constructor_NormalizesTargetSnapshotWithoutMutatingCaller()
    {
        var swift = "Swift".ToStandardPassive(SolverTestScenario.DB);
        var runner = "Runner".ToStandardPassive(SolverTestScenario.DB);
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredPassives = [swift, swift],
            OptionalPassives = [swift, runner, runner],
            RequiredGender = PalGender.MALE,
            IV_Attack = 90,
        };
        var settings = SolverTestScenario.Solver([]).Settings;

        var request = new BreedingSolverRequest(target, settings);
        var snapshot = request.Target;

        CollectionAssert.AreEqual(new[] { swift, swift }, target.RequiredPassives);
        CollectionAssert.AreEqual(new[] { swift, runner, runner }, target.OptionalPassives);
        CollectionAssert.AreEqual(new[] { swift }, snapshot.RequiredPassives);
        CollectionAssert.AreEqual(new[] { runner }, snapshot.OptionalPassives);
        Assert.AreEqual(target.Pal, snapshot.Pal);
        Assert.AreEqual(PalGender.MALE, snapshot.RequiredGender);
        Assert.AreEqual(90, snapshot.IV_Attack);

        snapshot.RequiredPassives.Clear();
        Assert.AreEqual(1, request.Target.RequiredPassives.Count);
    }

    [TestMethod]
    public void Constructor_SnapshotsCollectionsAndRetainsGameSettings()
    {
        var db = SolverTestScenario.DB;
        var gameSettings = new GameSettings
        {
            BreedingTime = TimeSpan.FromMinutes(5),
            MultipleBreedingFarms = false,
        };
        var ownedPals = new List<PalInstance>
        {
            SolverTestScenario.Owned("Katress", PalGender.MALE),
        };
        var allowedWildPals = new List<Pal> { "Wixen".ToPal(db) };
        var bannedBredPals = new List<Pal> { "Anubis".ToPal(db) };
        var surgeryPassives = new List<PassiveSkill> { db.SurgeryPassiveSkills.First() };

        var settings = new BreedingSolverSettings(
            db: db,
            breedingDB: PalBreedingDB.LoadEmbedded(db),
            gameSettings: gameSettings,
            ownedPals: ownedPals,
            resultPruning: ResultPruningPolicy.Default,
            maxBreedingSteps: 3,
            maxSolverIterations: 4,
            maxWildPals: 2,
            allowedWildPals: allowedWildPals,
            bannedBredPals: bannedBredPals,
            maxInputIrrelevantPassives: 2,
            maxBredIrrelevantPassives: 1,
            maxEffort: TimeSpan.FromDays(1),
            maxThreads: 1,
            maxSurgeryCost: 100,
            allowedSurgeryPassives: surgeryPassives,
            useGenderReversers: false
        );

        gameSettings.BreedingTime = TimeSpan.FromHours(1);
        gameSettings.MultipleBreedingFarms = true;
        ownedPals.Clear();
        allowedWildPals.Clear();
        bannedBredPals.Clear();
        surgeryPassives.Clear();

        Assert.AreSame(gameSettings, settings.GameSettings);
        Assert.AreEqual(TimeSpan.FromHours(1), settings.GameSettings.BreedingTime);
        Assert.IsTrue(settings.GameSettings.MultipleBreedingFarms);
        Assert.AreEqual(1, settings.OwnedPals.Count);
        Assert.AreEqual(1, settings.AllowedWildPals.Count);
        Assert.AreEqual(1, settings.BannedBredPals.Count);
        Assert.AreEqual(1, settings.SurgeryPassives.Count);
    }

    [TestMethod]
    public void SolverRunContext_UsesFixedRequestDefinition()
    {
        var settings = SolverTestScenario.Solver([]).Settings;
        var target = new PalSpecifier
        {
            Pal = "Wixen Noct".ToPal(SolverTestScenario.DB),
            RequiredPassives = ["Swift".ToStandardPassive(SolverTestScenario.DB)],
        };
        var request = new BreedingSolverRequest(
            target,
            settings
        );
        target.RequiredPassives.Clear();

        var context = SolverRunContext.Create(
            request,
            new SolverStateController(CancellationToken.None)
        );

        Assert.AreEqual(1, context.Target.RequiredPassives.Count);
        Assert.AreSame(settings, context.Settings);
    }

    [TestMethod]
    public void BreedingSolver_RepeatedRunsDoNotShareMutableRunState()
    {
        var configuredSolver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned("Katress", PalGender.MALE),
                SolverTestScenario.Owned("Wixen", PalGender.FEMALE),
            ],
            maxBreedingSteps: 1,
            maxSolverIterations: 1
        );

        var firstResults = SolverTestScenario.Solve(configuredSolver, "Wixen Noct");
        var secondResults = SolverTestScenario.Solve(configuredSolver, "Wixen Noct");

        CollectionAssert.AreEqual(
            SolverTestScenario.Signatures(firstResults).ToArray(),
            SolverTestScenario.Signatures(secondResults).ToArray()
        );
        Assert.IsFalse(
            firstResults.Any(first =>
                secondResults.Any(second => ReferenceEquals(first, second))
            )
        );
    }

    [TestMethod]
    public void BreedingSolver_ReturnsReadOnlyCompletedResult()
    {
        var configuredSolver = SolverTestScenario.Solver(
            [
                SolverTestScenario.Owned(
                    "Wixen Noct",
                    PalGender.MALE
                ),
            ],
            maxBreedingSteps: 0,
            maxSolverIterations: 0
        );

        var result = configuredSolver.Solver.Solve(
            new BreedingSolverRequest(
                new PalSpecifier
                {
                    Pal = "Wixen Noct".ToPal(
                        SolverTestScenario.DB
                    ),
                },
                configuredSolver.Settings
            ),
            new SolverStateController(CancellationToken.None)
        );

        Assert.IsFalse(result.IsCanceled);
        Assert.AreEqual(1, result.Results.Count);
        Assert.ThrowsException<NotSupportedException>(() =>
            ((IList<IPalReference>)result.Results)
                .Add(result.Results[0])
        );
    }
}
