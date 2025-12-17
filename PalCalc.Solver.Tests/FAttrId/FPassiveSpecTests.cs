using PalCalc.Solver.FImpl.AttrId;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests.FAttrId
{
    [TestClass]
    public class FPassiveSpecTests : FAttrIdTests
    {
        [TestMethod]
        public void FPassiveSpec_CtorIdentity()
        {
            var desired = FPassiveSet.FromModel(db, [runner, swift]);

            Assert.AreEqual(
                expected: new FPassiveSpec(CountStore: 2, MatchStore: 0b11_00_00_00),
                actual: FPassiveSpec.FromMatch(refSet: desired, passives: desired)
            );

            Assert.AreEqual(
                expected: desired,
                actual: FPassiveSpec.FromMatch(refSet: desired, passives: desired).ToFilteredSet(refSet: desired)
            );

            Assert.AreEqual(
                expected: FPassiveSet.Empty,
                actual: FPassiveSpec.FromMatch(refSet: desired, passives: FPassiveSet.Empty).ToFilteredSet(refSet: desired)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [runner, random1]),
                actual: FPassiveSpec.FromMatch(refSet: desired, passives: FPassiveSet.FromModel(db, [runner, nimble])).ToFilteredSet(refSet: desired)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSpec.FromMatch(refSet: desired, passives: FPassiveSet.FromModel(db, [nimble, legend])).ToFilteredSet(refSet: desired)
            );

            Assert.AreEqual(
                expected: FPassiveSet.FromModel(db, [random1, random2]),
                actual: FPassiveSpec.FromMatch(refSet: FPassiveSet.Empty, passives: desired).ToFilteredSet(refSet: FPassiveSet.Empty)
            );
        }
    }
}
