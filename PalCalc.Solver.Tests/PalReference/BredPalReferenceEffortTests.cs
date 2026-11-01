using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Processing.Attacks;

namespace PalCalc.Solver.Tests.PalReference;

[TestClass]
public class BredPalReferenceEffortTests
{
    [TestMethod]
    public void SameEggSize_SingleFarm_EggsHatchTogether()
    {
        var settings = new GameSettings();

        Assert.AreEqual(
            TimeSpan.FromMinutes(140),
            Combine(settings, Bred(settings, "Frostallion"), Bred(settings, "Frostallion"))
        );
    }

    [TestMethod]
    public void MixedEggSizes_SingleFarm_LongestIncubationStartsFirst()
    {
        var settings = new GameSettings();
        var huge = Bred(settings, "Frostallion");
        var normal = Bred(settings, "Lamball");

        Assert.AreEqual(TimeSpan.FromMinutes(130), Combine(settings, huge, normal));
        Assert.AreEqual(TimeSpan.FromMinutes(130), Combine(settings, normal, huge));
    }

    [TestMethod]
    public void MultipleFarms_ParentsBreedTogether()
    {
        var settings = new GameSettings { MultipleBreedingFarms = true };

        Assert.AreEqual(
            TimeSpan.FromMinutes(130),
            Combine(settings, Bred(settings, "Frostallion"), Bred(settings, "Frostallion"))
        );
    }

    [TestMethod]
    public void SingleIncubator_ParentEffortsAreAdded()
    {
        var settings = new GameSettings { MultipleIncubators = false };

        Assert.AreEqual(
            TimeSpan.FromMinutes(260),
            Combine(settings, Bred(settings, "Frostallion"), Bred(settings, "Frostallion"))
        );
    }

    [TestMethod]
    public void WildParent_EffortsAreAdded()
    {
        var settings = new GameSettings();
        var bred = Bred(settings, "Frostallion");
        var wild = new WildPalReference("Lamball".ToPal(SolverTestScenario.DB), [], 1, SolverTestScenario.DB.BreedingMechanics, AttackProfile.Inactive, null);

        Assert.AreEqual(bred.BreedingEffort + wild.BreedingEffort, Combine(settings, bred, wild));
    }

    private static TimeSpan Combine(GameSettings settings, IPalReference parent1, IPalReference parent2) =>
        BredPalReferenceEffort.CombineParentEffort(settings, parent1, parent2);

    private static BredPalReference Bred(GameSettings settings, string palName) =>
        new(
            settings,
            palName.ToPal(SolverTestScenario.DB),
            PalGender.WILDCARD,
            Owned(palName, PalGender.MALE),
            Owned(palName, PalGender.FEMALE),
            null,
            [],
            1,
            1,
            new IV_Set(),
            1,
            AttackProfile.Inactive,
            null
        );

    private static OwnedPalReference Owned(string palName, PalGender gender) =>
        new(
            SolverTestScenario.Owned(palName, gender),
            effectivePassives: [],
            effectiveIVs: new IV_Set(),
            attackProfile: AttackProfile.Inactive
        );
}
