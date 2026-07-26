using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedingMechanicsTests
{
    [TestMethod]
    public void DefaultMechanics_UseExpectedProbabilityTables()
    {
        var mechanics = BreedingMechanics.Default;

        Assert.AreEqual(0.25f, mechanics.IVProbabilityDirect[3]);
        Assert.AreEqual(0.10f, mechanics.PassiveProbabilityDirect[4]);
        Assert.AreEqual(0.30f, mechanics.PassiveProbabilityAtLeastN[3]);
        Assert.AreEqual(0.60f, mechanics.PassiveRandomAddedAtLeastN[1]);
        Assert.AreEqual(0.80f, mechanics.PassivesWildAtMostN[3]);

    }

    [TestMethod]
    public void PalDBJson_RoundTripsCustomizedMechanics()
    {
        var db = CloneDB();
        db.BreedingMechanics = MechanicsWith(
            ivProbabilityDirect: new Dictionary<int, float>
            {
                { 0, 0 },
                { 1, 1 },
                { 2, 0 },
                { 3, 0 },
            },
            minimumCaptureTime: TimeSpan.FromMinutes(7)
        );

        var roundTripped = PalDB.FromJson(db.ToJson());

        Assert.AreEqual(
            1.0f / 3.0f,
            roundTripped.BreedingMechanics
                .ProbabilityOfInheritingDesiredIVs(1)
        );
    }

    [TestMethod]
    public void WildPalReference_UsesSuppliedCaptureMechanics()
    {
        var pal = "Katress".ToPal(SolverTestScenario.DB);
        var mechanics = MechanicsWith(
            passivesWildAtMostN: new Dictionary<int, float>
            {
                { 0, 0.5f },
                { 1, 0.6f },
                { 2, 0.7f },
                { 3, 0.8f },
                { 4, 1.0f },
            },
            minimumCaptureTime: TimeSpan.FromMinutes(10),
            capturePriceThreshold: pal.Price
        );

        var reference = new WildPalReference(
            pal,
            guaranteedPassives: [],
            numRandomPassives: 0,
            mechanics: mechanics
        );

        Assert.AreEqual(
            TimeSpan.FromMinutes(20),
            reference.SelfBreedingEffort
        );
    }

    [TestMethod]
    public void SolverRunContext_CapturesCurrentPalDBMechanics()
    {
        var db = CloneDB();
        var capturedMechanics = MechanicsWith(
            ivProbabilityDirect: new Dictionary<int, float>
            {
                { 0, 0 },
                { 1, 1 },
                { 2, 0 },
                { 3, 0 },
            }
        );
        db.BreedingMechanics = capturedMechanics;
        var settings = Settings(db, []);
        var request = new BreedingSolverRequest(
            new PalSpecifier
            {
                Pal = "Wixen Noct".ToPal(db),
            },
            settings
        );

        var context = SolverRunContext.Create(
            request,
            new SolverStateController(CancellationToken.None)
        );
        db.BreedingMechanics = BreedingMechanics.CreateDefault();

        Assert.AreSame(capturedMechanics, context.Mechanics);
        Assert.AreNotSame(db.BreedingMechanics, context.Mechanics);
        Assert.AreEqual(
            1.0f / 3.0f,
            context.Mechanics.ProbabilityOfInheritingDesiredIVs(1)
        );
        Assert.AreEqual(
            BreedingMechanics.Default
                .ProbabilityOfInheritingDesiredIVs(1),
            db.BreedingMechanics.ProbabilityOfInheritingDesiredIVs(1)
        );
    }

    [TestMethod]
    public void SolverRuns_UseIndependentPalDBMechanics()
    {
        var baselineDb = CloneDB();
        var customizedDb = CloneDB();
        customizedDb.BreedingMechanics = MechanicsWith(
            passiveRandomAddedProbability:
                new Dictionary<int, float>
                {
                    { 0, 1 },
                    { 1, 0 },
                    { 2, 0 },
                    { 3, 0 },
                    { 4, 0 },
                }
        );

        var baseline = SolveSingleBreeding(baselineDb);
        var customized = SolveSingleBreeding(customizedDb);

        Assert.AreEqual(3, baseline.AvgRequiredBreedings);
        Assert.AreEqual(1, customized.AvgRequiredBreedings);

        baselineDb.BreedingMechanics =
            MechanicsWith(minimumCaptureTime: TimeSpan.FromHours(1));

        Assert.AreEqual(
            1,
            customizedDb.BreedingMechanics
                .PassiveRandomAddedProbability[0]
        );
    }

    private static BredPalReference SolveSingleBreeding(PalDB db)
    {
        var settings = Settings(
            db,
            [
                Owned(db, "Katress", PalGender.MALE),
                Owned(db, "Wixen", PalGender.FEMALE),
            ]
        );
        var results = new BreedingSolver().Solve(
            new BreedingSolverRequest(
                new PalSpecifier
                {
                    Pal = "Wixen Noct".ToPal(db),
                },
                settings
            ),
            new SolverStateController(CancellationToken.None)
        ).Results;

        return results.OfType<BredPalReference>().Single();
    }

    private static BreedingSolverSettings Settings(
        PalDB db,
        IEnumerable<PalInstance> ownedPals
    ) =>
        new(
            db: db,
            breedingDB: PalBreedingDB.LoadEmbedded(db),
            gameSettings: new GameSettings(),
            ownedPals: ownedPals,
            resultPruning: ResultPruningPolicy.Default,
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxWildPals: 0,
            allowedWildPals: [],
            bannedBredPals: [],
            maxInputIrrelevantPassives:
                GameConstants.MaxTotalPassives,
            maxBredIrrelevantPassives: 0,
            maxEffort: TimeSpan.MaxValue,
            maxThreads: 1,
            maxSurgeryCost: 0,
            allowedSurgeryPassives: [],
            useGenderReversers: false
        );

    private static PalInstance Owned(
        PalDB db,
        string palName,
        PalGender gender
    ) =>
        new()
        {
            InstanceId = Guid.NewGuid().ToString(),
            OwnerPlayerId = "mechanics-test",
            Pal = palName.ToPal(db),
            Gender = gender,
            PassiveSkills = [],
            Location = new PalLocation
            {
                ContainerId = "mechanics-test-palbox",
                Type = LocationType.Palbox,
            },
            ActiveSkills = [],
            EquippedActiveSkills = [],
        };

    private static PalDB CloneDB() =>
        PalDB.FromJson(SolverTestScenario.DB.ToJson());

    private static BreedingMechanics MechanicsWith(
        IReadOnlyDictionary<int, float>? ivProbabilityDirect = null,
        IReadOnlyDictionary<int, float>?
            passiveRandomAddedProbability = null,
        IReadOnlyDictionary<int, float>?
            passivesWildAtMostN = null,
        TimeSpan? minimumCaptureTime = null,
        int? capturePriceThreshold = null
    )
    {
        var defaults = BreedingMechanics.Default;
        return new(
            ivProbabilityDirect:
                ivProbabilityDirect ??
                defaults.IVProbabilityDirect,
            passiveProbabilityDirect:
                defaults.PassiveProbabilityDirect,
            passiveRandomAddedProbability:
                passiveRandomAddedProbability ??
                defaults.PassiveRandomAddedProbability,
            passivesWildAtMostN:
                passivesWildAtMostN ??
                defaults.PassivesWildAtMostN
        );
    }
}
