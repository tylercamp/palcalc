
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
                PassiveSkills = ["Brave".ToPassive(db), "Workaholic".ToPassive(db), "Runner".ToPassive(db)]
            },
            ["Runner".ToPassive(db), new RandomPassiveSkill(), new RandomPassiveSkill()],
            new IV_Set()
        );

        var wixen = new OwnedPalReference(
            new PalInstance()
            {
                Gender = PalGender.FEMALE,
                Pal = "Wixen".ToPal(db),
                PassiveSkills = ["Lucky".ToPassive(db), "Brave".ToPassive(db)]
            },
            ["Lucky".ToPassive(db), "Brave".ToPassive(db)],
            new IV_Set()
        );

        var parentPassives = reptyro.ActualPassives.Concat(wixen.ActualPassives).Distinct().ToList();
        var targetPassives = new List<PassiveSkill>() { "Lucky".ToPassive(db), "Runner".ToPassive(db) };
        var numFinalPassives = 3;

        var prob = PalCalc.Solver.Probabilities.Passives.ProbabilityInheritedTargetPassives(parentPassives, targetPassives, numFinalPassives);

        var saveGame = DirectSavesLocation.AllLocal.SelectMany(l => l.ValidSaveGames).MaxBy(g => g.LastModified);
        Console.WriteLine("Using {0}", saveGame);

        var savedInstances = saveGame.Level.ReadCharacterData(db, []).Pals;
        Console.WriteLine("Loaded save game");

        var solver = new BreedingSolver(
            gameSettings: new GameSettings(),
            db: db,
            pruningBuilder: PruningRulesBuilder.Default,
            ownedPals: savedInstances,
            maxBreedingSteps: 20,
            maxSolverIterations: 20,
            maxWildPals: 1,
            allowedWildPals: db.Pals.ToList(),
            bannedBredPals: new List<Pal>(),
            maxBredIrrelevantPassives: 0,
            maxInputIrrelevantPassives: 2,
            maxEffort: TimeSpan.FromDays(7),
            maxThreads: 0
        );

        var targetInstance = new PalSpecifier
        {
            Pal = "Ragnahawk".ToPal(db),
            RequiredPassives = new List<PassiveSkill> { "Swift".ToPassive(db), "Runner".ToPassive(db), "Nimble".ToPassive(db) },
        };

        var controller = new SolverStateController()
        {
            CancellationToken = CancellationToken.None,
        };
        var matches = solver.SolveFor(targetInstance, controller);

        Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));

        Console.WriteLine("\n\nRESULTS:");
        foreach (var match in matches)
        {
            var tree = new BreedingTree(match);
            tree.Print();
            Console.WriteLine("Should take: {0}\n", match.BreedingEffort);
        }
    }
}