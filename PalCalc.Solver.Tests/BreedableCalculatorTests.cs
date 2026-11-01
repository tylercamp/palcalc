using PalCalc.Model;

namespace PalCalc.Solver.Tests;

[TestClass]
public class BreedableCalculatorTests
{
    [TestMethod]
    public void Calculate_NoOwnedPalsHasNoEntries()
    {
        var result = Calculate(useGenderReversers: true);

        Assert.AreEqual(0, result.Entries.Count);
        Assert.AreEqual(DB.Pals.Count(), result.NotReachable.Count);
    }

    [TestMethod]
    public void Calculate_SingleOwnedSpeciesOnlyReachesItself()
    {
        var result = Calculate(true, Male("Penking"), Female("Penking"));

        var entry = result.Entries.Single();
        Assert.AreEqual("Penking".ToPal(DB), entry.Pal);
        Assert.AreEqual(0, entry.Tier);
        Assert.IsNull(entry.Source);
    }

    [TestMethod]
    public void Calculate_OwnedPairProducesChildAtTierOne()
    {
        var result = Calculate(false, Male("Penking"), Female("Bushi"));

        var entry = Entry(result, "Sibelyx");
        Assert.AreEqual(1, entry.Tier);
        AssertParents(entry, "Penking", "Bushi");
    }

    [TestMethod]
    public void Calculate_ChainedPairsIncreaseTier()
    {
        var parent = "Sibelyx".ToPal(DB);
        var partner = "Penking".ToPal(DB);
        var child = BreedingDB.BreedingByParent[parent][partner].Single().Child;

        var result = Calculate(false, Male("Penking"), Female("Bushi"));

        Assert.AreEqual(2, Entry(result, child.Name).Tier);
        Assert.IsTrue(Entry(result, child.Name).Source!.Parents.Any(p => p.Pal == parent));
    }

    [TestMethod]
    public void Calculate_OwnedSpeciesStaysTierZeroWhenAlsoBreedable()
    {
        var result = Calculate(false, Male("Penking"), Female("Bushi"), Male("Sibelyx"));

        Assert.AreEqual(0, Entry(result, "Sibelyx").Tier);
        Assert.IsNull(Entry(result, "Sibelyx").Source);
    }

    [TestMethod]
    public void Calculate_GenderReversersAllowPairingSameGenders()
    {
        var result = Calculate(true, Male("Penking"), Male("Bushi"));

        Assert.AreEqual(1, Entry(result, "Sibelyx").Tier);
    }

    [TestMethod]
    public void Calculate_SameGendersCannotPairWithoutReversers()
    {
        var result = Calculate(false, Male("Penking"), Male("Bushi"));

        Assert.AreEqual(2, result.Entries.Count);
        Assert.IsNull(Entry(result, "Sibelyx"));
    }

    [TestMethod]
    public void Calculate_BredPalHasEitherGender()
    {
        // "Sibelyx" is bred at tier 1, so it can provide the female needed by the male-only "Anubis"
        var sibelyx = "Sibelyx".ToPal(DB);
        var anubis = "Anubis".ToPal(DB);
        var child = BreedingDB.BreedingByParent[sibelyx][anubis].Single().Child;

        var result = Calculate(false, Male("Penking"), Female("Bushi"), Male("Anubis"));

        Assert.AreEqual(2, Entry(result, child.Name).Tier);
    }

    [TestMethod]
    public void Calculate_OwnedPalGainsGenderOnceBred()
    {
        // only "Anubis" and "Elgrove Cryst" can pair, which produces another "Anubis" of either gender. that female
        // can then pair with the male "Dualith" one generation later
        var result = Calculate(false, Male("Anubis"), Female("Elgrove Cryst"), Male("Dualith"));

        Assert.AreEqual(0, Entry(result, "Anubis").Tier);

        var entry = Entry(result, "Astegon");
        Assert.AreEqual(2, entry.Tier);
        AssertParents(entry, "Anubis", "Dualith");
    }

    [TestMethod]
    public void Calculate_GenderSpecificPairsRespectGenders()
    {
        var result = Calculate(false, Female("Katress"), Male("Wixen"));

        Assert.AreEqual(1, Entry(result, "Katress Ignis").Tier);
        Assert.AreNotEqual(1, Entry(result, "Wixen Noct")?.Tier);
    }

    [TestMethod]
    public void Calculate_GenderReversersAllowEitherGenderSpecificPair()
    {
        var result = Calculate(true, Male("Katress"), Male("Wixen"));

        Assert.AreEqual(1, Entry(result, "Katress Ignis").Tier);
        Assert.AreEqual(1, Entry(result, "Wixen Noct").Tier);
    }

    [TestMethod]
    public void Calculate_IgnoresPalsWithoutGenderLikeSolver()
    {
        var penking = SolverTestScenario.Owned("Penking", PalGender.NONE);

        foreach (var useGenderReversers in new[] { true, false })
        {
            var result = Calculate(useGenderReversers, penking, Female("Bushi"));

            Assert.IsNull(Entry(result, "Penking"));
            Assert.IsNull(Entry(result, "Sibelyx"));
        }
    }

    [TestMethod]
    public void Calculate_SelfOnlySpeciesIsNotReachableUnlessOwned()
    {
        var jetragon = "Jetragon".ToPal(DB);
        // (some display names are shared between pals, so these can't be looked up by name)
        var others = DB.Pals
            .Where(p => p != jetragon)
            .Select(p => new PalInstance { Pal = p, Gender = PalGender.MALE })
            .ToArray();

        var withoutJetragon = Calculate(true, others);
        CollectionAssert.AreEqual(new[] { jetragon }, withoutJetragon.NotReachable.ToList());

        var withJetragon = Calculate(true, [.. others, Male("Jetragon")]);
        Assert.AreEqual(0, withJetragon.NotReachable.Count);
        Assert.AreEqual(0, Entry(withJetragon, "Jetragon").Tier);
    }

    [TestMethod]
    public void Calculate_ParentUsesHighestIVsOfRequiredGender()
    {
        var low = WithIVs("Penking", PalGender.MALE, 10, 10, 10);
        var high = WithIVs("Penking", PalGender.MALE, 50, 50, 50);
        var wrongGender = WithIVs("Penking", PalGender.FEMALE, 100, 100, 100);
        var bushi = WithIVs("Bushi", PalGender.FEMALE, 0, 0, 0);

        var entry = Entry(Calculate(false, low, high, wrongGender, bushi), "Sibelyx");

        Assert.AreSame(high, InstanceFor(entry, "Penking"));
        Assert.AreSame(bushi, InstanceFor(entry, "Bushi"));
    }

    [TestMethod]
    public void Calculate_ParentComparesAllThreeIVs()
    {
        var oneHighIV = WithIVs("Penking", PalGender.MALE, 100, 0, 0);
        var balanced = WithIVs("Penking", PalGender.MALE, 40, 40, 40);

        var entry = Entry(Calculate(false, oneHighIV, balanced, Female("Bushi")), "Sibelyx");

        Assert.AreSame(balanced, InstanceFor(entry, "Penking"));
    }

    [TestMethod]
    public void Calculate_ParentsUseBestGenderArrangement()
    {
        var bestPenking = WithIVs("Penking", PalGender.FEMALE, 90, 90, 90);
        var bestBushi = WithIVs("Bushi", PalGender.MALE, 90, 90, 90);

        var entry = Entry(
            Calculate(
                false,
                WithIVs("Penking", PalGender.MALE, 20, 20, 20),
                bestPenking,
                bestBushi,
                WithIVs("Bushi", PalGender.FEMALE, 10, 10, 10)
            ),
            "Sibelyx"
        );

        Assert.AreSame(bestPenking, InstanceFor(entry, "Penking"));
        Assert.AreSame(bestBushi, InstanceFor(entry, "Bushi"));
    }

    [TestMethod]
    public void Calculate_ParentWildcardMatchesEitherGender()
    {
        var wildcard = WithIVs("Penking", PalGender.WILDCARD, 80, 80, 80);

        var entry = Entry(Calculate(false, wildcard, Female("Bushi")), "Sibelyx");

        Assert.AreSame(wildcard, InstanceFor(entry, "Penking"));
    }

    [TestMethod]
    public void Calculate_ParentRespectsGenderSpecificPairs()
    {
        var female = WithIVs("Katress", PalGender.FEMALE, 10, 10, 10);
        var strongerMale = WithIVs("Katress", PalGender.MALE, 90, 90, 90);

        var entry = Entry(Calculate(false, female, strongerMale, Male("Wixen")), "Katress Ignis");

        Assert.AreSame(female, InstanceFor(entry, "Katress"));
    }

    [TestMethod]
    public void Calculate_ParentWithEqualIVsIsStable()
    {
        var first = WithIVs("Penking", PalGender.MALE, 50, 50, 50);
        var second = WithIVs("Penking", PalGender.MALE, 50, 50, 50);
        var bushi = Female("Bushi");

        Assert.AreSame(
            InstanceFor(Entry(Calculate(false, first, second, bushi), "Sibelyx"), "Penking"),
            InstanceFor(Entry(Calculate(false, second, first, bushi), "Sibelyx"), "Penking")
        );
    }

    [TestMethod]
    public void Calculate_BredParentHasNoInstance()
    {
        var sibelyx = "Sibelyx".ToPal(DB);
        var partner = "Penking".ToPal(DB);
        var child = BreedingDB.BreedingByParent[sibelyx][partner].Single().Child;
        var penking = Male("Penking");

        var entry = Entry(Calculate(false, penking, Female("Bushi")), child.Name);

        Assert.IsNull(InstanceFor(entry, "Sibelyx"));
        Assert.AreSame(penking, InstanceFor(entry, "Penking"));
    }

    [TestMethod]
    public void Calculate_ParentWithoutRequiredGenderNeedsBreeding()
    {
        // only male "Anubis" is owned, the female needed here has to be bred first
        var dualith = Male("Dualith");

        var entry = Entry(Calculate(false, Male("Anubis"), Female("Elgrove Cryst"), dualith), "Astegon");

        Assert.IsNull(InstanceFor(entry, "Anubis"));
        Assert.AreSame(dualith, InstanceFor(entry, "Dualith"));
    }

    [TestMethod]
    public void Calculate_GenderReversersStillPreferRequiredGender()
    {
        var male = WithIVs("Penking", PalGender.MALE, 10, 10, 10);
        var strongerFemale = WithIVs("Penking", PalGender.FEMALE, 90, 90, 90);
        var bushi = Female("Bushi");

        var entry = Entry(Calculate(true, male, strongerFemale, bushi), "Sibelyx");

        Assert.AreSame(male, InstanceFor(entry, "Penking"));
        Assert.AreSame(bushi, InstanceFor(entry, "Bushi"));
    }

    [TestMethod]
    public void Calculate_GenderReversersFallBackToHighestIVsOfOtherGender()
    {
        var weak = WithIVs("Penking", PalGender.MALE, 10, 10, 10);
        var strong = WithIVs("Penking", PalGender.MALE, 90, 90, 90);
        var bushi = Male("Bushi");

        var entry = Entry(Calculate(true, weak, strong, bushi), "Sibelyx");

        Assert.AreSame(strong, InstanceFor(entry, "Penking"));
        Assert.AreSame(bushi, InstanceFor(entry, "Bushi"));
    }

    [TestMethod]
    public void Calculate_GenderReversersDoNotChooseBredParent()
    {
        var sibelyx = "Sibelyx".ToPal(DB);
        var partner = "Penking".ToPal(DB);
        var child = BreedingDB.BreedingByParent[sibelyx][partner].Single().Child;
        var penking = Male("Penking");

        var entry = Entry(Calculate(true, penking, Female("Bushi")), child.Name);

        Assert.IsNull(InstanceFor(entry, "Sibelyx"));
        Assert.AreSame(penking, InstanceFor(entry, "Penking"));
    }

    [TestMethod]
    public void Calculate_ChildIVsSpanBothParents()
    {
        var penking = WithIVs("Penking", PalGender.MALE, 10, 60, 30);
        var bushi = WithIVs("Bushi", PalGender.FEMALE, 40, 20, 30);

        var ivs = Entry(Calculate(false, penking, bushi), "Sibelyx").IVs;

        Assert.AreEqual((10, 40), (ivs.HP.Min, ivs.HP.Max));
        Assert.AreEqual((20, 60), (ivs.Attack.Min, ivs.Attack.Max));
        Assert.AreEqual((30, 30), (ivs.Defense.Min, ivs.Defense.Max));
    }

    [TestMethod]
    public void Calculate_BredSpeciesHasNoOwnedInstance()
    {
        var entry = Entry(Calculate(false, Male("Penking"), Female("Bushi")), "Sibelyx");

        Assert.IsNull(entry.OwnedInstance);
    }

    [TestMethod]
    public void Calculate_BredParentCarriesIVRangeForward()
    {
        var sibelyx = "Sibelyx".ToPal(DB);
        var partner = "Penking".ToPal(DB);
        var child = BreedingDB.BreedingByParent[sibelyx][partner].Single().Child;
        var penking = WithIVs("Penking", PalGender.MALE, 10, 10, 10);
        var bushi = WithIVs("Bushi", PalGender.FEMALE, 90, 90, 90);

        var ivs = Entry(Calculate(false, penking, bushi), child.Name).IVs;

        Assert.AreEqual((10, 90), (ivs.HP.Min, ivs.HP.Max));
    }

    [TestMethod]
    public void Calculate_OwnedSpeciesUsesHighestIVs()
    {
        var low = WithIVs("Penking", PalGender.MALE, 10, 10, 10);
        var high = WithIVs("Penking", PalGender.FEMALE, 50, 60, 70);

        var entry = Entry(Calculate(false, low, high), "Penking");
        var ivs = entry.IVs;

        Assert.AreSame(high, entry.OwnedInstance);
        Assert.AreEqual((50, 50), (ivs.HP.Min, ivs.HP.Max));
        Assert.AreEqual((60, 60), (ivs.Attack.Min, ivs.Attack.Max));
        Assert.AreEqual((70, 70), (ivs.Defense.Min, ivs.Defense.Max));
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Calculate_EverySpeciesAppearsExactlyOnce(bool useGenderReversers)
    {
        var result = Calculate(useGenderReversers, Male("Penking"), Female("Bushi"), Female("Lamball"));

        CollectionAssert.AreEquivalent(
            DB.Pals.ToList(),
            result.Entries.Select(e => e.Pal).Concat(result.NotReachable).ToList()
        );
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Calculate_SourcesComeFromEarlierTiers(bool useGenderReversers)
    {
        var result = Calculate(useGenderReversers, Male("Penking"), Female("Bushi"), Female("Lamball"));
        var tiers = result.Entries.ToDictionary(e => e.Pal, e => e.Tier);

        foreach (var entry in result.Entries.Where(e => e.Tier > 0))
        {
            Assert.AreEqual(entry.Pal, entry.Source!.Child);
            Assert.IsTrue(entry.Source.Parents.All(p => tiers[p.Pal] < entry.Tier));
        }
    }

    [TestMethod]
    public void Calculate_ResultsAreStableBetweenRuns()
    {
        PalInstance[] owned = [Male("Penking"), Female("Bushi"), Female("Lamball")];

        CollectionAssert.AreEqual(
            Calculate(true, owned).Entries.ToList(),
            Calculate(true, owned.Reverse().ToArray()).Entries.ToList()
        );
    }

    private static readonly PalDB DB = SolverTestScenario.DB;
    private static readonly PalBreedingDB BreedingDB = PalBreedingDB.LoadEmbedded(DB);

    private static PalInstance Male(string name) => SolverTestScenario.Owned(name, PalGender.MALE);
    private static PalInstance Female(string name) => SolverTestScenario.Owned(name, PalGender.FEMALE);

    private static PalInstance WithIVs(string name, PalGender gender, int hp, int attack, int defense) =>
        SolverTestScenario.Owned(name, gender, ivHp: hp, ivAttack: attack, ivDefense: defense);

    private static BreedableResult Calculate(bool useGenderReversers, params PalInstance[] ownedPals) =>
        new BreedableCalculator(
            SolverTestScenario.Solver(ownedPals, useGenderReversers: useGenderReversers).Settings
        ).Calculate();

    private static BreedableEntry Entry(BreedableResult result, string name) =>
        result.Entries.SingleOrDefault(e => e.Pal == name.ToPal(DB))!;

    private static PalInstance InstanceFor(BreedableEntry entry, string parent) =>
        entry.Source!.Parent1.Pal == parent.ToPal(DB) ? entry.Parent1Instance : entry.Parent2Instance;

    private static void AssertParents(BreedableEntry entry, string parent1, string parent2) =>
        CollectionAssert.AreEquivalent(
            new[] { parent1.ToPal(DB), parent2.ToPal(DB) },
            entry.Source!.Parents.Select(p => p.Pal).ToList()
        );
}
