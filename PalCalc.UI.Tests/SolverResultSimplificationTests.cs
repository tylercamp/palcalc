using PalCalc.Model;
using PalCalc.Solver.PalReference;
using PalCalc.Solver.PalReference.Properties;
using PalCalc.Solver.Utils;
using PalCalc.UI.ViewModel.Solver;

namespace PalCalc.UI.Tests;

[TestClass]
public class SolverResultSimplificationTests
{
    [TestMethod]
    public void BothSimplificationPassesPreserveSlowCakeSavingOptions()
    {
        var db = PalDB.LoadEmbedded();
        var first = Owned(db.Pals.First(), "first");
        var second = Owned(db.Pals.Last(), "second");
        var slow = Bred(100, 1);
        var middle = Bred(10, 5);
        var fast = Bred(1, 10);

        var results = SolverJobViewModel.SimplifyResults([fast, middle, slow], CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 1, 5, 10 },
            results.Select(result => PalResultProperty.SpecialCakes(result)).ToArray());
        Assert.IsTrue(slow.BreedingEffort > fast.BreedingEffort * 2);

        BredPalReference Bred(int attempts, int cakes) => new(
            new GameSettings(), db.Pals.First(), PalGender.WILDCARD, first, second,
            attempts, [], new(BreedingMethod.SpecialCake, 1), new IV_Set(), 1,
            new AttackProfile(new AttackProfileEntry(1, cakes)),
            new(AttackInheritanceMode.InheritAll, [], [], [], [], cakes, 1));
    }

    [TestMethod]
    public void InactiveProfilesRemainInTheZeroCakeGroup()
    {
        var owned = Owned(PalDB.LoadEmbedded().Pals.First(), "owned");
        Assert.AreEqual(0, PalResultProperty.SpecialCakes(owned));
        CollectionAssert.AreEqual(new IPalReference[] { owned },
            SolverJobViewModel.SimplifyResults([owned], CancellationToken.None));
    }

    private static OwnedPalReference Owned(Pal pal, string id) => new(
        new PalInstance
        {
            InstanceId = id, Pal = pal, Gender = PalGender.MALE, PassiveSkills = [],
            Location = new PalLocation { Type = LocationType.Palbox },
        }, [], new IV_Set(), AttackProfile.Inactive);
}
