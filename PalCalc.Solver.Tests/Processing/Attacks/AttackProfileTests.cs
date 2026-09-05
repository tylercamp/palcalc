using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackProfileTests
{
    [TestMethod]
    public void Accumulator_KeepsOneMinimumCakeChampionPerExactMask()
    {
        var profile = Build(Entry(mask: 1, cakes: 2), Entry(mask: 1, cakes: 1));

        Assert.AreEqual(new AttackProfileEntry(1, 1), profile.Entries.Single());
    }

    [TestMethod]
    public void Accumulator_EqualCakeEntriesCollapse()
    {
        var profile = Build(Entry(mask: 1, cakes: 2), Entry(mask: 1, cakes: 2));

        Assert.AreEqual(1, profile.Entries.Count);
    }

    [TestMethod]
    public void Accumulator_DifferentExactMasksRemainIndependent()
    {
        var profile = Build(Entry(mask: 0b01, cakes: 3), Entry(mask: 0b10, cakes: 1));

        CollectionAssert.AreEquivalent(
            new byte[] { 0b01, 0b10 },
            profile.Entries.Select(entry => entry.LearnedTargetMask).ToArray()
        );
    }

    [TestMethod]
    public void Accumulator_CouldImproveOnlyForALowerCakeCost()
    {
        var accumulator = new AttackProfileAccumulator();
        accumulator.Reset(hasNoop: false);
        accumulator.Add(Entry(mask: 1, cakes: 2));

        Assert.IsFalse(accumulator.CouldImprove(mask: 1, totalSpecialCakes: 3));
        Assert.IsFalse(accumulator.CouldImprove(mask: 1, totalSpecialCakes: 2));
        Assert.IsTrue(accumulator.CouldImprove(mask: 1, totalSpecialCakes: 1));
        Assert.IsTrue(accumulator.CouldImprove(mask: 2, totalSpecialCakes: 3));
    }

    [TestMethod]
    public void Accumulator_PreservesNoopState()
    {
        var profile = Build(hasNoop: true, Entry(mask: 1, cakes: 2));

        Assert.IsTrue(profile.HasNoopAttack);
        Assert.AreEqual(1, profile.Entries.Single().LearnedTargetMask);
    }

    [TestMethod]
    public void Accumulator_ReusesTheSharedEmptyEntryArray()
    {
        var first = Build();
        var second = Build();

        Assert.AreSame(first.Entries, second.Entries);
    }

    [TestMethod]
    public void Profile_EqualityAndHashUseOnlyNewProfileData()
    {
        var left = new AttackProfile(true, Entry(mask: 1, cakes: 2));
        var equal = new AttackProfile(true, Entry(mask: 1, cakes: 2));
        var differentCake = new AttackProfile(true, Entry(mask: 1, cakes: 3));
        var differentNoop = new AttackProfile(false, Entry(mask: 1, cakes: 2));

        Assert.AreEqual(left, equal);
        Assert.AreEqual(left.GetHashCode(), equal.GetHashCode());
        Assert.AreNotEqual(left, differentCake);
        Assert.AreNotEqual(left, differentNoop);
    }

    [TestMethod]
    public void Profile_InactiveIsDistinctFromAnActiveZeroMask()
    {
        var inactive = AttackProfile.Inactive;
        var active = new AttackProfile(Entry(mask: 0, cakes: 0));

        Assert.AreEqual(0, inactive.Entries.Count);
        Assert.AreEqual(1, active.Entries.Count);
        Assert.IsFalse(inactive.Contains(0));
        Assert.IsTrue(active.Contains(0));
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

    private static AttackProfileEntry Entry(byte mask, int cakes) => new(mask, cakes);

    private static AttackProfile Build(params AttackProfileEntry[] entries) => Build(false, entries);

    private static AttackProfile Build(bool hasNoop, params AttackProfileEntry[] entries)
    {
        var accumulator = new AttackProfileAccumulator();
        accumulator.Reset(hasNoop);
        foreach (ref readonly var entry in entries.AsSpan())
            accumulator.Add(entry);
        return accumulator.Build();
    }

    private static GameSettings Settings() => new()
    {
        BreedingTime = TimeSpan.FromMinutes(5),
        MassiveEggIncubationTime = TimeSpan.FromHours(2),
        MultipleIncubators = true,
    };

    private static TimeSpan SelfEffort(GameSettings settings, Pal pal, int breedings) =>
        BredPalReferenceEffort.CalculateSelfBreedingEffort(settings, pal, 1, 0.5f, breedings);
}
