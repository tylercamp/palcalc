using PalCalc.Model;
using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FPassiveSetTests : FAttrIdTests
    {
        [TestMethod]
        public void FPassiveSet_CtorIdentity()
        {
            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>(),
                FPassiveSet.FromModel(db, []).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>(),
                // 'null' is equivalent to an empty slot
                FPassiveSet.FromModel(db, [null]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner },
                FPassiveSet.FromModel(db, [runner]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift },
                FPassiveSet.FromModel(db, [runner, swift]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble },
                FPassiveSet.FromModel(db, [runner, swift, nimble]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble, lucky },
                FPassiveSet.FromModel(db, [runner, swift, nimble, lucky]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble, lucky, legend },
                FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]).ModelObjects.ToList()
            );

            Assert.AreEqual(
                FPassiveSet.FromModel(db, [runner, swift, nimble, lucky]),
                FPassiveSet.FromModel(db, [swift, runner, lucky, nimble])
            );

            Assert.AreEqual(
                FPassiveSet.FromModel(db, [runner, swift, nimble, lucky]),
                FPassiveSet.FromModel(db, [lucky, nimble, swift, runner])
            );

            Assert.AreEqual(
                FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]),
                FPassiveSet.FromModel(db, [lucky, legend, nimble, swift, runner])
            );
        }

        [TestMethod]
        public void FPassiveSet_Contains()
        {
            Assert.IsFalse(
                FPassiveSet.FromModel(db, []).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble, swift]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble, swift, legend]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble, swift, legend, lucky]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble, swift, legend, random1]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [nimble, swift, legend, random2]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [random1]).Contains(new FPassive(db, runner))
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [random1]).Contains(FPassive.Empty)
            );

            Assert.IsFalse(
                FPassiveSet.FromModel(db, [random1]).Contains(new FPassive(db, random1))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble]).Contains(new FPassive(db, nimble))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new FPassive(db, nimble))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new FPassive(db, runner))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new FPassive(db, swift))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new FPassive(db, lucky))
            );

            Assert.IsTrue(
                FPassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new FPassive(db, legend))
            );
        }

        [TestMethod]
        public void FPassiveSet_Count()
        {
            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, []).Count
            );

            Assert.AreEqual(
                expected: 1,
                FPassiveSet.FromModel(db, [runner]).Count
            );

            Assert.AreEqual(
                expected: 2,
                FPassiveSet.FromModel(db, [runner, swift]).Count
            );

            Assert.AreEqual(
                expected: 3,
                FPassiveSet.FromModel(db, [runner, swift, nimble]).Count
            );

            Assert.AreEqual(
                expected: 4,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Count
            );

            Assert.AreEqual(
                expected: 5,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Count
            );

            Assert.AreEqual(
                expected: 6,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Count
            );

            Assert.AreEqual(
                expected: 7,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1]).Count
            );

            Assert.AreEqual(
                expected: 8,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1, random2]).Count
            );
        }

        [TestMethod]
        public void FPassiveSet_CountRandom()
        {
            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, []).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner, swift]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner, swift, nimble]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).CountRandom
            );

            Assert.AreEqual(
                expected: 0,
                FPassiveSet.FromModel(db, []).CountRandom
            );

            Assert.AreEqual(
                expected: 1,
                FPassiveSet.FromModel(db, [random1]).CountRandom
            );

            Assert.AreEqual(
                expected: 2,
                FPassiveSet.FromModel(db, [random1, random2]).CountRandom
            );

            Assert.AreEqual(
                expected: 3,
                FPassiveSet.FromModel(db, [random1, random2, random3]).CountRandom
            );

            Assert.AreEqual(
                expected: 3,
                FPassiveSet.FromModel(db, [random1, random2, random3, legend]).CountRandom
            );

            Assert.AreEqual(
                expected: 4,
                FPassiveSet.FromModel(db, [random1, random2, random3, random4]).CountRandom
            );

            Assert.AreEqual(
                expected: 5,
                FPassiveSet.FromModel(db, [random1, random2, random3, random4, random5, lucky, workaholic]).CountRandom
            );
        }

        [TestMethod]
        public void FPassiveSet_Indexer()
        {
            var orderedPassives = new List<PassiveSkill>()
            {
                runner, swift, nimble
            }.OrderByDescending(p => new FPassive(db, p).Store).ToList();

            Assert.AreEqual(
                expected: orderedPassives[0],
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[0].ModelObject
            );

            Assert.AreEqual(
                expected: orderedPassives[1],
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[1].ModelObject
            );

            Assert.AreEqual(
                expected: orderedPassives[2],
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[2].ModelObject
            );

            Assert.AreEqual(expected: FPassive.Empty, actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[3]);
            Assert.AreEqual(expected: FPassive.Empty, actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[4]);
            Assert.AreEqual(expected: FPassive.Empty, actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[5]);
            Assert.AreEqual(expected: FPassive.Empty, actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[6]);
            Assert.AreEqual(expected: FPassive.Empty, actual: FPassiveSet.FromModel(db, [runner, swift, nimble])[7]);
        }

        [TestMethod]
        public void FPassiveSet_OfZero_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassive.Random)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassive.Empty)
            );
        }

        [TestMethod]
        public void FPassiveSet_OfOne_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(new FPassive(db, swift))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(new FPassive(db, swift))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ExceptSingle_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` FPassive should always noop

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(FPassive.Random)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(FPassive.Empty)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassive.Random)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassive.Empty)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassive.Random)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassive.Empty)
            );
        }

        [TestMethod]
        public void FPassiveSet_OfTwo_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(new FPassive(db, swift))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(new FPassive(db, nimble))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1]),
                actual: FPassiveSet.FromModel(db, [runner, random1]).Except(new FPassive(db, nimble))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfThree_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(new FPassive(db, swift))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(new FPassive(db, nimble))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(new FPassive(db, legend))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, random1]),
                actual: FPassiveSet.FromModel(db, [runner, swift, random1]).Except(new FPassive(db, nimble))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfFive_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new FPassive(db, swift))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new FPassive(db, nimble))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, swift, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new FPassive(db, legend))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, legend, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new FPassive(db, lucky))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1]).Except(new FPassive(db, legend))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfSix_ExceptSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, workaholic))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, lucky))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, swift, workaholic, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, legend))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, workaholic, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, swift))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, workaholic, swift, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, nimble))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [workaholic, nimble, swift, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, random1]),
                actual: FPassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, random1]).Except(new FPassive(db, workaholic))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ExceptSet_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` FPassive should always noop


            // (filter by empty set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [random1, random2]))
            );


            // (filter by random+real set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [random1, random2, swift]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAnyRandom_ExceptSet_OfEmptyOrRandom()
        {
            // (filter by empty set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1]),
                actual: FPassiveSet.FromModel(db, [runner, random1]).Except(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, random1, random2]).Except(FPassiveSet.FromModel(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [random1, random2]).Except(FPassiveSet.FromModel(db, [random2]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, random1, random2]).Except(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, random1, random2]).Except(FPassiveSet.FromModel(db, [random1, random2]))
            );

            // (filter by random+real set)
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [random1]).Except(FPassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [random1, random2]).Except(FPassiveSet.FromModel(db, [random2, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, random1, random2]).Except(FPassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, swift, random1, random2]).Except(FPassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, swift, random1, random2]).Except(FPassiveSet.FromModel(db, [random1, random2, swift, runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, swift, legend, random1, random2]).Except(FPassiveSet.FromModel(db, [random1, random2, swift, runner, legend]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfZero_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfOne_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [swift]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [nimble]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Except(FPassiveSet.FromModel(db, [swift, nimble]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfTwo_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Except(FPassiveSet.FromModel(db, [runner, lucky, nimble, legend, random1]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfThree_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [legend, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [runner, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [lucky, legend, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [lucky, legend, runner, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Except(FPassiveSet.FromModel(db, [lucky, legend, runner, swift, random1, random2]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfFour_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner, swift, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner, swift, legend, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner, swift, legend, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(FPassiveSet.FromModel(db, [runner, swift, legend, nimble, lucky, random1, random2]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfSix_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, runner, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, runner, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, lucky, runner, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift, nimble, lucky, legend, runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Except(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random2, workaholic]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_Intersect_EmptyOrRandom()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Intersect(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner]).Intersect(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(FPassiveSet.FromModel(db, [random1, random2]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfZero_Intersect_Any()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Intersect(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Intersect(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Intersect(FPassiveSet.FromModel(db, [random1, random2, swift]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfTwo_Intersect_Any()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Intersect(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Intersect(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Intersect(FPassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Intersect(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, random1]).Intersect(FPassiveSet.FromModel(db, [runner, swift, random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic, random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [random1, swift]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic, random1]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfSix_Intersect_Any()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner, swift, random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ConcatSingle_Empty()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassive.Empty)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble]).Concat(FPassive.Empty)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassive.Empty)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Concat(FPassive.Empty)
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ConcatSingle_Random()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, []).Concat(new FPassive(db, random1))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [random1]).Concat(new FPassive(db, random1))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2, random3, random4, random5]),
                actual: FPassiveSet.FromModel(db, [random1, random2, random3, random4]).Concat(new FPassive(db, random1))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ConcatSet_OfEmpty()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, []),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, [runner]).Concat(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Concat(FPassiveSet.FromModel(db, []))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfAny_ConcatSet_OfRandom()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1]),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSet.FromModel(db, [random1]).Concat(FPassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2, random3, random4, random5, random6]),
                actual: FPassiveSet.FromModel(db, [random1, random2, random3]).Concat(FPassiveSet.FromModel(db, [random1, random2, random3]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfZero_ConcatSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, [runner]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfZero_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner]),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, []).Concat(FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfTwo_ConcatSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(new FPassive(db, nimble))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(new FPassive(db, swift))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfTwo_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [runner, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [nimble, legend, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift]).Concat(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfFour_ConcatSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new FPassive(db, lucky))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new FPassive(db, runner))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new FPassive(db, swift))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfFour_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [runner, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [swift, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [nimble, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [legend, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(FPassiveSet.FromModel(db, [lucky, workaholic, random1, random2]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1, random2]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, random1]).Concat(FPassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1]))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfSix_ConcatSingle()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Concat(new FPassive(db, workaholic))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Concat(new FPassive(db, workaholic))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Concat(new FPassive(db, runner))
            );
        }

        [TestMethod]
        public void FPassiveSet_OfSix_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend, workaholic]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(FPassiveSet.FromModel(db, [legend, workaholic]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(FPassiveSet.FromModel(db, [legend, runner, swift, nimble, lucky]))
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend, random3]),
                actual: FPassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(FPassiveSet.FromModel(db, [legend, runner, swift, nimble, random1]))
            );
        }

        private IEnumerable<FPassiveSet> FSetCombinations(FPassiveSet set, int comboSize)
        {
            var iter = set.GetCombinationIterator(comboSize);
            while (iter.MoveNext())
                yield return iter.Current;
        }

        private FPassiveSet MkSet(params PassiveSkill[] passives) =>
            FPassiveSet.FromModel(db, passives.ToList());

        [TestMethod]
        public void FPassiveSet_OfTwo_Combinations()
        {
            var set = MkSet(runner, swift);

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner),
                    MkSet(swift)
                },
                actual: FSetCombinations(set, 1).ToList()
            );

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner, swift)
                },
                actual: FSetCombinations(set, 2).ToList()
            );
        }

        [TestMethod]
        public void FPassiveSet_OfFour_Combinations()
        {
            var set = MkSet(runner, swift, nimble, legend);

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner),
                    MkSet(swift),
                    MkSet(nimble),
                    MkSet(legend)
                },
                actual: FSetCombinations(set, 1).ToList()
            );

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner, swift),
                    MkSet(runner, nimble),
                    MkSet(runner, legend),
                    MkSet(swift, nimble),
                    MkSet(swift, legend),
                    MkSet(nimble, legend)
                },
                actual: FSetCombinations(set, 2).ToList()
            );

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner, swift, nimble),
                    MkSet(runner, swift, legend),
                    MkSet(swift, nimble, legend),
                    MkSet(runner, nimble, legend)
                },
                actual: FSetCombinations(set, 3).ToList()
            );

            CollectionAssert.AreEquivalent(
                expected: new List<FPassiveSet>()
                {
                    MkSet(runner, swift, nimble, legend)
                },
                actual: FSetCombinations(set, 4).ToList()
            );
        }

        [TestMethod]
        public void FPassiveSet_IndexOf_Consistency()
        {
            var set = FPassiveSet.FromModel(db, [runner, swift, nimble]);
            var runnerPassive = new FPassive(db, runner);
            var swiftPassive = new FPassive(db, swift);
            var nimblePassive = new FPassive(db, nimble);

            Assert.AreEqual(runnerPassive, set[set.IndexOf(runnerPassive)]);
            Assert.AreEqual(swiftPassive, set[set.IndexOf(swiftPassive)]);
            Assert.AreEqual(nimblePassive, set[set.IndexOf(nimblePassive)]);

            for (int i = 0; i < set.Count; i++)
            {
                var passiveAtIndex = set[i];
                Assert.AreEqual(i, set.IndexOf(passiveAtIndex));
            }
        }

        [TestMethod]
        public void FPassiveSet_IndexOf_NotFound()
        {
            var set = FPassiveSet.FromModel(db, [runner, swift, nimble]);
            var luckyPassive = new FPassive(db, lucky);
            var legendPassive = new FPassive(db, legend);

            Assert.AreEqual(-1, set.IndexOf(luckyPassive));
            Assert.AreEqual(-1, set.IndexOf(legendPassive));
        }

        [TestMethod]
        public void FPassiveSet_IndexOf_EmptySet()
        {
            var emptySet = FPassiveSet.FromModel(db, []);
            var runnerPassive = new FPassive(db, runner);

            Assert.AreEqual(-1, emptySet.IndexOf(runnerPassive));
        }
    }
}
