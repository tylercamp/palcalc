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
    public class FPassiveTests : FAttrIdTests
    {
        [TestMethod]
        public void Passive_CtorIdentity()
        {
            Assert.IsInstanceOfType<RandomPassiveSkill>(FPassive.Random.ModelObject);
            Assert.AreEqual(null, new FPassive(db, null).ModelObject);

            var runner = "Runner".ToStandardPassive(db);
            Assert.AreEqual(runner, new FPassive(db, runner).ModelObject);
        }

        [TestMethod]
        public void Passive_Valid_Properties()
        {
            Assert.IsFalse(new FPassive(db, runner).IsEmpty);
            Assert.IsFalse(new FPassive(db, runner).IsRandom);
        }

        [TestMethod]
        public void Passive_Random_Properties()
        {
            Assert.IsFalse(FPassive.Random.IsEmpty);
            Assert.IsTrue(FPassive.Random.IsRandom);
        }

        [TestMethod]
        public void Passive_Empty_Properties()
        {
            Assert.IsTrue(new FPassive(db, null).IsEmpty);
            Assert.IsFalse(new FPassive(db, null).IsRandom);
        }
    }
}
