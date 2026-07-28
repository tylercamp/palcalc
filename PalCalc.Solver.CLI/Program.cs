
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

        var saveGame = DirectSavesLocation.AllLocal.SelectMany(l => l.ValidSaveGames).MaxBy(g => g.LevelMeta.ReadGameOptions().PlayerLevel);
        Console.WriteLine("Using {0}", saveGame);

        var savedInstances = saveGame.Level.ReadCharacterData(db, GameSettings.Defaults, [], null).Pals;
        Console.WriteLine("Loaded save game");

        var solverSettings = new BreedingSolverSettings(
                gameSettings: new GameSettings(),
                db: db,
                breedingDB: PalBreedingDB.LoadEmbedded(db),
                resultPruning: ResultPruningPolicy.Default,
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
        );
        var solver = new BreedingSolver();

        solver.StatusUpdateInterval = TimeSpan.FromSeconds(1);
        solver.StatusUpdated += ev => Console.WriteLine($"{ev.CurrentPhase} ({ev.CurrentStepIndex}) - {ev.WorkProcessedCount} / {ev.CurrentWorkSize}");

        var targetInstance = new PalSpecifier
        {
            Pal = "Beakon".ToPal(db),
            RequiredPassives = new List<PassiveSkill> { "Lord of the Sea".ToStandardPassive(db), "Lord of the Underworld".ToStandardPassive(db), "Nimble".ToStandardPassive(db), "Legend".ToStandardPassive(db) },
            IV_Attack = 90,
            //IV_Defense = 90,
            //IV_HP = 90
        };

        var controller = new SolverStateController(
            CancellationToken.None
        );
        var solveResult = solver.Solve(
            new BreedingSolverRequest(targetInstance, solverSettings),
            controller
        );
        var matches = solveResult.Results;

        if (solveResult.IsCanceled)
        {
            Console.WriteLine("Solver was canceled");
            return;
        }

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
