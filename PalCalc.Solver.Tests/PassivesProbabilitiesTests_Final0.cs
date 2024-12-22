using PalCalc.Model;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public sealed class PassivesProbabilitiesTests_Final0 : PassivesProbabilitiesTestBase
    {
        /* If the final number of child passives is 0, the probability is just the chance of 0 random AND 0 inherited */

        [TestMethod]
        public void Parents_None__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_1__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0] * GameConstants.PassiveProbabilityDirect[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_2__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0] * GameConstants.PassiveProbabilityDirect[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0] * GameConstants.PassiveProbabilityDirect[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0] * GameConstants.PassiveProbabilityDirect[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_None()
        {
            Assert.AreEqual(
                expected: GameConstants.PassiveRandomAddedProbability[0] * GameConstants.PassiveProbabilityDirect[0],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 0
                ),
                delta: 0.0001f
            );
        }
    }
}
