using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FGenderTests : FAttrIdTests
    {
        [TestMethod]
        public void CtorIdentity()
        {
            Assert.AreEqual(Model.PalGender.MALE, new FGender(Model.PalGender.MALE).Value);
            Assert.AreEqual(Model.PalGender.FEMALE, new FGender(Model.PalGender.FEMALE).Value);
            Assert.AreEqual(Model.PalGender.WILDCARD, new FGender(Model.PalGender.WILDCARD).Value);
            Assert.AreEqual(Model.PalGender.OPPOSITE_WILDCARD, new FGender(Model.PalGender.OPPOSITE_WILDCARD).Value);
            Assert.AreEqual(Model.PalGender.NONE, new FGender(Model.PalGender.NONE).Value);
        }
    }
}

