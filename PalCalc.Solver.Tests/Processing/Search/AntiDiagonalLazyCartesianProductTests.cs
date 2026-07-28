using PalCalc.Solver.Processing.Search;

namespace PalCalc.Solver.Tests.Processing.Search
{
    [TestClass]
    public class AntiDiagonalLazyCartesianProductTests
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

                    var product = new AntiDiagonalLazyCartesianProduct<int>(listA, listB);

                    for (int chunkSize = 1; chunkSize < 20; chunkSize++)
                    {
                        var chunks = product.Chunks(chunkSize).Select(chunk => chunk.ToList()).ToList();
                        var actual = chunks.SelectMany(chunk => chunk).ToList();

                        var addedByError = actual.Except(expected).ToList();
                        var missingByError = expected.Except(actual).ToList();

                        Assert.AreEqual(0, addedByError.Count);
                        Assert.AreEqual(0, missingByError.Count);
                        Assert.AreEqual(expected.Count, actual.Count);
                        Assert.IsTrue(chunks.All(chunk => chunk.Count <= chunkSize));
                    }
                }
            }
        }

        [TestMethod]
        public void ChunksAreEmittedInAntiDiagonalOrder()
        {
            var product = new AntiDiagonalLazyCartesianProduct<int>(
                [1, 2, 3],
                [7, 8, 9]
            );

            var actual = product.Chunks(4).Select(chunk => chunk.ToList()).ToList();
            List<List<(int, int)>> expected =
            [
                [(1, 7), (1, 8), (2, 7), (2, 8)],
                [(1, 9), (2, 9)],
                [(3, 7), (3, 8)],
                [(3, 9)]
            ];

            Assert.AreEqual(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
                CollectionAssert.AreEqual(expected[i], actual[i]);
        }
    }
}
