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
        private PassiveSkill workaholic;

        private PassiveSkill random1 = new RandomPassiveSkill();
        private PassiveSkill random2 = new RandomPassiveSkill();
        private PassiveSkill random3 = new RandomPassiveSkill();
        private PassiveSkill random4 = new RandomPassiveSkill();
        private PassiveSkill random5 = new RandomPassiveSkill();
        private PassiveSkill random6 = new RandomPassiveSkill();

        [TestInitialize]
        public void InitDB()
        {
            db = PalDB.LoadEmbedded();

            runner = "Runner".ToStandardPassive(db);
            swift = "Swift".ToStandardPassive(db);
            nimble = "Nimble".ToStandardPassive(db);
            lucky = "Lucky".ToStandardPassive(db);
            legend = "Legend".ToStandardPassive(db);
            workaholic = "Workaholic".ToStandardPassive(db);
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
                PassiveSet.FromModel(db, []).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>(),
                // 'null' is equivalent to an empty slot
                PassiveSet.FromModel(db, [null]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner },
                PassiveSet.FromModel(db, [runner]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift },
                PassiveSet.FromModel(db, [runner, swift]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble },
                PassiveSet.FromModel(db, [runner, swift, nimble]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble, lucky },
                PassiveSet.FromModel(db, [runner, swift, nimble, lucky]).ModelObjects.ToList()
            );

            CollectionAssert.AreEquivalent(
                new List<PassiveSkill>() { runner, swift, nimble, lucky, legend },
                PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]).ModelObjects.ToList()
            );

            Assert.AreEqual(
                PassiveSet.FromModel(db, [runner, swift, nimble, lucky]),
                PassiveSet.FromModel(db, [swift, runner, lucky, nimble])
            );

            Assert.AreEqual(
                PassiveSet.FromModel(db, [runner, swift, nimble, lucky]),
                PassiveSet.FromModel(db, [lucky, nimble, swift, runner])
            );

            Assert.AreEqual(
                PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]),
                PassiveSet.FromModel(db, [lucky, legend, nimble, swift, runner])
            );
        }

        [TestMethod]
        public void PassiveSet_Contains()
        {
            Assert.IsFalse(
                PassiveSet.FromModel(db, []).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble, swift]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble, swift, legend]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble, swift, legend, lucky]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble, swift, legend, random1]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [nimble, swift, legend, random2]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [random1]).Contains(new Passive(db, runner))
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [random1]).Contains(Passive.Empty)
            );

            Assert.IsFalse(
                PassiveSet.FromModel(db, [random1]).Contains(new Passive(db, random1))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble]).Contains(new Passive(db, nimble))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new Passive(db, nimble))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new Passive(db, runner))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new Passive(db, swift))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new Passive(db, lucky))
            );

            Assert.IsTrue(
                PassiveSet.FromModel(db, [nimble, runner, swift, legend, lucky, random1]).Contains(new Passive(db, legend))
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(Passive.Empty)
            );
        }

        [TestMethod]
        public void PassiveSet_OfOne_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(new Passive(db, swift))
            );
                              
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(new Passive(db, swift))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ExceptSingle_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` passive should always noop

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(Passive.Empty)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(Passive.Empty)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(Passive.Random)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(Passive.Empty)
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1]),
                actual: PassiveSet.FromModel(db, [runner, random1]).Except(new Passive(db, nimble))
            );
        }

        [TestMethod]
        public void PassiveSet_OfThree_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(new Passive(db, legend))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, random1]),
                actual: PassiveSet.FromModel(db, [runner, swift, random1]).Except(new Passive(db, nimble))
            );
        }

        [TestMethod]
        public void PassiveSet_OfFive_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, swift, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new Passive(db, legend))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, legend, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Except(new Passive(db, lucky))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1]).Except(new Passive(db, legend))
            );
        }

        [TestMethod]
        public void PassiveSet_OfSix_ExceptSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, workaholic))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, swift, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, lucky))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, swift, workaholic, lucky]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, legend))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, workaholic, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, swift))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, workaholic, swift, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [workaholic, nimble, swift, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, workaholic]).Except(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, random1]),
                actual: PassiveSet.FromModel(db, [runner, nimble, swift, legend, lucky, random1]).Except(new Passive(db, workaholic))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ExceptSet_OfEmptyOrRandom()
        {
            // filtering out any `Empty` or `Random` passive should always noop


            // (filter by empty set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [random1, random2]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [random1, random2]))
            );


            // (filter by random+real set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [random1, random2, swift]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAnyRandom_ExceptSet_OfEmptyOrRandom()
        {
            // (filter by empty set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1]),
                actual: PassiveSet.FromModel(db, [runner, random1]).Except(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, random1, random2]).Except(PassiveSet.FromModel(db, []))
            );

            // (filter by random set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [random1, random2]).Except(PassiveSet.FromModel(db, [random2]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, random1, random2]).Except(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, random1, random2]).Except(PassiveSet.FromModel(db, [random1, random2]))
            );

            // (filter by random+real set)
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [random1]).Except(PassiveSet.FromModel(db, [random1, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [random1, random2]).Except(PassiveSet.FromModel(db, [random2, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, random1, random2]).Except(PassiveSet.FromModel(db, [random1, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, swift, random1, random2]).Except(PassiveSet.FromModel(db, [random1, random2, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, swift, random1, random2]).Except(PassiveSet.FromModel(db, [random1, random2, swift, runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, swift, legend, random1, random2]).Except(PassiveSet.FromModel(db, [random1, random2, swift, runner, legend]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Except(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfOne_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [swift]).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [nimble]).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Except(PassiveSet.FromModel(db, [swift, nimble]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Except(PassiveSet.FromModel(db, [runner, lucky, nimble, legend, random1]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfThree_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [legend, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [runner, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [lucky, legend, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [lucky, legend, runner, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Except(PassiveSet.FromModel(db, [lucky, legend, runner, swift, random1, random2]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfFour_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner, swift, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner, swift, legend, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner, swift, legend, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Except(PassiveSet.FromModel(db, [runner, swift, legend, nimble, lucky, random1, random2]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfSix_ExceptSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, runner, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, runner, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, lucky, runner, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift, nimble, lucky, legend, runner]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Except(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random2, workaholic]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_Intersect_EmptyOrRandom()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Intersect(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner]).Intersect(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(PassiveSet.FromModel(db, [random1, random2]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_Intersect_Any()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Intersect(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Intersect(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Intersect(PassiveSet.FromModel(db, [random1, random2, swift]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_Intersect_Any()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Intersect(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Intersect(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, [runner, swift]).Intersect(PassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Intersect(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, random1]).Intersect(PassiveSet.FromModel(db, [runner, swift, random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic, random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [random1, swift]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic, random1]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfSix_Intersect_Any()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner, swift, random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, legend]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, random1]).Intersect(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ConcatSingle_Empty()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Concat(Passive.Empty)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble]).Concat(Passive.Empty)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(Passive.Empty)
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Concat(Passive.Empty)
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ConcatSingle_Random()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, []).Concat(new Passive(db, random1))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [random1]).Concat(new Passive(db, random1))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2, random3, random4, random5]),
                actual: PassiveSet.FromModel(db, [random1, random2, random3, random4]).Concat(new Passive(db, random1))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ConcatSet_OfEmpty()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, []),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, [runner]).Concat(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, []))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]).Concat(PassiveSet.FromModel(db, []))
            );
        }

        [TestMethod]
        public void PassiveSet_OfAny_ConcatSet_OfRandom()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1]),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2]),
                actual: PassiveSet.FromModel(db, [random1]).Concat(PassiveSet.FromModel(db, [random1]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [random1, random2, random3, random4, random5, random6]),
                actual: PassiveSet.FromModel(db, [random1, random2, random3]).Concat(PassiveSet.FromModel(db, [random1, random2, random3]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_ConcatSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, [runner]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfZero_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner]),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, [runner]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, [runner, swift]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, []).Concat(PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ConcatSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(new Passive(db, nimble))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(new Passive(db, swift))
            );
        }

        [TestMethod]
        public void PassiveSet_OfTwo_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [runner, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [runner, swift, nimble]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [nimble, legend, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift]).Concat(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfFour_ConcatSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new Passive(db, lucky))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new Passive(db, runner))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(new Passive(db, swift))
            );
        }

        [TestMethod]
        public void PassiveSet_OfFour_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [runner, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [swift, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [nimble, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [legend, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, legend]).Concat(PassiveSet.FromModel(db, [lucky, workaholic, random1, random2]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1, random2]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, random1]).Concat(PassiveSet.FromModel(db, [runner, swift, nimble, legend, lucky, workaholic, random1]))
            );
        }

        [TestMethod]
        public void PassiveSet_OfSix_ConcatSingle()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, random1]).Concat(new Passive(db, workaholic))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Concat(new Passive(db, workaholic))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, legend, workaholic]).Concat(new Passive(db, runner))
            );
        }

        [TestMethod]
        public void PassiveSet_OfSix_ConcatSet_OfAny()
        {
            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend, workaholic]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(PassiveSet.FromModel(db, [legend, workaholic]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(PassiveSet.FromModel(db, [legend, runner, swift, nimble, lucky]))
            );

            Assert.AreEqual(
                expected: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2, legend, random3]),
                actual: PassiveSet.FromModel(db, [runner, swift, nimble, lucky, random1, random2]).Concat(PassiveSet.FromModel(db, [legend, runner, swift, nimble, random1]))
            );
        }
    }
}
