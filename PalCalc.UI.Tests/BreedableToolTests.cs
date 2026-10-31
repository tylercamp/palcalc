using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class BreedableToolTests
{
    [TestMethod]
    public async Task Run_GroupsCardsByTier()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        Assert.AreEqual("Already Owned (2)", tool.Groups.First().Title.Value);
        Assert.AreEqual("Tier 1 (1)", tool.Groups[1].Title.Value);
        StringAssert.StartsWith(tool.Groups.Last().Title.Value, "Not Reachable");
        Assert.AreEqual(DB.Pals.Count(), tool.Groups.Sum(g => g.Cards.Count));
    }

    [TestMethod]
    public async Task Run_CollapsesOwnedAndNotReachableGroups()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        Assert.IsFalse(tool.Groups.First().IsExpanded);
        Assert.IsFalse(tool.Groups.Last().IsExpanded);
        Assert.IsTrue(tool.Groups[1..^1].All(g => g.IsExpanded));
    }

    [TestMethod]
    public async Task Run_SkipsEmptyGroups()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(), null!);

        StringAssert.StartsWith(tool.Groups.Single().Title.Value, "Not Reachable");
    }

    [TestMethod]
    public async Task Run_OwnedParentHasGenderAndIVs()
    {
        var penking = Owned("Penking", PalGender.MALE);
        penking.IV_HP = 80;
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(penking, Owned("Bushi", PalGender.FEMALE)), null!);

        var card = tool.Groups[1].Cards.Single();
        var parent = card.Parent1.Pal.ModelObject == penking.Pal ? card.Parent1 : card.Parent2;

        Assert.IsTrue(parent.IsOwned);
        Assert.AreEqual(PalGender.MALE, parent.Gender.Value);
        Assert.AreEqual(80, ((IVDirectValueViewModel)parent.IVs.HP).Value);
    }

    [TestMethod]
    public async Task Run_BredParentHasNoInstance()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        var parents = tool.Groups[2].Cards.SelectMany(c => new[] { c.Parent1, c.Parent2 }).ToList();
        var bred = parents.First(p => !p.IsOwned);

        Assert.IsNull(bred.Gender);
        Assert.IsNull(bred.IVs);
        Assert.IsNull(bred.Location);
    }

    [TestMethod]
    public async Task Run_NewerRunReplacesOlderRun()
    {
        var tool = new BreedableToolViewModel(null!);
        var older = tool.RunAsync(Settings(Owned("Penking", PalGender.MALE)), null!);
        var newer = tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.MALE)), null!);

        await Task.WhenAll(older, newer);

        Assert.AreEqual("Already Owned (2)", tool.Groups.First().Title.Value);
    }

    // accessibility tools call `ToString` on list items, and `ILocalizedText.ToString` throws
    [TestMethod]
    public async Task Run_ItemsAreSafeToConvertToString()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        foreach (var group in tool.Groups)
        {
            _ = group.ToString();
            foreach (var card in group.Cards) _ = card.ToString();
        }
    }

    [TestMethod]
    public async Task Run_ClearsCalculatingNote()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE)), null!);

        Assert.IsFalse(tool.IsCalculating);
    }

    private static readonly PalDB DB = PalDB.LoadEmbedded();

    private static PalInstance Owned(string name, PalGender gender) => new()
    {
        InstanceId = Guid.NewGuid().ToString(),
        Pal = name.ToPal(DB),
        Gender = gender,
    };

    private static BreedingSolverSettings Settings(params PalInstance[] ownedPals) =>
        new(
            db: DB,
            breedingDB: PalBreedingDB.LoadEmbedded(DB),
            gameSettings: new GameSettings(),
            ownedPals: ownedPals,
            resultPruning: ResultPruningPolicy.Default,
            maxBreedingSteps: 1,
            maxSolverIterations: 1,
            maxWildPals: 0,
            allowedWildPals: [],
            bannedBredPals: [],
            maxInputIrrelevantPassives: 0,
            maxBredIrrelevantPassives: 0,
            maxEffort: TimeSpan.MaxValue,
            maxThreads: 1,
            maxSurgeryCost: 0,
            allowedSurgeryPassives: [],
            skipSurgeryPassivesDuringSearch: false,
            useGenderReversers: false,
            maxSpecialCakes: 0,
            trainPals: true
        );
}
