using PalCalc.Model;
using PassiveProbabilities = PalCalc.Solver.Probabilities.Passives;

namespace PalCalc.Solver.Tests.Probabilities;

[TestClass]
public class SpecialCakePassiveProbabilitiesTests
{
    private readonly PalDB db = SolverTestScenario.DB;

    [TestMethod]
    public void FixedFourCannotProduceFewerThanFourPassivesFromFourParentsPassives()
    {
        var parentPassives = Passives(4);
        var desired = parentPassives.Take(2).ToList();

        Assert.AreEqual(0, Probability(parentPassives, desired, finalCount: 2));
        Assert.AreEqual(0, Probability(parentPassives, desired, finalCount: 3));
        Assert.AreEqual(1, Probability(parentPassives, desired, finalCount: 4));
    }

    [TestMethod]
    public void OverrideFourFillsTheShortfallWithoutAnOrdinaryRandomCountRoll()
    {
        var parentPassives = Passives(3);
        var desired = parentPassives.Take(2).ToList();

        Assert.AreEqual(
            0,
            Probability(parentPassives, desired, finalCount: 3)
        );
        Assert.AreEqual(1, Probability(parentPassives, desired, finalCount: 4));
    }

    [TestMethod]
    public void FixedFourSelectsFourFromLargerParentPool()
    {
        var parentPassives = Passives(5);
        var desired = parentPassives.Take(2).ToList();

        Assert.AreEqual(0.6f, Probability(parentPassives, desired, finalCount: 4), 0.0001f);
    }

    [TestMethod]
    public void OverrideFourCanFillAllSlotsFromRandomPassivesAlone()
    {
        Assert.AreEqual(1, Probability([], [], finalCount: 4));
    }

    [TestMethod]
    public void FixedRollSmallerThanDesiredPassiveCountIsImpossible()
    {
        var desired = Passives(2);

        Assert.AreEqual(
            0,
            PassiveProbabilities.ProbabilityInheritedTargetPassivesForOverride(
                db.BreedingMechanics,
                desired,
                desired,
                numFinalPassives: 2,
                overrideCount: 1
            )
        );
    }

    private float Probability(
        List<PassiveSkill> parentPassives,
        List<PassiveSkill> desired,
        int finalCount
    ) => PassiveProbabilities.ProbabilityInheritedTargetPassivesForOverride(
        db.BreedingMechanics,
        parentPassives,
        desired,
        finalCount,
        overrideCount: GameConstants.MaxTotalPassives
    );

    private List<PassiveSkill> Passives(int count) =>
        db.PassiveSkills.Take(count).ToList();

    [DataTestMethod]
    [DataRow(1, 1, 1f / 5)]
    [DataRow(2, 2, 2f / 5)]
    [DataRow(4, 4, 4f / 5)]
    public void OverrideIgnoresRandomContributions(int count, int finalCount, float expected)
    {
        var parents = Passives(5);
        Assert.AreEqual(
            expected: expected,
            actual: PassiveProbabilities.ProbabilityInheritedTargetPassivesForOverride(
                db.BreedingMechanics, parents, parents.Take(1).ToList(), finalCount, count
            ),
            delta: 0.000001f
        );
    }

    [TestMethod]
    public void OverrideIgnoresOrdinaryWeightTablesAndRejectsNonpositiveOverrides()
    {
        var mechanics = new BreedingMechanics([1, 0, 0], new() { [1] = 1 }, new() { [0] = 1 });
        Assert.AreEqual(1f, PassiveProbabilities.ProbabilityInheritedTargetPassivesForOverride(mechanics, [], [], 4, 4));
        foreach (var count in new[] { 0, -1 })
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                PassiveProbabilities.ProbabilityInheritedTargetPassivesForOverride(mechanics, [], [], 4, count));
    }
}
