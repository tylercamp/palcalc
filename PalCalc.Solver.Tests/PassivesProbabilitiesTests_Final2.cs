﻿using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class PassivesProbabilitiesTests_Final2 : PassivesProbabilitiesTestBase
    {
        [TestMethod]
        public void Parents_None__Desired_None()
        {
            Assert.AreEqual(
                // nothing to inherit, just the chance of inheriting 2 random
                expected: GameConstants.PassiveRandomAddedProbability[2],
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [],
                    desiredParentPassives: [],
                    numFinalPassives: 2
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
                    // random 2, inherit 0
                    (GameConstants.PassiveRandomAddedProbability[2] * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 0)) +

                    // random 1, inherit 1
                    (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 1))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_1__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 1
                    (
                        (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 1, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 1, numDesired: 1, numChosen: 1)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 2
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
                    // random 2, inherit 0
                    (GameConstants.PassiveRandomAddedProbability[2] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 0)) +

                    // random 1, inherit 1
                    (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 1)) +

                    // random 0, inherit 2
                    (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_2__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 1
                    (
                        (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 2, numDesired: 1, numChosen: 1)
                    ) +
                    // random 0, inherit 2
                    (
                        (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                        *
                        SubCombinationProbability(numAvail: 2, numDesired: 1, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_2__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 2, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 2
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
                    // random 2, inherit 0
                    (GameConstants.PassiveRandomAddedProbability[2] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 0)) +

                    // random 1, inherit 1
                    (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1)) +

                    // random 0, inherit 2
                    (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 1
                    (
                        (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 1, numChosen: 1)
                    ) +
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 1, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_3__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 3, numDesired: 2, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 2
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
                    // random 2, inherit 0
                    (GameConstants.PassiveRandomAddedProbability[2] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 0)) +

                    // random 1, inherit 1
                    (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1)) +

                    // random 0, inherit 2
                    (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 1
                    (
                        (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 1)
                    ) +
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 1, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_4__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 4, numDesired: 2, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 2
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
                    // random 2, inherit 0
                    (GameConstants.PassiveRandomAddedProbability[2] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 0)) +

                    // random 1, inherit 1
                    (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1)) +

                    // random 0, inherit 2
                    (GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2))
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Irrelevant, Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_1()
        {
            Assert.AreEqual(
                expected: (
                    // random 1, inherit 1
                    (
                        (GameConstants.PassiveRandomAddedProbability[1] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 1))
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 1)
                    ) +
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 1, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Irrelevant, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        [TestMethod]
        public void Parents_5__Desired_2()
        {
            Assert.AreEqual(
                expected: (
                    // random 0, inherit 2
                    (
                        GameConstants.PassiveRandomAddedProbability[0] * PassiveProbabilityDirectUpTo(numAvailable: 3, numRequired: 2)
                        *
                        SubCombinationProbability(numAvail: 5, numDesired: 2, numChosen: 2)
                    )
                ),
                actual: Probabilities.Passives.ProbabilityInheritedTargetPassives(
                    parentPassives: [Runner, Swift, Irrelevant, Irrelevant, Irrelevant],
                    desiredParentPassives: [Runner, Swift],
                    numFinalPassives: 2
                ),
                delta: 0.0001f
            );
        }

        #endregion
    }
}
