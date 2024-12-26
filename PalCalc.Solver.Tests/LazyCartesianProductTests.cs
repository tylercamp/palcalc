using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Tests
{
    [TestClass]
    public class LazyCartesianProductTests
    {
        [TestMethod]
        public void TestProductResults()
        {
            for (int sizeA = 0; sizeA < 10; sizeA++)
            {
                for (int sizeB = 0; sizeB < 10; sizeB++)
                {
                    var listA = Enumerable.Range(0, sizeA).ToList();
                    var listB = Enumerable.Range(100, sizeB).ToList();
                    var expected = listA.SelectMany(a => listB.Select(b => (a, b))).ToList();

                    var lcp = new LazyCartesianProduct<int>(listA, listB);

                    for (int batchSize = 1; batchSize < 20; batchSize++)
                    {
                        var actual = lcp.Chunks(batchSize).SelectMany(e => e).ToList();

                        var addedByError = actual.Except(expected).ToList();
                        var missingByError = expected.Except(actual).ToList();

                        Assert.AreEqual(0, addedByError.Count);
                        Assert.AreEqual(0, missingByError.Count);
                        Assert.AreEqual(expected.Count, actual.Count);
                    }
                }
            }
        }
    }
}
