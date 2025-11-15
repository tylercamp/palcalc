using PalCalc.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class AttrIdTests
    {
        private PalDB db;

        private PassiveSkill runner;
        private PassiveSkill swift;
        private PassiveSkill nimble;
        private PassiveSkill lucky;
        private PassiveSkill legend;

        private PassiveSkill random1 = new RandomPassiveSkill();
        private PassiveSkill random2 = new RandomPassiveSkill();

        [TestInitialize]
        public void InitDB()
        {
            db = PalDB.LoadEmbedded();

            runner = "Runner".ToStandardPassive(db);
            swift = "Swift".ToStandardPassive(db);
            nimble = "Nimble".ToStandardPassive(db);
            lucky = "Lucky".ToStandardPassive(db);
            legend = "Legend".ToStandardPassive(db);
        }

        [TestMethod]
        public void Gender_CtorIdentity()
        {
            Assert.AreEqual(Model.PalGender.MALE, new Gender(Model.PalGender.MALE).Value);
            Assert.AreEqual(Model.PalGender.FEMALE, new Gender(Model.PalGender.FEMALE).Value);
            Assert.AreEqual(Model.PalGender.WILDCARD, new Gender(Model.PalGender.WILDCARD).Value);
            Assert.AreEqual(Model.PalGender.OPPOSITE_WILDCARD, new Gender(Model.PalGender.OPPOSITE_WILDCARD).Value);
            Assert.AreEqual(Model.PalGender.NONE, new Gender(Model.PalGender.NONE).Value);
        }

        [TestMethod]
        public void Time_CtorIdentity()
        {
            Assert.AreEqual(TimeSpan.Zero, new Time(TimeSpan.Zero).Value);
            Assert.AreEqual(TimeSpan.FromMinutes(100), new Time(TimeSpan.FromMinutes(100)).Value);
        }

        [TestMethod]
        public void IV_CtorIdentity()
        {
            Assert.AreEqual(
                new IV_Range(isRelevant: true, value: 50),
                new IV(isRelevant: true, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(isRelevant: false, value: 50),
                new IV(isRelevant: false, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(IsRelevant: true, Min: 25, Max: 100),
                new IV(isRelevant: true, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(IsRelevant: false, Min: 25, Max: 100),
                new IV(isRelevant: false, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                IV_Random.Instance,
                IV.Random.ModelObject
            );
        }

        [TestMethod]
        public void IVSet_CtorIdentity()
        {
            Assert.AreEqual(
                new IV_Set() { Attack = IV_Random.Instance, Defense = IV_Random.Instance, HP = IV_Random.Instance },
                new IVSet(IV.Random, IV.Random, IV.Random).ModelObject
            );

            Assert.AreEqual(
                new IV_Set()
                {
                    Attack = new IV_Range(isRelevant: true, 10),
                    Defense = new IV_Range(isRelevant: false, 50),
                    HP = new IV_Range(isRelevant: true, 100)
                },
                new IVSet(
                    Attack: new IV(isRelevant: true, value: 10),
                    Defense: new IV(isRelevant: false, value: 50),
                    HP: new IV(isRelevant: true, value: 100)
                ).ModelObject
            );
        }

        [TestMethod]
        public void Passive_CtorIdentity()
        {
            Assert.IsInstanceOfType<RandomPassiveSkill>(Passive.Random.ModelObject);
            Assert.AreEqual(null, new Passive(db, null).ModelObject);

            var runner = "Runner".ToStandardPassive(db);
            Assert.AreEqual(runner, new Passive(db, runner).ModelObject);
        }

        [TestMethod]
        public void Passive_Valid_Properties()
        {
            Assert.IsFalse(new Passive(db, runner).IsEmpty);
            Assert.IsFalse(new Passive(db, runner).IsRandom);
        }

        [TestMethod]
        public void Passive_Random_Properties()
        {
            Assert.IsFalse(Passive.Random.IsEmpty);
            Assert.IsTrue(Passive.Random.IsRandom);
        }

        [TestMethod]
        public void Passive_Empty_Properties()
        {
            Assert.IsTrue(new Passive(db, null).IsEmpty);
            Assert.IsFalse(new Passive(db, null).IsRandom);
        }

        [TestMethod]
        public void PassiveSet_CtorIdentity()
        {
            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>(),
                new PassiveSet(db, []).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>(),
                // 'null' is equivalent to an empty slot
                new PassiveSet(db, [null]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner },
                new PassiveSet(db, [runner]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift },
                new PassiveSet(db, [runner, swift]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble },
                new PassiveSet(db, [runner, swift, nimble]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble, lucky },
                new PassiveSet(db, [runner, swift, nimble, lucky]).ModelObjects.ToList()
            );

            Assert.AreEqual(
                new PassiveSet(db, [runner, swift, nimble, lucky]),
                new PassiveSet(db, [swift, runner, lucky, nimble])
            );

            Assert.AreEqual(
                new PassiveSet(db, [runner, swift, nimble, lucky]),
                new PassiveSet(db, [lucky, nimble, swift, runner])
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_ExceptSingle()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(Passive.Empty)
            );
        }

        [TestMethod]
        public void PassiveSet_OfOne_ExceptSingle()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new Passive(db, swift))
            );
                              
            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(new Passive(db, swift))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ExceptSingle_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` passive should always noop

            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(Passive.Empty)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(Passive.Empty)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(Passive.Empty)
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ExceptSingle()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1]),
                actual: new PassiveSet(db, [runner, random1]).Except(new Passive(db, nimble))
            );
        }

        [TestMethod]
        public void PassiveSet_OfThree_ExceptSingle()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new Passive(db, legend))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, random1]),
                actual: new PassiveSet(db, [runner, swift, random1]).Except(new Passive(db, nimble))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ExceptSet_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` passive should always noop


            // (filter by empty set)
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, []))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, []))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [random1]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [random1]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [random1]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [random1, random2]))
            );


            // (filter by random+real set)
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [random1, random2, swift]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAnyRandom_ExceptSet_OfEmptyOrRandom()
        {
            // (filter by empty set)
            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(new PassiveSet(db, []))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1]),
                actual: new PassiveSet(db, [runner, random1]).Except(new PassiveSet(db, []))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1, random2]),
                actual: new PassiveSet(db, [runner, random1, random2]).Except(new PassiveSet(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(new PassiveSet(db, [random1]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [random1, random2]),
                actual: new PassiveSet(db, [random1, random2]).Except(new PassiveSet(db, [random2]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1, random2]),
                actual: new PassiveSet(db, [runner, random1, random2]).Except(new PassiveSet(db, [random1]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1, random2]),
                actual: new PassiveSet(db, [runner, random1, random2]).Except(new PassiveSet(db, [random1, random2]))
            );

            // (filter by random+real set)
            Assert.AreEqual(
                expected: new PassiveSet(db, [random1]),
                actual: new PassiveSet(db, [random1]).Except(new PassiveSet(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [random1, random2]),
                actual: new PassiveSet(db, [random1, random2]).Except(new PassiveSet(db, [random2, runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1, random2]),
                actual: new PassiveSet(db, [runner, random1, random2]).Except(new PassiveSet(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, random1, random2]),
                actual: new PassiveSet(db, [runner, swift, random1, random2]).Except(new PassiveSet(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [random1, random2]),
                actual: new PassiveSet(db, [runner, swift, random1, random2]).Except(new PassiveSet(db, [random1, random2, swift, runner]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfNone_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, []).Except(new PassiveSet(db, [runner, swift, nimble, legend]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfOne_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [swift]).Except(new PassiveSet(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [nimble]).Except(new PassiveSet(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner]).Except(new PassiveSet(db, [swift, nimble]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, [swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift]),
                actual: new PassiveSet(db, [runner, swift]).Except(new PassiveSet(db, [runner, lucky, nimble, legend]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfThree_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, [swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [legend]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [legend, lucky]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [runner, legend]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [lucky, legend, swift, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [swift]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [lucky, legend, runner, nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble]).Except(new PassiveSet(db, [lucky, legend, runner, swift]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfFour_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: new PassiveSet(db, [swift, nimble, legend]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [runner]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, nimble, legend]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, legend]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [nimble]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [runner, swift, nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [legend]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [nimble, legend]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [nimble, legend]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [runner, swift, lucky]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, [nimble]),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [runner, swift, legend, lucky]))
            );

            Assert.AreEqual(
                expected: new PassiveSet(db, []),
                actual: new PassiveSet(db, [runner, swift, nimble, legend]).Except(new PassiveSet(db, [runner, swift, legend, nimble]))
            );
        }
    }
}
