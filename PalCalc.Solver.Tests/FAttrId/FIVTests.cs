using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FIVTests : FAttrIdTests
    {
        [TestMethod]
        public void IV_CtorIdentity()
        {
            Assert.AreEqual(
                new IV_Range(isRelevant: true, value: 50),
                new FIV(isRelevant: true, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(isRelevant: false, value: 50),
                new FIV(isRelevant: false, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(IsRelevant: true, Min: 25, Max: 100),
                new FIV(isRelevant: true, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                new IV_Range(IsRelevant: false, Min: 25, Max: 100),
                new FIV(isRelevant: false, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                IV_Random.Instance,
                FIV.Random.ModelObject
            );
        }
    }
}
