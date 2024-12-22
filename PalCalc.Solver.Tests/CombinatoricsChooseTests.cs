using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class CombinatoricsChooseTests
    {
        [TestMethod]
        public void TestChoose()
        {
            Assert.AreEqual(1, Probabilities.Passives.Choose(0, 0));
            Assert.AreEqual(0, Probabilities.Passives.Choose(0, 1));
            Assert.AreEqual(0, Probabilities.Passives.Choose(0, 2));
            Assert.AreEqual(0, Probabilities.Passives.Choose(0, 3));
            Assert.AreEqual(0, Probabilities.Passives.Choose(0, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(1, 0));
            Assert.AreEqual(1, Probabilities.Passives.Choose(1, 1));
            Assert.AreEqual(0, Probabilities.Passives.Choose(1, 2));
            Assert.AreEqual(0, Probabilities.Passives.Choose(1, 3));
            Assert.AreEqual(0, Probabilities.Passives.Choose(1, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(2, 0));
            Assert.AreEqual(2, Probabilities.Passives.Choose(2, 1));
            Assert.AreEqual(1, Probabilities.Passives.Choose(2, 2));
            Assert.AreEqual(0, Probabilities.Passives.Choose(2, 3));
            Assert.AreEqual(0, Probabilities.Passives.Choose(2, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(3, 0));
            Assert.AreEqual(3, Probabilities.Passives.Choose(3, 1));
            Assert.AreEqual(3, Probabilities.Passives.Choose(3, 2));
            Assert.AreEqual(1, Probabilities.Passives.Choose(3, 3));
            Assert.AreEqual(0, Probabilities.Passives.Choose(3, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(4, 0));
            Assert.AreEqual(4, Probabilities.Passives.Choose(4, 1));
            Assert.AreEqual(6, Probabilities.Passives.Choose(4, 2));
            Assert.AreEqual(4, Probabilities.Passives.Choose(4, 3));
            Assert.AreEqual(1, Probabilities.Passives.Choose(4, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(5, 0));
            Assert.AreEqual(5, Probabilities.Passives.Choose(5, 1));
            Assert.AreEqual(10, Probabilities.Passives.Choose(5, 2));
            Assert.AreEqual(10, Probabilities.Passives.Choose(5, 3));
            Assert.AreEqual(5, Probabilities.Passives.Choose(5, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(6, 0));
            Assert.AreEqual(6, Probabilities.Passives.Choose(6, 1));
            Assert.AreEqual(15, Probabilities.Passives.Choose(6, 2));
            Assert.AreEqual(20, Probabilities.Passives.Choose(6, 3));
            Assert.AreEqual(15, Probabilities.Passives.Choose(6, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(7, 0));
            Assert.AreEqual(7, Probabilities.Passives.Choose(7, 1));
            Assert.AreEqual(21, Probabilities.Passives.Choose(7, 2));
            Assert.AreEqual(35, Probabilities.Passives.Choose(7, 3));
            Assert.AreEqual(35, Probabilities.Passives.Choose(7, 4));

            Assert.AreEqual(1, Probabilities.Passives.Choose(8, 0));
            Assert.AreEqual(8, Probabilities.Passives.Choose(8, 1));
            Assert.AreEqual(28, Probabilities.Passives.Choose(8, 2));
            Assert.AreEqual(56, Probabilities.Passives.Choose(8, 3));
            Assert.AreEqual(70, Probabilities.Passives.Choose(8, 4));
        }
    }
}
