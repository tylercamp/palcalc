
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.ResultPruning;
using PalCalc.Solver.Tree;
using System.Diagnostics;

internal class Program
{
    static void Main(string[] args)
    {
        Logging.InitCommonFull();

        var sw = Stopwatch.StartNew();

        var db = PalDB.LoadEmbedded();
        Console.WriteLine("Loaded Pal DB");

        var reptyro = new OwnedPalReference(
            new PalInstance()
            {
                Gender = PalGender.MALE,
                Pal = "Reptyro Cryst".ToPal(db),
                PassiveSkills = ["Brave".ToStandardPassive(db), "Workaholic".ToStandardPassive(db), "Runner".ToStandardPassive(db)]
            },
            ["Runner".ToStandardPassive(db), new RandomPassiveSkill(), new RandomPassiveSkill()],
            new IV_Set()
        );

        var wixen = new OwnedPalReference(
            new PalInstance()
            {
                Gender = PalGender.FEMALE,
                Pal = "Wixen".ToPal(db),
                PassiveSkills = ["Lucky".ToStandardPassive(db), "Brave".ToStandardPassive(db)]
            },
            ["Lucky".ToStandardPassive(db), "Brave".ToStandardPassive(db)],
            new IV_Set()
        );

        var parentPassives = reptyro.ActualPassives.Concat(wixen.ActualPassives).Distinct().ToList();
        var targetPassives = new List<PassiveSkill>() { "Lucky".ToStandardPassive(db), "Runner".ToStandardPassive(db) };
        var numFinalPassives = 3;

        var prob = PalCalc.Solver.Probabilities.Passives.ProbabilityInheritedTargetPassives(parentPassives, targetPassives, numFinalPassives);

        var saveGame = DirectSavesLocation.AllLocal.SelectMany(l => l.ValidSaveGames).MaxBy(g => g.LevelMeta.ReadGameOptions().PlayerLevel);
        Console.WriteLine("Using {0}", saveGame);

        var savedInstances = saveGame.Level.ReadCharacterData(db, GameSettings.Defaults, [], null).Pals;
        Console.WriteLine("Loaded save game");

        var solver = new BreedingSolver(
            new BreedingSolverSettings(
                gameSettings: new GameSettings(),
                db: db,
                pruningBuilder: PruningRulesBuilder.Default,
                ownedPals: savedInstances,
                maxBreedingSteps: 99,
                maxSolverIterations: 99,
                maxWildPals: 99,
                allowedWildPals: db.Pals.ToList(),
                bannedBredPals: new List<Pal>(),
                maxBredIrrelevantPassives: 2,
                maxInputIrrelevantPassives: 4,
                maxEffort: TimeSpan.FromDays(7),
                maxThreads: 0,
                maxSurgeryCost: 1_000_000,
                allowedSurgeryPassives: db.PassiveSkills.Where(p => p.SupportsSurgery).ToList(),
                useGenderReversers: true
            )
        );

        solver.SolverStateUpdateInterval = TimeSpan.FromSeconds(1);
        solver.SolverStateUpdated += ev => Console.WriteLine($"{ev.CurrentPhase} ({ev.CurrentStepIndex}) - {ev.WorkProcessedCount} / {ev.CurrentWorkSize}");

        var targetInstance = new PalSpecifier
        {
            Pal = "Beakon".ToPal(db),
            RequiredPassives = new List<PassiveSkill> { "Lord of the Sea".ToStandardPassive(db), "Lord of the Underworld".ToStandardPassive(db), "Nimble".ToStandardPassive(db), "Legend".ToStandardPassive(db) },
            IV_Attack = 90,
            //IV_Defense = 90,
            //IV_HP = 90
        };

        var controller = new SolverStateController()
        {
            CancellationToken = CancellationToken.None,
        };
        var matches = solver.SolveFor(targetInstance, controller);

        Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));

        Console.WriteLine("\n\nRESULTS:");
        foreach (var match in matches.OrderBy(m => m.BreedingEffort))
        {
            var tree = new BreedingTree(match);
            tree.Print();
            Console.WriteLine("Should take: {0}\n", match.BreedingEffort);
        }
    }
}