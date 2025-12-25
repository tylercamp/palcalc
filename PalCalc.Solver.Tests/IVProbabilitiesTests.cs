using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class IVProbabilitiesTests
    {
        private static IV_Value RealValue_Relevant = new IV_Value(true, 50, 50);
        private static IV_Value RealValue_Irrelevant = new IV_Value(false, 50, 50);
        private static IV_Value RandomValue = IV_Value.Random;

        private static IV_Value _ANY_ = RandomValue;

        private static IV_Set MkIVs(IV_Value? hp = null, IV_Value? attack = null, IV_Value? defense = null) =>
            new IV_Set { HP = hp ?? _ANY_, Attack = attack ?? _ANY_, Defense = defense ?? _ANY_ };

        [TestMethod]
        public void IVs_HP_0_Atk_0_Def_0()
        {
            Assert.AreEqual(
                1,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    new IV_Set { HP = RealValue_Irrelevant, Attack = RealValue_Irrelevant, Defense = RealValue_Irrelevant },
                    new IV_Set { HP = RealValue_Irrelevant, Attack = RealValue_Irrelevant, Defense = RealValue_Irrelevant }
                )
            );

            Assert.AreEqual(
                1,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    new IV_Set { HP = RandomValue, Attack = RandomValue, Defense = RandomValue },
                    new IV_Set { HP = RandomValue, Attack = RandomValue, Defense = RandomValue }
                )
            );
        }

        #region Single IV Tests

        [TestMethod]
        public void IVs_HP_1_Atk_0_Def_0()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the HP IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the HP IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the HP IV
                    *
                    (1 / 2.0) // got from the right parent
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_0_Def_0()
        {
            Assert.AreEqual(
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the HP IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the HP IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the HP IV
                    *
                    (2 / 2.0) // either parent is ok
                ),
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_1_Def_0()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the Attack IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the Attack IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the Attack IV
                    *
                    (1 / 2.0) // got from the right parent
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_2_Def_0()
        {
            Assert.AreEqual(
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the Attack IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the Attack IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the Attack IV
                    *
                    (2 / 2.0) // either parent is ok
                ),
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_0_Def_1()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the Defense IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the Defense IV
                    *
                    (1 / 2.0) // got from the right parent
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the Defense IV
                    *
                    (1 / 2.0) // got from the right parent
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_0_Def_2()
        {
            Assert.AreEqual(
                (
                    GameConstants.IVProbabilityDirect[1] // inherited 1 IV
                    *
                    (1 / 3.0) // it was the Defense IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (2 / 3.0) // one of them was the Defense IV
                    *
                    (2 / 2.0) // either parent is ok
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get the Defense IV
                    *
                    (2 / 2.0) // either parent is ok
                ),
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        #endregion

        #region Two IV Tests (HP + Attack)

        [TestMethod]
        public void IVs_HP_1_Atk_1_Def_0()
        {
            var probability = (
                    (
                        GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                        *
                        (1 / 3.0) // they're the desired IVs
                        *
                        (
                            (1 / 2.0) // first is from the right parent
                            *
                            (1 / 2.0) // next is from the right parent
                        )
                    )
                    + // OR
                    (
                        GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                        *
                        (3 / 3.0) // will always get both IVs
                        *
                        (
                            (1 / 2.0) // first is from the desired parent
                            *
                            (1 / 2.0) // second is from the desired parent
                        )
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_1_Def_0()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_1_Atk_2_Def_0()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_2_Def_0()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (2 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        #endregion

        #region Two IV Tests (HP + Defense)

        [TestMethod]
        public void IVs_HP_1_Atk_0_Def_1()
        {
            var probability = (
                    (
                        GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                        *
                        (1 / 3.0) // they're the desired IVs
                        *
                        (
                            (1 / 2.0) // first is from the right parent
                            *
                            (1 / 2.0) // next is from the right parent
                        )
                    )
                    + // OR
                    (
                        GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                        *
                        (3 / 3.0) // will always get both IVs
                        *
                        (
                            (1 / 2.0) // first is from the desired parent
                            *
                            (1 / 2.0) // second is from the desired parent
                        )
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_0_Def_1()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_1_Atk_0_Def_2()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_0_Def_2()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (2 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        #endregion

        #region Two IV Tests (Attack + Defense)

        [TestMethod]
        public void IVs_HP_0_Atk_1_Def_1()
        {
            var probability = (
                    (
                        GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                        *
                        (1 / 3.0) // they're the desired IVs
                        *
                        (
                            (1 / 2.0) // first is from the right parent
                            *
                            (1 / 2.0) // next is from the right parent
                        )
                    )
                    + // OR
                    (
                        GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                        *
                        (3 / 3.0) // will always get both IVs
                        *
                        (
                            (1 / 2.0) // first is from the desired parent
                            *
                            (1 / 2.0) // second is from the desired parent
                        )
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_2_Def_1()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_1_Def_2()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (1 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_0_Atk_2_Def_2()
        {
            var probability = (
                (
                    GameConstants.IVProbabilityDirect[2] // inherited 2 IVs
                    *
                    (1 / 3.0) // they're the desired IVs
                    *
                    (
                        (2 / 2.0) // first is from the right parent
                        *
                        (2 / 2.0) // next is from the right parent
                    )
                )
                + // OR
                (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (3 / 3.0) // will always get both IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                    )
                )
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        #endregion

        #region Three IV Tests

        [TestMethod]
        public void IVs_HP_1_Atk_1_Def_1()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (1 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                        *
                        (1 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs()
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_1_Def_1()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                        *
                        (1 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_1_Atk_2_Def_1()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (1 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                        *
                        (1 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, hp: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_1_Atk_1_Def_2()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (1 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                        *
                        (2 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant, hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant, hp: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant, hp: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(defense: RealValue_Relevant),
                    MkIVs(defense: RealValue_Relevant, hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_2_Def_1()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                        *
                        (1 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_1_Def_2()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (1 / 2.0) // second is from the desired parent
                        *
                        (2 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant, attack: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, defense: RealValue_Relevant, attack: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_1_Atk_2_Def_2()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (1 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                        *
                        (2 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant, hp: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(attack: RealValue_Relevant, defense: RealValue_Relevant, hp: RealValue_Relevant)
                ),
                0.001
            );
        }

        [TestMethod]
        public void IVs_HP_2_Atk_2_Def_2()
        {
            var probability = (
                    GameConstants.IVProbabilityDirect[3] // inherited 3 IVs
                    *
                    (
                        (2 / 2.0) // first is from the desired parent
                        *
                        (2 / 2.0) // second is from the desired parent
                        *
                        (2 / 2.0) // third is from the desired parent
                    )
                );

            Assert.AreEqual(
                probability,
                Probabilities.IVs.ProbabilityInheritedTargetIVs(
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant),
                    MkIVs(hp: RealValue_Relevant, attack: RealValue_Relevant, defense: RealValue_Relevant)
                ),
                0.001
            );
        }

        #endregion
    }
}
