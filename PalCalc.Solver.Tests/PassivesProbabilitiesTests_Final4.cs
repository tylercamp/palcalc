using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class PassivesProbabilitiesTests_Final4 : PassivesProbabilitiesTestBase
    {
        private float RandomProbabilityAddedAtLeast(int numRequired)
        {
            // we're always going to total 4 passives (the limit)
            //
            // if we've inherited 2 directly, then we can inherit 2, 3, or 4 random passives
            // and still hit the target of 4

            float res = 0.0f;
            for (int i = numRequired; i <= GameConstants.MaxTotalPassives; i++)
            {
                res += GameConstants.PassiveRandomAddedProbability[i];
            }

            return res;
        }

        [TestMethod]
        public void Parents_None__Desired_None()
        {
            Assert.AreEqual(
                // nothing to inherit, just the chance of inheriting 4 random
                expected: RandomProbabilityAddedAtLeast(4),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #region 1 Parent Passive

        [TestMethod]
        public void Parents_1__Desired_None()
        {
            Assert.AreEqual(
                expected: (
                    // random 4, inherit 0
                    (RandomProbabilityAddedAtLeast(4) * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 0)) +

                    // random 3, inherit 1
                    (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 1))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_1__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 3, inherit 1
                    (
                        (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 1, numDesired: 1, numChosen: 1)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #endregion

        #region 2 Parent Passives

        [TestMethod]
        public void Parents_2__Desired_None()
        {
            Assert.AreEqual(
                expected: (
                    // random 4, inherit 0
                    (RandomProbabilityAddedAtLeast(4) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 0)) +

                    // random 3, inherit 1
                    (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 1)) +

                    // random 2, inherit 2
                    (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_2__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 3, inherit 1
                    (
                        (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 2, numDesired: 1, numChosen: 1)
                    ) +
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 2, numDesired: 1, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_2__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 2, numDesired: 2, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #endregion

        #region 3 Parent Passives

        [TestMethod]
        public void Parents_3__Desired_None()
        {
            Assert.AreEqual(
                expected: (
                    // random 4, inherit 0
                    (RandomProbabilityAddedAtLeast(4) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 0)) +

                    // random 3, inherit 1
                    (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1)) +

                    // random 2, inherit 2
                    (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)) +

                    // random 1, inherit 3
                    (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 3))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 3, inherit 1
                    (
                        (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 1, numChosen: 1)
                    ) +
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 1, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 1, numChosen: 3)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 2, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 2, numChosen: 3)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_3()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 3, numChosen: 3)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Nimble],
                    desiredParentPassives: [Runner, Swift, Nimble],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #endregion

        #region 4 Parent Passives

        [TestMethod]
        public void Parents_4__Desired_None()
        {
            Assert.AreEqual(
                expected: (
                    // random 4, inherit 0
                    (RandomProbabilityAddedAtLeast(4) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 0)) +

                    // random 3, inherit 1
                    (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 1)) +

                    // random 2, inherit 2
                    (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 2)) +

                    // random 1, inherit 3
                    (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 3)) +

                    // random 0, inherit 4
                    (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 4))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 3, inherit 1
                    (
                        (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 1)
                    ) +
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 2, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 2, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 2, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_3()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 3, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 3, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Nimble, Irrelevant],
                    desiredParentPassives: [Runner, Swift, Nimble],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_4()
        {
            Assert.AreEqual(
                expected: (
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 4, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 4, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Nimble, Lucky],
                    desiredParentPassives: [Runner, Swift, Nimble, Lucky],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #endregion

        #region 5 Parent Passives

        [TestMethod]
        public void Parents_5__Desired_None()
        {
            Assert.AreEqual(
                expected: (
                    // random 4, inherit 0
                    (RandomProbabilityAddedAtLeast(4) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 0)) +

                    // random 3, inherit 1
                    (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 1)) +

                    // random 2, inherit 2
                    (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 2)) +

                    // random 1, inherit 3
                    (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 3)) +

                    // random 0, inherit 4
                    (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 4))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 3, inherit 1
                    (
                        (RandomProbabilityAddedAtLeast(3) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 1)
                    ) +
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 2, inherit 2
                    (
                        (RandomProbabilityAddedAtLeast(2) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 2, numChosen: 2)
                    ) +
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 2, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 2, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_3()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 3
                    (
                        (RandomProbabilityAddedAtLeast(1) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 3))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 3, numChosen: 3)
                    ) +
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 3, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Nimble, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner, Swift, Nimble],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_4()
        {
            Assert.AreEqual(
                expected: (
                    // random 0, inherit 4
                    (
                        (RandomProbabilityAddedAtLeast(0) * PassiveProbabilityDirectUpTo(numAvailable: 5, numRequired: 4))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 4, numChosen: 4)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Nimble, Lucky, Irrelevant],
                    desiredParentPassives: [Runner, Swift, Nimble, Lucky],
                    numFinalPassives: 4
                ),
                delta: 0.0001f
            );
        }

        #endregion
    }
}
