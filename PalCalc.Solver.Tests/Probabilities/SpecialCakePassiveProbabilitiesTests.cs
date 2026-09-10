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
    public void FixedFourInheritsAllAvailableBeforeRollingRandomPassives()
    {
        var parentPassives = Passives(3);
        var desired = parentPassives.Take(2).ToList();

        Assert.AreEqual(
            db.BreedingMechanics.PassiveRandomAddedProbability[0],
            Probability(parentPassives, desired, finalCount: 3)
        );
    }

    [TestMethod]
    public void FixedFourSelectsFourFromLargerParentPool()
    {
        var parentPassives = Passives(5);
        var desired = parentPassives.Take(2).ToList();

        Assert.AreEqual(0.6f, Probability(parentPassives, desired, finalCount: 4), 0.0001f);
    }

    [TestMethod]
    public void FixedFourCannotFillAllSlotsFromRandomPassivesAlone()
    {
        Assert.AreEqual(0, Probability([], [], finalCount: 4));
    }

    [TestMethod]
    public void FixedRollSmallerThanDesiredPassiveCountIsImpossible()
    {
        var desired = Passives(2);

        Assert.AreEqual(
            0,
            PassiveProbabilities.ProbabilityInheritedTargetPassivesForFixedInheritRoll(
                db.BreedingMechanics,
                desired,
                desired,
                numFinalPassives: 2,
                inheritedCountRoll: 1
            )
        );
    }

    private float Probability(
        List<PassiveSkill> parentPassives,
        List<PassiveSkill> desired,
        int finalCount
    ) => PassiveProbabilities.ProbabilityInheritedTargetPassivesForFixedInheritRoll(
        db.BreedingMechanics,
        parentPassives,
        desired,
        finalCount,
        inheritedCountRoll: GameConstants.MaxTotalPassives
    );

    private List<PassiveSkill> Passives(int count) =>
        db.PassiveSkills.Take(count).ToList();
}
