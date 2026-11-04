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
    public void SimplificationPreservesSlowSpecialCakeSavingOptionsWithinTheCakeLimit()
    {
        var slow = new TestPalReference(100, 1) { AttackProfile = new(new AttackProfileEntry(1, 1)) };
        var middle = new TestPalReference(10, 1) { AttackProfile = new(new AttackProfileEntry(1, 5)) };
        var fast = new TestPalReference(1, 1) { AttackProfile = new(new AttackProfileEntry(1, 10)) };

        var results = SolverJobViewModel.SimplifyResults([fast, middle, slow], CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { 1, 5, 10 },
            results.Select(result => PalResultProperty.SpecialCakes(result)).ToArray());
        Assert.IsTrue(slow.BreedingEffort > fast.BreedingEffort * 2);
    }

    [TestMethod]
    public void CakeLimitKeepsExactlyTwiceTheFastestPathsCount()
    {
        var fast = new TestPalReference(10, 10);
        // Different location kinds keep these alternatives through the initial simplification.
        var boundary = new TestPalReference(12, 20) { Location = new CapturedRefLocation() };
        var overLimit = new TestPalReference(15, 21) { Location = SurgeryRefLocation.Instance };

        CollectionAssert.AreEquivalent(new IPalReference[] { fast, boundary },
            SolverJobViewModel.SimplifyResults([fast, boundary, overLimit], CancellationToken.None));
    }

    [TestMethod]
    public void CakeLimitUsesTheFastestPathInsteadOfTheCheapest()
    {
        var fast = new TestPalReference(10, 10);
        var cheap = new TestPalReference(15, 1) { Location = new CapturedRefLocation() };
        var boundary = new TestPalReference(12, 20) { Location = SurgeryRefLocation.Instance };
        var candidates = new IPalReference[] { cheap, boundary, fast };

        CollectionAssert.AreEquivalent(candidates,
            SolverJobViewModel.SimplifyResults(candidates, CancellationToken.None));
    }

    [TestMethod]
    public void CakeLimitPrunesAcrossDifferentStepCounts()
    {
        var fast = new TestPalReference(10, 10, steps: 1);
        var boundary = new TestPalReference(12, 20, steps: 2);
        var overLimit = new TestPalReference(15, 21, steps: 3);

        CollectionAssert.AreEquivalent(new IPalReference[] { fast, boundary },
            SolverJobViewModel.SimplifyResults([overLimit, boundary, fast], CancellationToken.None));
    }

    [TestMethod]
    public void DifferentFinalPassivesHaveIndependentCakeLimits()
    {
        var first = new TestPalReference(10, 1);
        var second = new TestPalReference(10, 100)
        {
            EffectivePassives = ["Swift".ToStandardPassive(PalDB.LoadEmbedded())],
        };
        var candidates = new IPalReference[] { first, second };

        CollectionAssert.AreEquivalent(candidates,
            SolverJobViewModel.SimplifyResults(candidates, CancellationToken.None));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TiedFastestPathsUseTheLowerCakeCountRegardlessOfOrder(bool reverse)
    {
        var first = new TestPalReference(10, 10);
        var second = new TestPalReference(10, 20) { Location = new CapturedRefLocation() };
        var overLimit = new TestPalReference(12, 21) { Location = SurgeryRefLocation.Instance };
        var candidates = new IPalReference[] { first, second, overLimit };
        if (reverse) Array.Reverse(candidates);

        CollectionAssert.AreEquivalent(new IPalReference[] { first, second },
            SolverJobViewModel.SimplifyResults(candidates, CancellationToken.None));
    }

    [TestMethod]
    public void ZeroCakeFastestPathExcludesCakeUsingAlternatives()
    {
        var owned = Owned(PalDB.LoadEmbedded().Pals.First(), "owned");

        CollectionAssert.AreEqual(new IPalReference[] { owned },
            SolverJobViewModel.SimplifyResults([new TestPalReference(10, 1), owned], CancellationToken.None));
    }

    [TestMethod]
    public void DoublingCakeLimitDoesNotOverflow()
    {
        var first = new TestPalReference(10, int.MaxValue);
        var second = new TestPalReference(12, int.MaxValue - 1) { Location = new CapturedRefLocation() };
        var candidates = new IPalReference[] { first, second };

        CollectionAssert.AreEquivalent(candidates,
            SolverJobViewModel.SimplifyResults(candidates, CancellationToken.None));
    }

    [TestMethod]
    public void TimeCutoffStillAppliesWithinTheCakeLimit()
    {
        var fast = new TestPalReference(10, 10, steps: 1);
        var boundary = new TestPalReference(20, 20, steps: 2);
        var overLimit = new TestPalReference(21, 20, steps: 3);

        CollectionAssert.AreEquivalent(new IPalReference[] { fast, boundary },
            SolverJobViewModel.SimplifyResults([fast, boundary, overLimit], CancellationToken.None));
    }

    [TestMethod]
    public void EmptyResultsRemainEmpty() =>
        Assert.IsEmpty(SolverJobViewModel.SimplifyResults([], CancellationToken.None));

    [TestMethod]
    public void InactiveProfilesRemainInTheZeroCakeGroup()
    {
        var owned = Owned(PalDB.LoadEmbedded().Pals.First(), "owned");
        Assert.AreEqual(0, PalResultProperty.SpecialCakes(owned));
        CollectionAssert.AreEqual(new IPalReference[] { owned },
            SolverJobViewModel.SimplifyResults([owned], CancellationToken.None));
    }

    // Keep effort and egg counts independent so the existing time cutoff can't mask cake-limit failures.
    private sealed class TestPalReference(int minutes, int cakes, int steps = 1) : IPalReference
    {
        public Pal Pal { get; } = PalDB.LoadEmbedded().Pals.First();
        public PalGender Gender => PalGender.WILDCARD;
        public List<PassiveSkill> EffectivePassives { get; init; } = [];
        public int EffectivePassivesHash => EffectivePassives.SetHash(passive => passive.InternalName);
        public List<PassiveSkill> ActualPassives => EffectivePassives;
        public IV_Set IVs => new();
        public AttackProfile AttackProfile { get; init; } = AttackProfile.Inactive;
        public float TimeFactor => 1;
        public IPalRefLocation Location { get; init; } = BredRefLocation.Instance;
        public TimeSpan BreedingEffort => TimeSpan.FromMinutes(minutes);
        public TimeSpan SelfBreedingEffort => BreedingEffort;
        public int TotalCost => 0;
        public int NumTotalBreedingSteps => steps;
        public int NumTotalEggs => cakes;
        public int NumTotalWildPals => 0;
        public bool IsOutdated { get; set; }
        public IPalReference WithGuaranteedGender(PalDB db, PalGender gender, bool useReverser) =>
            throw new NotSupportedException();
    }

    private static OwnedPalReference Owned(Pal pal, string id) => new(
        new PalInstance
        {
            InstanceId = id, Pal = pal, Gender = PalGender.MALE, PassiveSkills = [],
            Location = new PalLocation { Type = LocationType.Palbox },
        }, [], new IV_Set(), AttackProfile.Inactive);
}
