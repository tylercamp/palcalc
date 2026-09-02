
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

        var saveLocation = DirectSavesLocation.AllLocal.MaxBy(l => l.ValidSaveGames.Max(s => s.LastModified));
        var saveGame = saveLocation.ValidSaveGames.MaxBy(g => g.LastModified);
        Console.WriteLine("Using {0}", saveGame);

        var savedInstances = saveGame.Level.ReadCharacterData(db, GameSettings.Defaults, [], null).Pals;
        Console.WriteLine("{0} native pals", savedInstances.Count);

        var fromGps = saveLocation.GlobalPalStorage.ReadPals("GPS").Pals;
        savedInstances.AddRange(fromGps);
        Console.WriteLine("{0} GPS pals", fromGps.Count);

        var fromDps = saveGame.Players.SelectMany(p => p.DimensionalPalStorageSaveFile?.ReadPals("DPS")?.Pals ?? []).ToList();
        savedInstances.AddRange(fromDps);
        Console.WriteLine("{0} DPS pals", fromDps.Count);

        Console.WriteLine("Loaded save game");

        var solverSettings = new BreedingSolverSettings(
                gameSettings: new GameSettings(),
                db: db,
                breedingDB: PalBreedingDB.LoadEmbedded(db),
                resultPruning: ResultPruningPolicy.Default,
                ownedPals: savedInstances,
                maxBreedingSteps: 6,
                maxSolverIterations: 99,
                maxWildPals: 0,
                allowedWildPals: db.Pals.ToList(),
                bannedBredPals: new List<Pal>(),
                maxBredIrrelevantPassives: 0,
                maxInputIrrelevantPassives: 4,
                maxEffort: TimeSpan.FromDays(7),
                maxThreads: 1,
                maxSurgeryCost: 1_000_000,
                allowedSurgeryPassives: db.PassiveSkills.Where(p => p.SupportsSurgery).ToList(),
                useGenderReversers: true,
                maxSpecialCakes: 1000000
        );
        var solver = new BreedingSolver();

        solver.StatusUpdateInterval = TimeSpan.FromSeconds(1);
        solver.StatusUpdated += ev => Console.WriteLine($"{ev.CurrentPhase} ({ev.CurrentStepIndex:N0}) - {ev.WorkProcessedCount:N0} / {ev.CurrentWorkSize:N0}");

        var requiredAttack = "PowerShot".InternalToActive(db);

        var targetInstance = new PalSpecifier
        {
            Pal = "Beakon".ToPal(db),
            RequiredPassives = new List<PassiveSkill> { "Nimble".ToStandardPassive(db) },
            RequiredAttacks = [requiredAttack],
            //IV_Attack = 90,
            //IV_Defense = 90,
            //IV_HP = 90
        };

        var targetInstance2 = new PalSpecifier
        {
            Pal = "Broncherry".ToPal(db),
            RequiredPassives = [],
            RequiredAttacks = [ "Bog Blast".ToActive(db) /* , "Bubble Blast".ToActive(db) , "Aqua Gun".ToActive(db) */ ],
            //IV_Attack = 90,
            //IV_Defense = 90,
            //IV_HP = 90
        };

        var controller = new SolverStateController(
            CancellationToken.None
        );
        var solveResult = solver.Solve(
            new BreedingSolverRequest(targetInstance2, solverSettings),
            controller
        );
        var matches = solveResult.Results;

        if (solveResult.IsCanceled)
        {
            Console.WriteLine("Solver was canceled");
            return;
        }

        Console.WriteLine("Took {0}", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds));
        Console.WriteLine("Required attack: {0} ({1})", requiredAttack.Name, requiredAttack.InternalName);

        Console.WriteLine("\n\nRESULTS:");
        foreach (var match in matches.OrderBy(m => m.BreedingEffort))
        {
            var tree = new BreedingTree(match);
            tree.Print();
            foreach (var bred in tree.AllNodes.Select(node => node.Item1.PalRef).OfType<BredPalReference>())
            {
                Console.WriteLine(
                    "{0} attacks: {1}; chance: {2:P2}; expected attempts: {3}; step effort: {4}",
                    bred.Pal.Name,
                    string.Join(", ", bred.MaterializedAttackInheritance?.ChildLearnedAttacks.Select(attack => attack.Name) ?? []),
                    bred.MaterializedAttackInheritance?.AttackProbability ?? 1,
                    bred.AvgRequiredBreedings,
                    bred.SelfBreedingEffort
                );
            }
            Console.WriteLine(
                "Selected result attacks: {0}",
                string.Join(", ", match switch
                {
                    BredPalReference bred => bred.MaterializedAttackInheritance?.ChildLearnedAttacks.Select(attack => attack.Name) ?? [],
                    OwnedPalReference owned => (owned.UnderlyingInstance.ActiveSkills ?? []).Select(attack => attack.Name),
                    _ => [],
                })
            );
            Console.WriteLine("Should take: {0}\n", match.BreedingEffort);
        }
    }
}
