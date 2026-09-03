using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackProfileTests
{
    [TestMethod]
    public void EntryComparison_PrefersFewerCakesBeforeLowerEffort()
    {
        var fewerCakes = Entry(mask: 1, cakes: 1, effort: 20, breedings: 2);
        var faster = Entry(mask: 1, cakes: 2, effort: 10, breedings: 1);

        Assert.IsTrue(AttackProfileEntryComparer.Instance.Compare(fewerCakes, faster) < 0);
    }

    [TestMethod]
    public void Reducer_FasterEqualCakeSupersetCoversSlowerSubset()
    {
        var provider = Entry(mask: 0b11, cakes: 2, effort: 10, breedings: 2);
        var required = Entry(mask: 0b01, cakes: 2, effort: 11, breedings: 3);

        Assert.IsTrue(AttackProfileReducer.Covers(provider, required));
        Assert.AreEqual(1, AttackProfileReducer.Reduce([required, provider]).Entries.Count);
    }

    [TestMethod]
    public void Reducer_FasterCakeHeavierEntryDoesNotCoverLowerCakeEntry()
    {
        var provider = Entry(mask: 0b11, cakes: 3, effort: 10, breedings: 2);
        var required = Entry(mask: 0b01, cakes: 2, effort: 11, breedings: 3);

        Assert.IsFalse(AttackProfileReducer.Covers(provider, required));
    }

    [TestMethod]
    public void Reducer_NonCakeEntryCoversOtherwiseEqualCakeEntry()
    {
        var nonCake = Entry(mask: 1, cakes: 2, effort: 10, breedings: 2, usesCake: false);
        var cake = Entry(mask: 1, cakes: 2, effort: 10, breedings: 2, usesCake: true);

        Assert.IsTrue(AttackProfileReducer.Covers(nonCake, cake));
    }

    [TestMethod]
    public void Reducer_CakeEntryDoesNotCoverOtherwiseEqualNonCakeEntry()
    {
        var cake = Entry(mask: 1, cakes: 2, effort: 10, breedings: 2, usesCake: true);
        var nonCake = Entry(mask: 1, cakes: 2, effort: 10, breedings: 2, usesCake: false);

        Assert.IsFalse(AttackProfileReducer.Covers(cake, nonCake));
    }

    [TestMethod]
    public void Reducer_LargeProfileUsesSameCoverageRules()
    {
        var duplicate = Entry(mask: 1, cakes: 2, effort: 10, breedings: 2);
        var profile = AttackProfileReducer.Reduce(Enumerable.Repeat(duplicate, 257).ToArray());

        Assert.AreEqual(1, profile.Entries.Count);
        Assert.AreEqual(duplicate, profile.Entries.Single());
    }

    [TestMethod]
    public void Reducer_KeepsAtMostOneChampionPerMask()
    {
        var random = new Random(1729);
        for (var sample = 0; sample < 100; sample++)
        {
            var entries = Enumerable.Range(0, random.Next(1, 65))
                .Select(_ => Entry(
                    mask: (byte)random.Next(64),
                    cakes: random.Next(5),
                    effort: random.Next(20),
                    breedings: random.Next(5),
                    usesCake: random.Next(2) == 0
                ))
                .ToArray();

            var reduced = AttackProfileReducer.Reduce(entries).Entries;

            Assert.IsTrue(reduced.Count <= 64, $"Sample {sample}");
            Assert.AreEqual(reduced.Count, reduced.Select(entry => entry.LearnedTargetMask).Distinct().Count());
        }
    }

    [TestMethod]
    public void Accumulator_CanPreserveOriginalEntryChosenByAdjustedCosts()
    {
        var accumulator = new AttackProfileReducer.Accumulator();
        accumulator.Reset(hasNoop: false);
        var ordinaryChampion = Entry(mask: 1, cakes: 1, effort: 10, breedings: 1);
        var genderChampion = Entry(mask: 1, cakes: 2, effort: 20, breedings: 2);

        accumulator.Add(ordinaryChampion, Entry(mask: 1, cakes: 3, effort: 30, breedings: 3));
        accumulator.Add(genderChampion, Entry(mask: 1, cakes: 2, effort: 25, breedings: 2));

        Assert.AreEqual(genderChampion, accumulator.Build().Entries.Single());
    }

    [TestMethod]
    public void GenderTransformation_PreservesCoverageFromLowerSelfBreedings()
    {
        var (pal, gender) = GenderedPal();
        var settings = Settings();
        var faster = Entry(mask: 1, cakes: 0, effort: 17, breedings: 1);
        var slower = Entry(mask: 1, cakes: 0, effort: 22, breedings: 2);

        var adjustedFaster = Transform(faster, settings, pal, gender);
        var adjustedSlower = Transform(slower, settings, pal, gender);

        Assert.IsTrue(AttackProfileReducer.Covers(adjustedFaster, adjustedSlower));
    }

    [TestMethod]
    public void GenderTransformation_AddsCurrentEdgeCakesOnlyForCakeEntries()
    {
        var (pal, gender) = GenderedPal();
        var settings = Settings();
        var cake = Entry(mask: 1, cakes: 5, effort: 17, breedings: 1, usesCake: true);
        var nonCake = Entry(mask: 1, cakes: 5, effort: 17, breedings: 1, usesCake: false);

        var adjustedCake = Transform(cake, settings, pal, gender);
        var adjustedNonCake = Transform(nonCake, settings, pal, gender);

        Assert.AreEqual(cake.TotalSpecialCakes + adjustedCake.SelfBreedings - cake.SelfBreedings, adjustedCake.TotalSpecialCakes);
        Assert.AreEqual(nonCake.TotalSpecialCakes, adjustedNonCake.TotalSpecialCakes);
        Assert.AreEqual(
            cake.BreedingEffort - SelfEffort(settings, pal, cake.SelfBreedings) + SelfEffort(settings, pal, adjustedCake.SelfBreedings),
            adjustedCake.BreedingEffort
        );
    }

    [TestMethod]
    public void BredEffortHelper_MatchesCharacterizedIncubatorCalculations()
    {
        var pal = SolverTestScenario.DB.Pals.First(p => p.EggSize == EggSize.Normal);
        var settings = Settings();

        Assert.AreEqual(TimeSpan.FromMinutes(25), SelfEffort(settings, pal, 3));

        settings.MultipleIncubators = false;
        Assert.AreEqual(TimeSpan.FromMinutes(35), SelfEffort(settings, pal, 3));
    }

    [TestMethod]
    public void Profile_InactiveIsDistinctFromAnActiveZeroMask()
    {
        var inactive = AttackProfile.Inactive;
        var active = new AttackProfile(Entry(mask: 0, cakes: 0, effort: 0, breedings: 0));

        Assert.AreEqual(0, inactive.Entries.Count);
        Assert.AreEqual(1, active.Entries.Count);
        Assert.IsFalse(inactive.Contains(0));
        Assert.IsTrue(active.Contains(0));
    }

    private static AttackProfileEntry Entry(byte mask, int cakes, int effort, int breedings, bool usesCake = false) =>
        new(mask, cakes, TimeSpan.FromMinutes(effort), breedings, usesCake);

    private static GameSettings Settings() => new()
    {
        BreedingTime = TimeSpan.FromMinutes(5),
        MassiveEggIncubationTime = TimeSpan.FromHours(2),
        MultipleIncubators = true,
    };

    private static TimeSpan SelfEffort(GameSettings settings, Pal pal, int breedings) =>
        BredPalReferenceEffort.CalculateSelfBreedingEffort(settings, pal, 1, 0.5f, breedings);

    private static AttackProfileEntry Transform(AttackProfileEntry entry, GameSettings settings, Pal pal, PalGender gender) =>
        entry.WithGuaranteedGender(settings, pal, 1, 0.5f, SolverTestScenario.DB, gender, useReverser: false);

    private static (Pal Pal, PalGender Gender) GenderedPal()
    {
        var pal = SolverTestScenario.DB.Pals.First(p =>
            SolverTestScenario.DB.BreedingGenderProbability[p][PalGender.MALE] < 1
        );
        return (pal, PalGender.MALE);
    }
}
