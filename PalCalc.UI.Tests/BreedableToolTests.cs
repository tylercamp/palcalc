using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

// the view models and localization used here expect to run on the UI thread, so these tests can't run in parallel
[TestClass]
[DoNotParallelize]
public class BreedableToolTests
{
    [TestMethod]
    public async Task Run_GroupsCardsByTier()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        Assert.AreEqual("Already Owned (2) - Best IVs Shown", tool.Groups.First().Title.Value);
        Assert.AreEqual("Tier 1 (1)", tool.Groups[1].Title.Value);
        StringAssert.StartsWith(tool.Groups.Last().Title.Value, "Not Reachable");
        Assert.AreEqual(DB.Pals.Count() - 1, tool.Groups.Sum(g => g.Cards.Count));
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
    public async Task Run_CardShowsIVRange()
    {
        var penking = Owned("Penking", PalGender.MALE);
        var bushi = Owned("Bushi", PalGender.FEMALE);
        penking.IV_HP = 20;
        bushi.IV_HP = 70;
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(penking, bushi), null!);

        var range = (IVRangeValueViewModel)tool.Groups[1].Cards.Single().IVs.HP;
        Assert.AreEqual((20, 70), (range.Min, range.Max));
    }

    [TestMethod]
    public async Task Run_OwnedCardHasBestOwnedPal()
    {
        var weak = Owned("Penking", PalGender.MALE);
        var strong = Owned("Penking", PalGender.FEMALE);
        strong.IV_HP = 90;
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(weak, strong), null!);

        var owned = tool.Groups.First().Cards.Single().Owned;
        Assert.AreEqual(PalGender.FEMALE, owned.Gender.Value);
        Assert.AreEqual(90, ((IVDirectValueViewModel)owned.IVs.HP).Value);
    }

    [TestMethod]
    public async Task Run_BredCardHasNoOwnedPal()
    {
        var tool = await RunWithPenkingAndBushi();

        Assert.IsNull(tool.Groups[1].Cards.Single().Owned);
    }

    [TestMethod]
    public async Task Run_NotReachableCardHasNoIVs()
    {
        var tool = new BreedableToolViewModel(null!);

        await tool.RunAsync(Settings(), null!);

        Assert.IsTrue(tool.Groups.Single().Cards.All(c => c.IVs == null && c.Owned == null));
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

    [DataTestMethod]
    [DataRow("sibel")]
    [DataRow("SIBELYX")]
    [DataRow("  belyx ")]
    public async Task Search_HighlightsNameMatches(string search)
    {
        var tool = await RunWithPenkingAndBushi();

        tool.SearchText = search;

        var card = tool.Groups[1].Cards.Single();
        Assert.IsTrue(card.IsHighlighted);
        Assert.IsTrue(tool.Groups[1].HasHighlightedCards);
        Assert.IsFalse(tool.Groups[0].HasHighlightedCards);
        Assert.IsTrue(tool.HasHighlightedCards);
    }

    [TestMethod]
    public async Task Search_ClearingRemovesHighlights()
    {
        var tool = await RunWithPenkingAndBushi();

        tool.SearchText = "sibel";
        tool.SearchText = "  ";

        Assert.IsFalse(tool.HasHighlightedCards);
        Assert.IsFalse(tool.Groups.Any(g => g.HasHighlightedCards));
        Assert.IsFalse(tool.Groups.SelectMany(g => g.Cards).Any(c => c.IsHighlighted));
    }

    [TestMethod]
    public async Task Search_AppliesToNewRuns()
    {
        var tool = await RunWithPenkingAndBushi();
        tool.SearchText = "sibel";

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        Assert.IsTrue(tool.Groups[1].Cards.Single().IsHighlighted);
    }

    [TestMethod]
    public async Task ToggleExpanded_ExpandsThenCollapsesEveryGroup()
    {
        var tool = await RunWithPenkingAndBushi();
        Assert.AreEqual("Expand All", tool.ExpandToggleLabel.Value);

        tool.ToggleExpandedCommand.Execute(null);

        Assert.IsTrue(tool.Groups.All(g => g.IsExpanded));
        Assert.AreEqual("Collapse All", tool.ExpandToggleLabel.Value);

        tool.ToggleExpandedCommand.Execute(null);

        Assert.IsFalse(tool.Groups.Any(g => g.IsExpanded));
        Assert.AreEqual("Expand All", tool.ExpandToggleLabel.Value);
    }

    [TestMethod]
    public async Task ToggleExpanded_FollowsManualChanges()
    {
        var tool = await RunWithPenkingAndBushi();
        tool.ToggleExpandedCommand.Execute(null);

        tool.Groups[0].IsExpanded = false;

        Assert.AreEqual("Expand All", tool.ExpandToggleLabel.Value);

        tool.ToggleExpandedCommand.Execute(null);

        Assert.IsTrue(tool.Groups.All(g => g.IsExpanded));
    }

    [TestMethod]
    public async Task Run_IsLoadingUntilEveryCardIsShown()
    {
        var tool = new BreedableToolViewModel(null!);
        var changes = new List<(bool IsLoading, int NumShown)>();
        tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BreedableToolViewModel.IsLoading))
                changes.Add((tool.IsLoading, tool.Groups.Sum(g => g.Cards.Count)));
        };

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);

        CollectionAssert.AreEqual(new[] { (true, 0), (false, DB.Pals.Count() - 1) }, changes);
        Assert.IsTrue(tool.Groups.All(g => g.Cards.SequenceEqual(g.AllCards)));
    }

    [TestMethod]
    public async Task Run_NewerRunReplacesOneStillLoading()
    {
        var tool = new BreedableToolViewModel(null!);
        var hooked = false;
        var started = false;
        Task second = null;

        // (starts the second run as soon as the first adds a card, like a rerun arriving between batches in the app)
        tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(BreedableToolViewModel.Groups) || tool.Groups.Count == 0 || hooked) return;
            hooked = true;
            tool.Groups[0].Cards.CollectionChanged += (_, _) =>
            {
                if (started) return;
                started = true;
                second = tool.RunAsync(Settings(), null!);
            };
        };

        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);
        Assert.IsTrue(started);
        await second;

        Assert.IsFalse(tool.IsLoading);
        StringAssert.StartsWith(tool.Groups.Single().Title.Value, "Not Reachable");
        Assert.IsTrue(tool.Groups.All(g => g.Cards.SequenceEqual(g.AllCards)));
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

    private static async Task<BreedableToolViewModel> RunWithPenkingAndBushi()
    {
        var tool = new BreedableToolViewModel(null!);
        await tool.RunAsync(Settings(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE)), null!);
        return tool;
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
            specialCakePolicy: SpecialCakePolicy.Never,
            trainPals: true
        );
}
