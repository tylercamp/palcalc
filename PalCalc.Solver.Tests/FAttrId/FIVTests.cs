using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
                new IV_Value(IsRelevant: true, Min: 50, Max: 50),
                new FIV(isRelevant: true, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Value(IsRelevant: false, Min: 50, Max: 50),
                new FIV(isRelevant: false, value: 50).ModelObject
            );

            Assert.AreEqual(
                new IV_Value(IsRelevant: true, Min: 25, Max: 100),
                new FIV(isRelevant: true, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                new IV_Value(IsRelevant: false, Min: 25, Max: 100),
                new FIV(isRelevant: false, minValue: 25, maxValue: 100).ModelObject
            );

            Assert.AreEqual(
                IV_Value.Random,
                FIV.Random.ModelObject
            );
        }
    }
}
