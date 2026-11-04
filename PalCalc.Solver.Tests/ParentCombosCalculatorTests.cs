using PalCalc.Model;

namespace PalCalc.Solver.Tests;

[TestClass]
public class ParentCombosCalculatorTests
{
    [TestMethod]
    public void Calculate_OwnedPairHasBothParents()
    {
        var penking = Male("Penking");
        var bushi = Female("Bushi");

        var combo = Combo(Calculate(false, "Sibelyx", penking, bushi), "Penking", "Bushi");

        Assert.AreEqual(2, combo.NumOwnedParents);
        Assert.AreEqual(penking, InstanceFor(combo, "Penking"));
        Assert.AreEqual(bushi, InstanceFor(combo, "Bushi"));
    }

    [TestMethod]
    public void Calculate_ListsEveryPairForTheChild()
    {
        var result = Calculate(true, "Sibelyx");

        Assert.AreEqual(
            BreedingDB.Breeding.Count(b => b.Child == "Sibelyx".ToPal(DB) && b.Parents.All(p => p.Pal != FlowerGumoss)),
            result.Combos.Count
        );
        Assert.IsTrue(result.Combos.All(c => c.Source.Child == "Sibelyx".ToPal(DB)));
    }

    [TestMethod]
    public void Calculate_SameGendersCannotPairWithoutReversers()
    {
        var combo = Combo(Calculate(false, "Sibelyx", Male("Penking"), Male("Bushi")), "Penking", "Bushi");

        // (nothing else is owned, so the other gender can't be bred either)
        Assert.AreEqual(1, combo.NumOwnedParents);
        CollectionAssert.Contains(Availabilities(combo), ParentAvailability.NotBreedable);
    }

    [TestMethod]
    public void Calculate_GenderReversersAllowPairingSameGenders()
    {
        var combo = Combo(Calculate(true, "Sibelyx", Male("Penking"), Male("Bushi")), "Penking", "Bushi");

        Assert.AreEqual(2, combo.NumOwnedParents);
    }

    [DataTestMethod]
    [DataRow(PalGender.MALE)]
    [DataRow(PalGender.FEMALE)]
    public void Calculate_PrefersGendersWhichNeedNoReverser(PalGender penkingGender)
    {
        var penking = SolverTestScenario.Owned("Penking", penkingGender);
        var bushi = SolverTestScenario.Owned("Bushi", penkingGender.OppositeGender());

        var combo = Combo(Calculate(true, "Sibelyx", penking, bushi), "Penking", "Bushi");

        Assert.AreEqual(penkingGender, GenderFor(combo, "Penking"));
    }

    [TestMethod]
    public void Calculate_MissingGenderCanBeBred()
    {
        // only a male "Sibelyx" is owned, and a female can be bred from "Penking" and "Bushi"
        var child = BreedingDB.BreedingByParent["Sibelyx".ToPal(DB)]["Penking".ToPal(DB)].Single().Child;
        PalInstance[] owned = [Male("Penking"), Female("Bushi"), Male("Sibelyx")];

        var withoutReversers = Combo(Calculate(false, child.Name, owned), "Sibelyx", "Penking");

        Assert.AreEqual(1, withoutReversers.NumOwnedParents);
        CollectionAssert.Contains(Availabilities(withoutReversers), ParentAvailability.Breedable);
        Assert.AreEqual(2, Combo(Calculate(true, child.Name, owned), "Sibelyx", "Penking").NumOwnedParents);
    }

    [TestMethod]
    public void Calculate_GenderSpecificPairsNeedTheirGenders()
    {
        var wrongGenders = Combo(Calculate(false, "Katress Ignis", Male("Katress"), Female("Wixen")), "Katress", "Wixen");
        var rightGenders = Combo(Calculate(false, "Katress Ignis", Female("Katress"), Male("Wixen")), "Katress", "Wixen");

        // (the other genders can still be bred, through "Wixen Noct")
        Assert.AreEqual(0, wrongGenders.NumOwnedParents);
        Assert.IsTrue(Availabilities(wrongGenders).All(a => a == ParentAvailability.Breedable));
        Assert.AreEqual(2, rightGenders.NumOwnedParents);
        Assert.AreEqual(PalGender.FEMALE, GenderFor(rightGenders, "Katress"));
    }

    [TestMethod]
    public void Calculate_IncludesPairWithItself()
    {
        var male = Male("Penking");
        var female = Female("Penking");

        var combo = Combo(Calculate(false, "Penking", male, female), "Penking", "Penking");

        Assert.AreEqual(2, combo.NumOwnedParents);
        CollectionAssert.AreEquivalent(new[] { male, female }, new[] { combo.Parent1Instance, combo.Parent2Instance });
    }

    [TestMethod]
    public void Calculate_PairWithItselfNeedsTwoPals()
    {
        var combo = Combo(Calculate(true, "Penking", Male("Penking")), "Penking", "Penking");

        Assert.AreEqual(1, combo.NumOwnedParents);
        Assert.IsNotNull(combo.Parent1Instance);
        Assert.IsNull(combo.Parent2Instance);
    }

    [TestMethod]
    public void Calculate_ParentOnlyBredFromItselfIsCatchOnly()
    {
        var combo = Combo(Calculate(true, "Faleris Aqua", Male("Jormuntide")), "Faleris", "Jormuntide");

        Assert.AreEqual(1, combo.NumOwnedParents);
        Assert.AreEqual(ParentAvailability.CatchOnly, AvailabilityFor(combo, "Faleris"));
        Assert.IsNull(combo.IVs);
    }

    [TestMethod]
    public void Calculate_HidesFlowerGumossPairsUnlessOwned()
    {
        var pair = BreedingDB.Breeding.First(b => b.Parent1.Pal == FlowerGumoss ^ b.Parent2.Pal == FlowerGumoss);
        var partner = pair.Parent1.Pal == FlowerGumoss ? pair.Parent2.Pal : pair.Parent1.Pal;
        var partnerPal = SolverTestScenario.Owned(partner.Name, PalGender.MALE);

        var withoutFlower = new ParentCombosCalculator(Settings(true, partnerPal)).Calculate(pair.Child);
        var withFlower = new ParentCombosCalculator(Settings(true, partnerPal, new PalInstance { Pal = FlowerGumoss, Gender = PalGender.FEMALE })).Calculate(pair.Child);

        Assert.IsFalse(withoutFlower.Combos.Any(c => c.Source.Parents.Any(p => p.Pal == FlowerGumoss)));
        Assert.AreEqual(2, withFlower.Combos.Single(c => c.Source == pair).NumOwnedParents);
    }

    [TestMethod]
    public void Calculate_ChildIVsCoverBothParents()
    {
        var result = Calculate(false, "Sibelyx", WithIVs("Penking", PalGender.MALE, 10, 20, 30), WithIVs("Bushi", PalGender.FEMALE, 40, 5, 30));

        var ivs = Combo(result, "Penking", "Bushi").IVs!.Value;
        Assert.AreEqual((10, 40), (ivs.HP.Min, ivs.HP.Max));
        Assert.AreEqual((5, 20), (ivs.Attack.Min, ivs.Attack.Max));
        Assert.AreEqual((30, 30), (ivs.Defense.Min, ivs.Defense.Max));
    }

    [TestMethod]
    public void Calculate_UsesHighestIVPals()
    {
        var best = WithIVs("Penking", PalGender.MALE, 90, 90, 90);
        var result = Calculate(false, "Sibelyx", WithIVs("Penking", PalGender.MALE, 10, 10, 10), best, Female("Bushi"));

        Assert.AreEqual(best, InstanceFor(Combo(result, "Penking", "Bushi"), "Penking"));
    }

    [TestMethod]
    public void Calculate_MostOwnedParentsComeFirst()
    {
        var result = Calculate(false, "Sibelyx", Male("Penking"), Female("Bushi"), Female("Lamball"));
        var owned = result.Combos.Select(c => c.NumOwnedParents).ToList();

        CollectionAssert.AreEqual(owned.OrderDescending().ToList(), owned);
    }

    // checks every pair of every child, against the breedable result and the breeding data
    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Calculate_AvailabilityMatchesBreedableResult(bool useGenderReversers)
    {
        var settings = Settings(useGenderReversers, Male("Penking"), Female("Bushi"), Female("Lamball"), Male("Anubis"));
        var breedable = new BreedableCalculator(settings).Calculate();
        var reachable = breedable.Entries.Select(e => e.Pal).ToHashSet();
        var calculator = new ParentCombosCalculator(settings);

        foreach (var child in DB.Pals)
        {
            foreach (var combo in calculator.Calculate(child, breedable).Combos)
            {
                foreach (var (pal, instance, availability) in Parents(combo))
                {
                    Assert.AreEqual(instance != null, availability == ParentAvailability.Owned);

                    if (availability == ParentAvailability.Breedable)
                        Assert.IsTrue(reachable.Contains(pal), pal.Name);

                    if (availability == ParentAvailability.CatchOnly)
                        Assert.IsTrue(BreedingDB.Breeding.Where(b => b.Child == pal).All(b => b.Parents.All(p => p.Pal == pal)), pal.Name);
                }

                Assert.AreEqual(combo.NumOwnedParents == 2, combo.IVs != null);
            }
        }
    }

    private static readonly PalDB DB = SolverTestScenario.DB;
    private static readonly PalBreedingDB BreedingDB = PalBreedingDB.LoadEmbedded(DB);
    private static readonly Pal FlowerGumoss = "PlantSlime_Flower".InternalToPal(DB);

    private static PalInstance Male(string name) => SolverTestScenario.Owned(name, PalGender.MALE);
    private static PalInstance Female(string name) => SolverTestScenario.Owned(name, PalGender.FEMALE);

    private static PalInstance WithIVs(string name, PalGender gender, int hp, int attack, int defense) =>
        SolverTestScenario.Owned(name, gender, ivHp: hp, ivAttack: attack, ivDefense: defense);

    private static BreedingSolverSettings Settings(bool useGenderReversers, params PalInstance[] ownedPals) =>
        SolverTestScenario.Solver(ownedPals, useGenderReversers: useGenderReversers).Settings;

    private static ParentCombosResult Calculate(bool useGenderReversers, string child, params PalInstance[] ownedPals) =>
        new ParentCombosCalculator(Settings(useGenderReversers, ownedPals)).Calculate(child.ToPal(DB));

    private static ParentCombo Combo(ParentCombosResult result, string parent1, string parent2) =>
        result.Combos.Single(c =>
            c.Source.Parents.Select(p => p.Pal).OrderBy(p => p.InternalIndex).SequenceEqual(
                new[] { parent1.ToPal(DB), parent2.ToPal(DB) }.OrderBy(p => p.InternalIndex)
            )
        );

    private static (Pal, PalInstance, ParentAvailability)[] Parents(ParentCombo combo) =>
        [
            (combo.Source.Parent1.Pal, combo.Parent1Instance, combo.Parent1Availability),
            (combo.Source.Parent2.Pal, combo.Parent2Instance, combo.Parent2Availability),
        ];

    private static ParentAvailability[] Availabilities(ParentCombo combo) =>
        [combo.Parent1Availability, combo.Parent2Availability];

    private static bool IsParent1(ParentCombo combo, string parent) => combo.Source.Parent1.Pal == parent.ToPal(DB);

    private static PalInstance InstanceFor(ParentCombo combo, string parent) =>
        IsParent1(combo, parent) ? combo.Parent1Instance : combo.Parent2Instance;

    private static ParentAvailability AvailabilityFor(ParentCombo combo, string parent) =>
        IsParent1(combo, parent) ? combo.Parent1Availability : combo.Parent2Availability;

    private static PalGender GenderFor(ParentCombo combo, string parent) =>
        IsParent1(combo, parent) ? combo.Parent1Gender : combo.Parent1Gender.OppositeGender();
}
