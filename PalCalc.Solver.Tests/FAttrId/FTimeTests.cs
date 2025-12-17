using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FTimeTests : FAttrIdTests
    {
        [TestMethod]
        public void CtorIdentity()
        {
            Assert.AreEqual(TimeSpan.Zero, new FTime(TimeSpan.Zero).Value);
            Assert.AreEqual(TimeSpan.FromMinutes(100), new FTime(TimeSpan.FromMinutes(100)).Value);
        }
    }
}
