using PalCalc.Model;
using PalCalc.Solver;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.ResultPruning;
using PalCalc.UI.ViewModel.Mapped;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

// the view models and localization used here expect to run on the UI thread, so these tests can't run in parallel
[TestClass]
[DoNotParallelize]
public class ParentCombosToolTests
{
    [TestMethod]
    public void Run_NoChildHasNoGroups()
    {
        var tool = RunWithPenkingAndBushi();

        Assert.IsNull(tool.Child);
        Assert.AreEqual(0, tool.Groups.Count);
    }

    [TestMethod]
    public void Child_GroupsCardsByOwnedParents()
    {
        var tool = RunWithPenkingAndBushi();

        tool.Child = Pal("Sibelyx");

        Assert.AreEqual("Ready Now (1)", tool.Groups.First().Title.Value);
        StringAssert.StartsWith(tool.Groups[1].Title.Value, "Breed or Catch One First");
        StringAssert.StartsWith(tool.Groups.Last().Title.Value, "Neither Parent Owned");
        Assert.AreEqual(BreedingDB.Breeding.Count(b => b.Child == "Sibelyx".ToPal(DB) && b.Parents.All(p => p.Pal.InternalName != BreedableCalculator.HiddenPalInternalName)), tool.Groups.Sum(g => g.Cards.Count));
    }

    [TestMethod]
    public void ShowIVs_IsOffByDefault()
    {
        Assert.IsFalse(new ParentCombosToolViewModel(null!).ShowIVs);
    }

    [TestMethod]
    public void OnSelected_ClearsChildAndKeepsSort()
    {
        var tool = RunWithPenkingAndBushi();
        tool.Child = Pal("Sibelyx");
        tool.SelectedSort = tool.SortOptions.Single(o => o.Sort == ParentComboSort.ZToA);

        tool.OnSelected();

        Assert.IsNull(tool.Child);
        Assert.AreEqual(0, tool.Groups.Count);
        Assert.AreEqual(ParentComboSort.ZToA, tool.SelectedSort.Sort);
    }

    [TestMethod]
    public void Child_ReadyCardShowsOwnedParentsAndChildIVs()
    {
        var penking = Owned("Penking", PalGender.MALE);
        var bushi = Owned("Bushi", PalGender.FEMALE);
        penking.IV_HP = 20;
        bushi.IV_HP = 70;
        var tool = Run(penking, bushi);

        tool.Child = Pal("Sibelyx");

        var card = tool.Groups.First().Cards.Single();
        var range = (IVRangeValueViewModel)card.IVs.HP;
        Assert.IsTrue(card.Parent1.IsOwned && card.Parent2.IsOwned);
        Assert.AreEqual((20, 70), (range.Min, range.Max));
    }

    [TestMethod]
    public void Child_MarksParentWhichNeedsReverser()
    {
        var tool = Run(useGenderReversers: true, Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.MALE));

        tool.Child = Pal("Sibelyx");

        var card = tool.Groups.First().Cards.Single();
        Assert.AreEqual(2, card.Combo.NumOwnedParents);
        Assert.AreEqual(1, new[] { card.Parent1, card.Parent2 }.Count(p => p.NeedsReverser));
    }

    [TestMethod]
    public void Child_ParentsAreInAlphabeticalOrder()
    {
        var tool = RunWithPenkingAndBushi();

        tool.Child = Pal("Sibelyx");

        foreach (var card in tool.Groups.SelectMany(g => g.Cards))
            Assert.IsTrue(string.Compare(card.Parent1.Pal.Name.Value, card.Parent2.Pal.Name.Value, StringComparison.CurrentCultureIgnoreCase) <= 0);
    }

    [TestMethod]
    public void Child_MissingParentHasHint()
    {
        var jormuntide = Owned("Jormuntide", PalGender.MALE);
        jormuntide.IV_HP = 80;
        var tool = Run(jormuntide);

        tool.Child = Pal("Faleris Aqua");

        var card = tool.Groups.Single(g => g.NumOwnedParents == 1).Cards.Single();
        Assert.AreEqual("Catch only", card.Parent1.Hint.Value);
        Assert.IsNull(card.Parent1.IVs);
        Assert.IsTrue(card.Parent2.IsOwned);
        Assert.IsNull(card.Parent2.Hint);
        Assert.IsNull(card.IVs);
        Assert.AreEqual(80, card.SortIVs!.Value.HP.Min);
    }

    [TestMethod]
    public void SelectedSort_PhilanthropistPicksPhilanthropistPals()
    {
        var best = Owned("Penking", PalGender.MALE);
        best.IV_HP = 90;
        var philanthropist = Owned("Penking", PalGender.MALE);
        philanthropist.PassiveSkills = [Philanthropist];
        var tool = Run(best, philanthropist, Owned("Bushi", PalGender.FEMALE));
        tool.Child = Pal("Sibelyx");

        PassiveSkillViewModel PenkingPassive() =>
            tool.Groups.First().Cards.Single().Parent2.BreedSpeedPassive;

        Assert.IsNull(PenkingPassive());

        tool.SelectedSort = tool.SortOptions.Single(o => o.Sort == ParentComboSort.MostPhilanthropist);
        Assert.AreEqual(Philanthropist, PenkingPassive().ModelObject);

        tool.SelectedSort = tool.SortOptions.Single(o => o.Sort == ParentComboSort.HighestIVs);
        Assert.IsNull(PenkingPassive());
    }

    [TestMethod]
    public void ChildOptions_LeaveOutFlowerGumoss()
    {
        var tool = new ParentCombosToolViewModel(null!);

        Assert.IsFalse(tool.ChildOptions.Any(p => p.ModelObject.InternalName == BreedableCalculator.HiddenPalInternalName));
        Assert.AreEqual(DB.Pals.Count() - 1, tool.ChildOptions.Count);
    }

    [TestMethod]
    public void Child_IsLoadingUntilEveryCardIsShown()
    {
        var tool = RunWithPenkingAndBushi();
        var changes = new List<(bool IsLoading, int NumShown)>();
        tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ParentCombosToolViewModel.IsLoading))
                changes.Add((tool.IsLoading, tool.Groups.Sum(g => g.Cards.Count)));
        };

        tool.Child = Pal("Sibelyx");

        CollectionAssert.AreEqual(new[] { (true, 0), (false, tool.Groups.Sum(g => g.AllCards.Count)) }, changes);
    }

    [TestMethod]
    public void Child_NewChildReplacesOneStillLoading()
    {
        var tool = RunWithPenkingAndBushi();
        var hooked = false;

        // (picks another child as soon as the first adds a card, like picking one between batches in the app)
        tool.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ParentCombosToolViewModel.Groups) || tool.Groups.Count == 0 || hooked) return;
            hooked = true;
            tool.Groups[0].Cards.CollectionChanged += (_, _) =>
            {
                if (tool.Child.ModelObject == "Sibelyx".ToPal(DB)) tool.Child = Pal("Lamball");
            };
        };

        tool.Child = Pal("Sibelyx");

        Assert.AreEqual("Lamball".ToPal(DB), tool.Child.ModelObject);
        Assert.IsFalse(tool.IsLoading);
        Assert.IsTrue(tool.Groups.SelectMany(g => g.AllCards).All(c => c.Combo.Source.Child == "Lamball".ToPal(DB)));
        Assert.IsTrue(tool.Groups.All(g => g.Cards.SequenceEqual(g.AllCards)));
    }

    [TestMethod]
    public void Sorted_HighestIVsPutsUnreachableLast()
    {
        var cards = new[]
        {
            Card("Penking", "Bushi", null),
            Card("Lamball", "Cattiva", IVs(10, 10, 10)),
            Card("Chikipi", "Foxparks", IVs(50, 50, 50)),
        };

        AssertOrder(ParentComboSort.HighestIVs, cards, "Chikipi", "Lamball", "Penking");
    }

    [TestMethod]
    public void Sorted_ByName()
    {
        var cards = new[]
        {
            Card("Lamball", "Cattiva", IVs(10, 10, 10)),
            Card("Penking", "Bushi", IVs(10, 10, 10)),
            Card("Chikipi", "Foxparks", IVs(10, 10, 10)),
        };

        AssertOrder(ParentComboSort.AToZ, cards, "Chikipi", "Lamball", "Penking");
        AssertOrder(ParentComboSort.ZToA, cards, "Penking", "Lamball", "Chikipi");
    }

    [TestMethod]
    public void Sorted_SingleStatThenAverage()
    {
        var cards = new[]
        {
            Card("Lamball", "Cattiva", IVs(60, 0, 0)),
            Card("Penking", "Bushi", IVs(90, 0, 0)),
            Card("Chikipi", "Foxparks", IVs(60, 90, 90)),
        };

        AssertOrder(ParentComboSort.HighestHP, cards, "Penking", "Chikipi", "Lamball");
        AssertOrder(ParentComboSort.HighestAttack, cards, "Chikipi", "Penking", "Lamball");
    }

    [TestMethod]
    public void Sorted_MostPhilanthropistThenAverage()
    {
        var cards = new[]
        {
            Card("Lamball", "Cattiva", IVs(90, 90, 90)),
            Card("Penking", "Bushi", IVs(10, 10, 10), numPhilanthropists: 2),
            Card("Chikipi", "Foxparks", IVs(50, 50, 50), numPhilanthropists: 1),
            Card("Anubis", "Bushi", IVs(10, 10, 10), numPhilanthropists: 1),
        };

        AssertOrder(ParentComboSort.MostPhilanthropist, cards, "Penking", "Chikipi", "Anubis", "Lamball");
    }

    private static readonly PalDB DB = PalDB.LoadEmbedded();
    private static readonly PalBreedingDB BreedingDB = PalBreedingDB.LoadEmbedded(DB);

    private static PalViewModel Pal(string name) => PalViewModel.Make(name.ToPal(DB));

    private static ParentCombosToolViewModel Run(params PalInstance[] ownedPals) => Run(false, ownedPals);

    private static ParentCombosToolViewModel Run(bool useGenderReversers, params PalInstance[] ownedPals)
    {
        var tool = new ParentCombosToolViewModel(null!);
        tool.OnSelected();
        tool.Run(Settings(useGenderReversers, ownedPals), null!);
        return tool;
    }

    private static ParentCombosToolViewModel RunWithPenkingAndBushi() =>
        Run(Owned("Penking", PalGender.MALE), Owned("Bushi", PalGender.FEMALE));

    private static IV_Set IVs(int hp, int attack, int defense) =>
        new(
            HP: new(IsRelevant: true, Min: hp, Max: hp),
            Attack: new(IsRelevant: true, Min: attack, Max: attack),
            Defense: new(IsRelevant: true, Min: defense, Max: defense)
        );

    private static readonly PassiveSkill Philanthropist = "Philanthropist".ToStandardPassive(DB);

    // (only the names, IVs and passives matter for sorting, so the breeding pair doesn't need to be real)
    private static ParentComboCardViewModel Card(string parent1, string parent2, IV_Set? ivs, int numPhilanthropists = 0)
    {
        var source = BreedingDB.Breeding.First();
        PalInstance WithPhilanthropist(int index) =>
            numPhilanthropists > index ? new() { Pal = source.Parent1.Pal, PassiveSkills = [Philanthropist] } : null;

        return new(
            new ParentCombo(source, PalGender.MALE, WithPhilanthropist(0), WithPhilanthropist(1), ParentAvailability.Owned, ParentAvailability.Owned, ivs),
            new BreedableParentViewModel(null!, null!, parent1.ToPal(DB), null),
            new BreedableParentViewModel(null!, null!, parent2.ToPal(DB), null)
        );
    }

    private static void AssertOrder(ParentComboSort sort, IEnumerable<ParentComboCardViewModel> cards, params string[] firstParents) =>
        CollectionAssert.AreEqual(
            firstParents,
            ParentCombosToolViewModel.Sorted(cards, sort).Select(c => c.Parent1.Pal.ModelObject.Name).ToList()
        );

    private static PalInstance Owned(string name, PalGender gender) => new()
    {
        InstanceId = Guid.NewGuid().ToString(),
        Pal = name.ToPal(DB),
        Gender = gender,
    };

    private static BreedingSolverSettings Settings(bool useGenderReversers, params PalInstance[] ownedPals) =>
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
            useGenderReversers: useGenderReversers,
            specialCakePolicy: SpecialCakePolicy.Never,
            trainPals: true
        );
}
