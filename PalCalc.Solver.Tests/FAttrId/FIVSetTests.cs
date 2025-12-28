using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FIVSetTests : FAttrIdTests
    {
        [TestMethod]
        public void IVSet_CtorIdentity()
        {
            Assert.AreEqual(
                new IV_Set() { Attack = IV_Value.Random, Defense = IV_Value.Random, HP = IV_Value.Random },
                new FIVSet(FIV.Random, FIV.Random, FIV.Random).ModelObject
            );

            Assert.AreEqual(
                new IV_Set()
                {
                    Attack = new IV_Value(IsRelevant: true, Min: 10, Max: 10),
                    Defense = new IV_Value(IsRelevant: false, Min: 50, Max: 50),
                    HP = new IV_Value(IsRelevant: true, Min: 100, Max: 100)
                },
                new FIVSet(
                    Attack: new FIV(isRelevant: true, value: 10),
                    Defense: new FIV(isRelevant: false, value: 50),
                    HP: new FIV(isRelevant: true, value: 100)
                ).ModelObject
            );
        }
    }
}
