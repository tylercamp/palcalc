using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PassiveProbabilities = PalCalc.Solver.Probabilities.Passives;

namespace PalCalc.Solver.Tests.Probabilities
{
    [TestClass]
    public class CombinatoricsChooseTests
    {
        [TestMethod]
        public void TestChoose()
        {
            Assert.AreEqual(1, PassiveProbabilities.Choose(0, 0));
            Assert.AreEqual(0, PassiveProbabilities.Choose(0, 1));
            Assert.AreEqual(0, PassiveProbabilities.Choose(0, 2));
            Assert.AreEqual(0, PassiveProbabilities.Choose(0, 3));
            Assert.AreEqual(0, PassiveProbabilities.Choose(0, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(1, 0));
            Assert.AreEqual(1, PassiveProbabilities.Choose(1, 1));
            Assert.AreEqual(0, PassiveProbabilities.Choose(1, 2));
            Assert.AreEqual(0, PassiveProbabilities.Choose(1, 3));
            Assert.AreEqual(0, PassiveProbabilities.Choose(1, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(2, 0));
            Assert.AreEqual(2, PassiveProbabilities.Choose(2, 1));
            Assert.AreEqual(1, PassiveProbabilities.Choose(2, 2));
            Assert.AreEqual(0, PassiveProbabilities.Choose(2, 3));
            Assert.AreEqual(0, PassiveProbabilities.Choose(2, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(3, 0));
            Assert.AreEqual(3, PassiveProbabilities.Choose(3, 1));
            Assert.AreEqual(3, PassiveProbabilities.Choose(3, 2));
            Assert.AreEqual(1, PassiveProbabilities.Choose(3, 3));
            Assert.AreEqual(0, PassiveProbabilities.Choose(3, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(4, 0));
            Assert.AreEqual(4, PassiveProbabilities.Choose(4, 1));
            Assert.AreEqual(6, PassiveProbabilities.Choose(4, 2));
            Assert.AreEqual(4, PassiveProbabilities.Choose(4, 3));
            Assert.AreEqual(1, PassiveProbabilities.Choose(4, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(5, 0));
            Assert.AreEqual(5, PassiveProbabilities.Choose(5, 1));
            Assert.AreEqual(10, PassiveProbabilities.Choose(5, 2));
            Assert.AreEqual(10, PassiveProbabilities.Choose(5, 3));
            Assert.AreEqual(5, PassiveProbabilities.Choose(5, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(6, 0));
            Assert.AreEqual(6, PassiveProbabilities.Choose(6, 1));
            Assert.AreEqual(15, PassiveProbabilities.Choose(6, 2));
            Assert.AreEqual(20, PassiveProbabilities.Choose(6, 3));
            Assert.AreEqual(15, PassiveProbabilities.Choose(6, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(7, 0));
            Assert.AreEqual(7, PassiveProbabilities.Choose(7, 1));
            Assert.AreEqual(21, PassiveProbabilities.Choose(7, 2));
            Assert.AreEqual(35, PassiveProbabilities.Choose(7, 3));
            Assert.AreEqual(35, PassiveProbabilities.Choose(7, 4));

            Assert.AreEqual(1, PassiveProbabilities.Choose(8, 0));
            Assert.AreEqual(8, PassiveProbabilities.Choose(8, 1));
            Assert.AreEqual(28, PassiveProbabilities.Choose(8, 2));
            Assert.AreEqual(56, PassiveProbabilities.Choose(8, 3));
            Assert.AreEqual(70, PassiveProbabilities.Choose(8, 4));
        }
    }
}
