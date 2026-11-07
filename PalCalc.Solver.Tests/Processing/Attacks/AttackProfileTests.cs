using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;

namespace PalCalc.Solver.Tests.Processing.Attacks;

[TestClass]
public class AttackProfileTests
{
    [TestMethod]
    public void ProfileStoresDistinctExactMasksAndNoopState()
    {
        var profile = new AttackProfile(true, 1, 2, 1);

        Assert.IsTrue(profile.HasNoopAttack);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, profile.Masks.ToArray());
        Assert.IsFalse(profile.Contains(3));
        Assert.AreEqual(profile, new AttackProfile(true, 2, 1));
        Assert.AreNotEqual(profile, new AttackProfile(false, 1, 2));
    }

    [TestMethod]
    public void ProfileInactiveIsDistinctFromAnActiveZeroMask()
    {
        Assert.IsFalse(AttackProfile.Inactive.Contains(0));
        Assert.IsTrue(new AttackProfile(0).Contains(0));
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

    private static GameSettings Settings() => new()
    {
        BreedingTime = TimeSpan.FromMinutes(5),
        MassiveEggIncubationTime = TimeSpan.FromHours(2),
        MultipleIncubators = true,
    };

    private static TimeSpan SelfEffort(GameSettings settings, Pal pal, int breedings) =>
        BredPalReferenceEffort.CalculateSelfBreedingEffort(settings, pal, 1, 0.5f, breedings);
}
