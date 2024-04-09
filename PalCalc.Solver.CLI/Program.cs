
using PalCalc.Model;
using PalCalc.SaveReader;
using PalCalc.Solver;
using System.Diagnostics;

internal class Program
{
    static void Main(string[] args)
    {
        var sw = Stopwatch.StartNew();

        var db = PalDB.LoadEmbedded();
        Console.WriteLine("Loaded Pal DB");

        var saveGame = SavesLocation.AllLocal.SelectMany(l => l.ValidSaveGames).MaxBy(g => g.LastModified);
        Console.WriteLine("Using {0}", saveGame);

        var savedInstances = saveGame.Level.ReadPalInstances(db);
        Console.WriteLine("Loaded save game");

        var solver = new Solver(
            gameSettings: new GameSettings(),
            db: db,
            ownedPals: savedInstances,
            maxBreedingSteps: 20,
            maxWildPals: 1,
            maxIrrelevantTraits: 0,
            maxEffort: TimeSpan.FromDays(7)
        );

        var targetInstance = new PalInstance
        {
            Pal = "Ragnahawk".ToPal(db),
            Gender = PalGender.WILDCARD,
            Traits = new List<Trait> { "Swift".ToTrait(db), "Runner".ToTrait(db), "Nimble".ToTrait(db) },
            Location = null
        };

        var matches = solver.SolveFor(targetInstance);

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